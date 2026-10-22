using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ dữ liệu cho GIAO DIỆN 67 – Báo cáo tổng hợp khuyến mãi.
/// Tổng hợp thông tin các chương trình khuyến mãi và hiệu quả thực tế áp dụng trên hóa đơn.
/// </summary>
public sealed class BaoCaoKhuyenMaiService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;
    private const string MauHeaderBg = "#087F6E";
    private const string DinhDangTien = "#,##0\" đ\"";
    private const string DinhDangSo = "#,##0";
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public BaoCaoKhuyenMaiService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    /// <summary>
    /// Đọc dữ liệu báo cáo tổng hợp khuyến mãi trong khoảng thời gian xác định.
    /// </summary>
    public async Task<BaoCaoTongHopKhuyenMaiDuLieu> TaiBaoCaoKhuyenMaiAsync(
        DateTime tuNgay,
        DateTime denNgay,
        string? trangThai = null,
        string? loaiKm = null,
        string? timKiem = null,
        CancellationToken token = default)
    {
        var tu = tuNgay.Date;
        var denKeTiep = denNgay.Date.AddDays(1);

        var ketQua = new BaoCaoTongHopKhuyenMaiDuLieu
        {
            TuNgay = tu,
            DenNgay = denNgay.Date
        };

        await using var conn = await _taoKetNoi();

        // 1. LẤY DANH SÁCH CHƯƠNG TRÌNH KHUYẾN MÃI
        var sqlKm = """
        SELECT 
            km.id AS Id,
            km.ma_chuong_trinh AS MaChuongTrinh,
            km.ten_chuong_trinh AS TenChuongTrinh,
            km.ngay_bat_dau AS NgayBatDau,
            km.ngay_ket_thuc AS NgayKetThuc,
            km.nhom_khach_hang_id AS NhomKhachHangId,
            nkh.ten_nhom AS TenNhomKhachHang,
            km.trang_thai AS TrangThai,
            km.loai_khuyen_mai AS LoaiKhuyenMai,
            km.ghi_chu AS GhiChu,
            km.chi_nhanh_id AS ChiNhanhId
        FROM chuong_trinh_khuyen_mai km
        LEFT JOIN nhom_khach_hang nkh ON nkh.id = km.nhom_khach_hang_id
        WHERE km.ngay_bat_dau < @DenKeTiep AND km.ngay_ket_thuc >= @TuNgay
        """;

        var param = new DynamicParameters();
        param.Add("TuNgay", tu);
        param.Add("DenKeTiep", denKeTiep);

        if (!string.IsNullOrWhiteSpace(loaiKm) && loaiKm != "TAT_CA")
        {
            sqlKm += " AND km.loai_khuyen_mai = @LoaiKm";
            param.Add("LoaiKm", loaiKm);
        }

        sqlKm += " ORDER BY km.id DESC;";

        var kmRows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlKm, param, cancellationToken: token))).AsList();

        if (kmRows.Count == 0)
        {
            return ketQua;
        }

        var ctKmIds = kmRows.Select(r => (uint)r.Id).ToList();

        // 2. LẤY CHI TIẾT SẢN PHẨM KHUYẾN MÃI
        var sqlDetails = """
        SELECT 
            ct.chuong_trinh_id AS CtId,
            ct.san_pham_id AS SanPhamId,
            ct.loai_giam_gia AS LoaiGiamGia,
            ct.gia_tri_giam AS GiaTriGiam,
            ct.so_luong_tang AS SoLuongTang,
            ct.ten_san_pham_tang AS TenSanPhamTang
        FROM chi_tiet_khuyen_mai ct
        WHERE ct.chuong_trinh_id IN @CtIds;
        """;

        var details = (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlDetails, new { CtIds = ctKmIds }, cancellationToken: token))).AsList();
        var detailsByCt = details.GroupBy(d => (uint)d.CtId).ToDictionary(g => g.Key, g => g.ToList());

        // 3. THỐNG KÊ HÓA ĐƠN BÁN HÀNG PHÁT SINH TRONG KHOẢNG THỜI GIAN
        // Đọc các hóa đơn trong khoảng [tuNgay, denKeTiep) có giảm giá hoặc có sản phẩm
        var sqlHoaDon = """
        SELECT 
            hd.id AS Id,
            hd.ma_hoa_don AS MaHoaDon,
            hd.thoi_gian_ban AS ThoiGianBan,
            hd.tong_tien_hang AS TongTienHang,
            hd.giam_gia AS GiamGia,
            hd.khach_can_tra AS KhachCanTra,
            hd.khach_hang_id AS KhachHangId
        FROM hoa_don_ban_hang hd
        WHERE hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep;
        """;

        var hoaDons = (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlHoaDon, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token))).AsList();

        // Đọc chi tiết sản phẩm của các hóa đơn này để gắn với sản phẩm khuyến mãi
        var sqlHdDetails = """
        SELECT 
            ct.hoa_don_id AS HoaDonId,
            ct.san_pham_id AS SanPhamId,
            ct.so_luong AS SoLuong,
                ct.don_gia AS DonGiaBan,
            ct.thanh_tien AS ThanhTien
        FROM chi_tiet_hoa_don_ban_hang ct
        JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
        WHERE hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep;
        """;

        var hdItems = (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlHdDetails, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token))).AsList();
        var hdItemsByInvoice = hdItems.GroupBy(i => (uint)i.HoaDonId).ToDictionary(g => g.Key, g => g.ToList());

        var today = DateTime.Today;
        var items = new List<BaoCaoTongHopKhuyenMaiItem>();
        int stt = 0;

        foreach (var km in kmRows)
        {
            uint id = (uint)km.Id;
            string ma = km.MaChuongTrinh ?? "";
            string ten = km.TenChuongTrinh ?? "";
            string loai = km.LoaiKhuyenMai ?? "THEO_HANG_HOA";
            string rawTrangThai = km.TrangThai ?? "DANG_HOAT_DONG";
            DateTime start = (DateTime)km.NgayBatDau;
            DateTime end = (DateTime)km.NgayKetThuc;
            string? tenNhomKhach = km.TenNhomKhachHang;
            string ghiChu = km.GhiChu ?? "";

            // Trạng thái tính toán
            string ttTinhToan;
            if (string.Equals(rawTrangThai, "TAM_NGUNG", StringComparison.OrdinalIgnoreCase))
                ttTinhToan = "TAM_NGUNG";
            else if (start.Date > today)
                ttTinhToan = "CHUA_BAT_DAU";
            else if (end.Date < today)
                ttTinhToan = "DA_KET_THUC";
            else
                ttTinhToan = "DANG_HOAT_DONG";

            // Lọc theo trạng thái nếu có
            if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
            {
                if (!string.Equals(ttTinhToan, trangThai, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrWhiteSpace(timKiem))
            {
                var kw = timKiem.Trim().ToLowerInvariant();
                var match = ma.ToLowerInvariant().Contains(kw)
                            || ten.ToLowerInvariant().Contains(kw)
                            || ghiChu.ToLowerInvariant().Contains(kw);
                if (!match) continue;
            }

            // Tóm tắt ưu đãi & số lượng mặt hàng
            var ctList = detailsByCt.TryGetValue(id, out var dList) ? dList : new List<dynamic>();
            int soMatHang = ctList.Select(d => (uint)d.SanPhamId).Distinct().Count();
            string tomTatUuDai = "";

            if (ctList.Count > 0)
            {
                var firstDet = ctList.First();
                decimal soLuongTang = (decimal)(firstDet.SoLuongTang ?? 0m);
                string? tenTang = firstDet.TenSanPhamTang;
                string loaiGiam = firstDet.LoaiGiamGia ?? "PHAN_TRAM";
                decimal giaTriGiam = (decimal)(firstDet.GiaTriGiam ?? 0m);

                if (soLuongTang > 0)
                {
                    tomTatUuDai = $"Tặng {soLuongTang:0.##} {(string.IsNullOrWhiteSpace(tenTang) ? "sản phẩm" : tenTang)}";
                }
                else if (loaiGiam == "PHAN_TRAM")
                {
                    tomTatUuDai = giaTriGiam > 0 ? $"Giảm {giaTriGiam:0.##}%" : "Ưu đãi theo %";
                }
                else
                {
                    tomTatUuDai = giaTriGiam > 0 ? $"Giảm {giaTriGiam:N0} đ" : "Ưu đãi trừ tiền";
                }

                if (soMatHang > 0)
                    tomTatUuDai += $" ({soMatHang} mặt hàng)";
            }
            else
            {
                tomTatUuDai = loai switch
                {
                    "THEO_HOA_DON" => "Chiết khấu theo hóa đơn",
                    "TANG_KEM" => "Tặng quà kèm hóa đơn",
                    _ => "Toàn bộ danh mục"
                };
            }

            // TÍNH TOÁN HIỆU QUẢ TRÊN HÓA ĐƠN
            // Hóa đơn xảy ra trong khoảng giao nhau giữa [start, end] và [tu, denKeTiep)
            var startHieuLuc = start.Date > tu ? start.Date : tu;
            var endHieuLuc = end.Date.AddDays(1) < denKeTiep ? end.Date.AddDays(1) : denKeTiep;

            var spKmSet = new HashSet<uint>(ctList.Select(d => (uint)d.SanPhamId).Where(sid => sid > 0));

            int soHd = 0;
            decimal doanhThu = 0m;
            decimal tienKm = 0m;

            if (startHieuLuc < endHieuLuc)
            {
                foreach (var hd in hoaDons)
                {
                    DateTime tg = (DateTime)hd.ThoiGianBan;
                    if (tg < startHieuLuc || tg >= endHieuLuc)
                        continue;

                    uint hdId = (uint)hd.Id;
                    decimal hdGiamGia = (decimal)(hd.GiamGia ?? 0m);
                    decimal hdKhachCanTra = (decimal)(hd.KhachCanTra ?? 0m);
                    decimal hdTongTien = (decimal)(hd.TongTienHang ?? 0m);

                    if (spKmSet.Count > 0)
                    {
                        // Kiểm tra xem hóa đơn có chứa sản phẩm khuyến mãi nào không
                        if (hdItemsByInvoice.TryGetValue(hdId, out var itemsInHd))
                        {
                            var matchingItems = itemsInHd.Where(i => spKmSet.Contains((uint)i.SanPhamId)).ToList();
                            if (matchingItems.Count > 0)
                            {
                                soHd++;
                                decimal ttMatching = matchingItems.Sum(i => (decimal)(i.ThanhTien ?? 0m));
                                doanhThu += ttMatching;

                                // Ước tính phần giảm giá cho hóa đơn đó
                                if (hdTongTien > 0 && hdGiamGia > 0)
                                {
                                    tienKm += Math.Round(hdGiamGia * (ttMatching / hdTongTien), 0);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Khuyến mãi chung (theo hóa đơn hoặc toàn danh mục)
                        if (hdGiamGia > 0)
                        {
                            soHd++;
                            doanhThu += hdKhachCanTra;
                            tienKm += hdGiamGia;
                        }
                    }
                }
            }

            stt++;
            var item = new BaoCaoTongHopKhuyenMaiItem
            {
                Stt = stt,
                Id = id,
                MaChuongTrinh = ma,
                TenChuongTrinh = ten,
                LoaiKhuyenMai = loai,
                LoaiKhuyenMaiHienThi = loai switch
                {
                    "THEO_HANG_HOA" => "Theo hàng hóa",
                    "THEO_HOA_DON" => "Theo hóa đơn",
                    "TANG_KEM" => "Tặng hàng kèm",
                    _ => "Chiết khấu giá"
                },
                NgayBatDau = start,
                NgayKetThuc = end,
                DoiTuongHienThi = string.IsNullOrWhiteSpace(tenNhomKhach) ? "Tất cả khách hàng" : $"Nhóm: {tenNhomKhach}",
                TrangThai = ttTinhToan,
                TrangThaiHienThi = ttTinhToan switch
                {
                    "DANG_HOAT_DONG" => "Đang diễn ra",
                    "CHUA_BAT_DAU" => "Chưa bắt đầu",
                    "TAM_NGUNG" => "Tạm ngưng",
                    "DA_KET_THUC" => "Đã kết thúc",
                    _ => rawTrangThai
                },
                TrangThaiBadgeBg = ttTinhToan switch
                {
                    "DANG_HOAT_DONG" => "#E6F4EA",
                    "CHUA_BAT_DAU" => "#E8F0FE",
                    "TAM_NGUNG" => "#FEF7E0",
                    "DA_KET_THUC" => "#F1F3F4",
                    _ => "#F1F3F4"
                },
                TrangThaiBadgeFg = ttTinhToan switch
                {
                    "DANG_HOAT_DONG" => "#137333",
                    "CHUA_BAT_DAU" => "#1A73E8",
                    "TAM_NGUNG" => "#B06000",
                    "DA_KET_THUC" => "#5F6368",
                    _ => "#5F6368"
                },
                SoLuongSanPham = soMatHang,
                TomTatUuDai = tomTatUuDai,
                SoHoaDonApDung = soHd,
                TongDoanhThu = doanhThu,
                TongTienKhuyenMai = tienKm,
                GhiChu = ghiChu
            };

            items.Add(item);
        }

        // TỔNG HỢP KPI
        ketQua.Items = items;
        ketQua.TongSoChuongTrinh = items.Count;
        ketQua.SoChuongTrinhDangChay = items.Count(x => x.TrangThai == "DANG_HOAT_DONG");
        ketQua.TongSoMatHangApDung = items.Sum(x => x.SoLuongSanPham);
        ketQua.TongSoHoaDonApDung = items.Sum(x => x.SoHoaDonApDung);
        ketQua.TongDoanhThu = items.Sum(x => x.TongDoanhThu);
        ketQua.TongGiaTriKhuyenMai = items.Sum(x => x.TongTienKhuyenMai);

        return ketQua;
    }

    /// <summary>
    /// Xuất file Excel báo cáo tổng hợp khuyến mãi chuyên nghiệp bằng ClosedXML.
    /// </summary>
    public static XLWorkbook TaoWorkbookKhuyenMai(BaoCaoTongHopKhuyenMaiDuLieu data)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("TongHopKhuyenMai");

        // 1. TIÊU ĐỀ BÁO CÁO
        ws.Cell("A1").Value = "BÁO CÁO TỔNG HỢP CHƯƠNG TRÌNH KHUYẾN MÃI";
        ws.Range("A1:K1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml(MauHeaderBg);
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Dòng phụ: Thời gian
        ws.Cell("A2").Value = $"Khoảng thời gian: {data.KhoangHienThi} • Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Range("A2:K2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;
        ws.Cell("A2").Style.Font.FontSize = 10;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#555555");
        ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 2. KHỐI THỐNG KÊ TỔNG QUAN (KPI)
        ws.Cell("A4").Value = "Tổng số chương trình:";
        ws.Cell("B4").Value = data.TongSoChuongTrinh;
        ws.Cell("B4").Style.Font.Bold = true;

        ws.Cell("D4").Value = "Đang diễn ra:";
        ws.Cell("E4").Value = data.SoChuongTrinhDangChay;
        ws.Cell("E4").Style.Font.Bold = true;

        ws.Cell("G4").Value = "Mặt hàng áp dụng:";
        ws.Cell("H4").Value = data.TongSoMatHangApDung;
        ws.Cell("H4").Style.Font.Bold = true;

        ws.Cell("A5").Value = "Số hóa đơn áp dụng:";
        ws.Cell("B5").Value = data.TongSoHoaDonApDung;
        ws.Cell("B5").Style.Font.Bold = true;

        ws.Cell("D5").Value = "Tổng doanh thu:";
        ws.Cell("E5").Value = data.TongDoanhThu;
        ws.Cell("E5").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("E5").Style.Font.Bold = true;

        ws.Cell("G5").Value = "Tổng tiền khuyến mãi:";
        ws.Cell("H5").Value = data.TongGiaTriKhuyenMai;
        ws.Cell("H5").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("H5").Style.Font.Bold = true;

        ws.Range("A4:H5").Style.Font.FontSize = 10;
        ws.Range("A4:H5").Style.Fill.BackgroundColor = XLColor.FromHtml("#F4FAF8");

        // 3. TIÊU ĐỀ BẢNG DỮ LIỆU
        int headerRow = 7;
        string[] headers =
        [
            "STT", "Mã CT", "Tên chương trình", "Loại khuyến mãi",
            "Thời gian áp dụng", "Đối tượng", "Mặt hàng", "Ưu đãi",
            "Trạng thái", "Hóa đơn", "Doanh thu", "Tiền KM đã giảm"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            cell.Style.Alignment.Horizontal = (i == 0 || i == 1 || i == 3 || i == 4 || i == 8)
                ? XLAlignmentHorizontalValues.Center
                : (i == 6 || i == 9 || i == 10 || i == 11)
                    ? XLAlignmentHorizontalValues.Right
                    : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(headerRow).Height = 24;

        // 4. CÁC DÒNG DỮ LIỆU
        int row = headerRow + 1;
        foreach (var it in data.Items)
        {
            ws.Cell(row, 1).Value = it.Stt;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 2).Value = it.MaChuongTrinh;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Style.Font.Bold = true;

            ws.Cell(row, 3).Value = it.TenChuongTrinh;

            ws.Cell(row, 4).Value = it.LoaiKhuyenMaiHienThi;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 5).Value = it.ThoiGianHienThi;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 6).Value = it.DoiTuongHienThi;

            ws.Cell(row, 7).Value = it.SoLuongSanPham;
            ws.Cell(row, 7).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 8).Value = it.TomTatUuDai;

            ws.Cell(row, 9).Value = it.TrangThaiHienThi;
            ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 10).Value = it.SoHoaDonApDung;
            ws.Cell(row, 10).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 11).Value = it.TongDoanhThu;
            ws.Cell(row, 11).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 12).Value = it.TongTienKhuyenMai;
            ws.Cell(row, 12).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(row, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (row % 2 == 0)
            {
                ws.Range(row, 1, row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FCFB");
            }

            ws.Row(row).Height = 20;
            row++;
        }

        // 5. DÒNG TỔNG CỘNG
        int totalRow = row;
        ws.Cell(totalRow, 1).Value = "TỔNG CỘNG";
        ws.Range(totalRow, 1, totalRow, 6).Merge();
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(totalRow, 7).Value = data.TongSoMatHangApDung;
        ws.Cell(totalRow, 7).Style.NumberFormat.Format = DinhDangSo;
        ws.Cell(totalRow, 7).Style.Font.Bold = true;
        ws.Cell(totalRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 8).Value = "";

        ws.Cell(totalRow, 9).Value = "";

        ws.Cell(totalRow, 10).Value = data.TongSoHoaDonApDung;
        ws.Cell(totalRow, 10).Style.NumberFormat.Format = DinhDangSo;
        ws.Cell(totalRow, 10).Style.Font.Bold = true;
        ws.Cell(totalRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 11).Value = data.TongDoanhThu;
        ws.Cell(totalRow, 11).Style.NumberFormat.Format = DinhDangTien;
        ws.Cell(totalRow, 11).Style.Font.Bold = true;
        ws.Cell(totalRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 12).Value = data.TongGiaTriKhuyenMai;
        ws.Cell(totalRow, 12).Style.NumberFormat.Format = DinhDangTien;
        ws.Cell(totalRow, 12).Style.Font.Bold = true;
        ws.Cell(totalRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Range(totalRow, 1, totalRow, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F4F0");
        ws.Range(totalRow, 1, totalRow, 12).Style.Font.Bold = true;
        ws.Row(totalRow).Height = 22;

        // Border cho toàn bảng
        var dataRange = ws.Range(headerRow, 1, totalRow, 12);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E5E0");
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauHeaderBg);

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 6;
        ws.Column(3).Width = 28;
        ws.Column(8).Width = 24;

        return wb;
    }
}
