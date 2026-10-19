using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ dữ liệu cho GIAO DIỆN 68 – Báo cáo tổng hợp tình hình nhập/xuất hàng hóa.
/// Phân tích đối chiếu luồng nhập (Inflows) và luồng xuất (Outflows), tính chênh lệch vốn và tỷ lệ tiêu thụ.
/// Hỗ trợ 3 chế độ xem: Theo Mặt hàng, Theo Nhóm hàng, Theo Ngày.
/// </summary>
public sealed class BaoCaoTinhHinhNhapXuatService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;
    private const string MauHeaderBg = "#087F6E";
    private const string DinhDangTien = "#,##0\" đ\"";
    private const string DinhDangSo = "#,##0";
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public BaoCaoTinhHinhNhapXuatService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    private static async Task<bool> KiemTraBangTonTaiAsync(MySqlConnection conn, string tableName)
    {
        try
        {
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @Name;",
                new { Name = tableName });
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Đọc dữ liệu tổng hợp tình hình nhập/xuất trong khoảng thời gian xác định.
    /// </summary>
    public async Task<BaoCaoTinhHinhNhapXuatDuLieu> TaiBaoCaoTinhHinhAsync(
        DateTime tuNgay,
        DateTime denNgay,
        string cheDoXem = "THEO_HANG_HOA",
        int nhomId = -1,
        string? timKiem = null,
        CancellationToken token = default)
    {
        var tu = tuNgay.Date;
        var denKeTiep = denNgay.Date.AddDays(1);

        var ketQua = new BaoCaoTinhHinhNhapXuatDuLieu
        {
            TuNgay = tu,
            DenNgay = denNgay.Date,
            CheDoXem = cheDoXem
        };

        await using var conn = await _taoKetNoi();

        bool hasBangTraHang = await KiemTraBangTonTaiAsync(conn, "phieu_tra_hang");
        bool hasBangNhapKhac = await KiemTraBangTonTaiAsync(conn, "phieu_nhap_khac");
        bool hasBangXuatNoiBo = await KiemTraBangTonTaiAsync(conn, "phieu_xuat_noi_bo");
        bool hasBangXuatTraNcc = await KiemTraBangTonTaiAsync(conn, "phieu_xuat_tra_ncc");

        if (cheDoXem == "THEO_NGAY")
        {
            return await TaiTheoNgayAsync(conn, tu, denKeTiep, hasBangTraHang, hasBangNhapKhac, hasBangXuatTraNcc, hasBangXuatNoiBo, token);
        }

        // =========================================================================
        // CHẾ ĐỘ: THEO HÀNG HÓA HOẶC THEO NHÓM HÀNG
        // =========================================================================

        // 1. Danh sách sản phẩm
        var sqlSp = new StringBuilder("""
            SELECT
                sp.id                                                    AS SanPhamId,
                sp.ma_san_pham                                           AS MaSanPham,
                sp.ten_san_pham                                          AS TenSanPham,
                COALESCE(nsp.ten_nhom, 'Chưa phân nhóm')                AS TenNhom,
                COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái')          AS DonViTinh,
                COALESCE(sp.gia_von, 0)                                  AS GiaVon,
                COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0)           AS TonHienTai
            FROM san_pham sp
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
            WHERE 1=1
        """);

        var pSp = new DynamicParameters();
        if (nhomId > 0)
        {
            sqlSp.Append(" AND sp.nhom_san_pham_id = @NhomId");
            pSp.Add("NhomId", nhomId);
        }
        else if (nhomId == 0)
        {
            sqlSp.Append(" AND (sp.nhom_san_pham_id IS NULL OR sp.nhom_san_pham_id = 0)");
        }

        if (!string.IsNullOrWhiteSpace(timKiem))
        {
            sqlSp.Append(" AND (sp.ma_san_pham LIKE @Search OR sp.ten_san_pham LIKE @Search)");
            pSp.Add("Search", $"%{timKiem.Trim()}%");
        }

        sqlSp.Append(" ORDER BY nsp.ten_nhom, sp.ten_san_pham;");

        var dsSanPham = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlSp.ToString(), pSp, cancellationToken: token))).AsList();

        // 2. Nhập mua hàng trong kỳ
        const string sqlNhapMua = """
            SELECT
                ctpn.san_pham_id AS SanPhamId,
                COUNT(DISTINCT pn.id) AS SoLanNhap,
                COALESCE(SUM(ctpn.so_luong), 0) AS SoLuongNhap,
                COALESCE(SUM(ctpn.thanh_tien), 0) AS GiaTriNhap
            FROM chi_tiet_phieu_nhap_hang ctpn
            INNER JOIN phieu_nhap_hang pn ON pn.id = ctpn.phieu_nhap_hang_id
            WHERE pn.trang_thai != 'HUY' AND pn.thoi_gian_nhap >= @TuNgay AND pn.thoi_gian_nhap < @DenKeTiep
            GROUP BY ctpn.san_pham_id;
        """;

        var nhapMuaDict = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlNhapMua, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)))
            .ToDictionary(
                r => (uint)Convert.ToUInt32(r.SanPhamId),
                r => (
                    SoLan: Convert.ToInt32(r.SoLanNhap),
                    SoLuong: Convert.ToDecimal(r.SoLuongNhap),
                    GiaTri: Convert.ToDecimal(r.GiaTriNhap)
                ));

        // 3. Khách trả hàng (nhập lại vào kho)
        var nhapTraLaiDict = new Dictionary<uint, (int SoLan, decimal SoLuong, decimal GiaTri)>();
        if (hasBangTraHang)
        {
            try
            {
                const string sqlTraHang = """
                    SELECT
                        ctpt.san_pham_id AS SanPhamId,
                        COUNT(DISTINCT pt.id) AS SoLanNhap,
                        COALESCE(SUM(ctpt.so_luong), 0) AS SoLuongNhap,
                        COALESCE(SUM(ctpt.thanh_tien), 0) AS GiaTriNhap
                    FROM chi_tiet_phieu_tra_hang ctpt
                    INNER JOIN phieu_tra_hang pt ON pt.id = ctpt.phieu_tra_hang_id
                    WHERE pt.trang_thai = 'HOAN_THANH' AND pt.thoi_gian_tra >= @TuNgay AND pt.thoi_gian_tra < @DenKeTiep
                    GROUP BY ctpt.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlTraHang, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    nhapTraLaiDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToInt32(r.SoLanNhap),
                        Convert.ToDecimal(r.SoLuongNhap),
                        Convert.ToDecimal(r.GiaTriNhap)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_tra_hang", ex);
            }
        }

        // 4. Nhập khác
        var nhapKhacDict = new Dictionary<uint, (int SoLan, decimal SoLuong, decimal GiaTri)>();
        if (hasBangNhapKhac)
        {
            try
            {
                const string sqlNhapKhac = """
                    SELECT
                        ct.san_pham_id AS SanPhamId,
                        COUNT(DISTINCT p.id) AS SoLanNhap,
                        COALESCE(SUM(ct.so_luong), 0) AS SoLuongNhap,
                        COALESCE(SUM(ct.so_luong * COALESCE(ct.don_gia, 0)), 0) AS GiaTriNhap
                    FROM chi_tiet_phieu_nhap_khac ct
                    INNER JOIN phieu_nhap_khac p ON p.id = ct.phieu_nhap_khac_id
                    WHERE p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep
                    GROUP BY ct.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlNhapKhac, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    nhapKhacDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToInt32(r.SoLanNhap),
                        Convert.ToDecimal(r.SoLuongNhap),
                        Convert.ToDecimal(r.GiaTriNhap)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_nhap_khac", ex);
            }
        }

        // 5. Xuất bán hàng trong kỳ
        const string sqlXuatBan = """
            SELECT
                cthd.san_pham_id AS SanPhamId,
                COUNT(DISTINCT hd.id) AS SoLanXuat,
                COALESCE(SUM(cthd.so_luong), 0) AS SoLuongXuat,
                COALESCE(SUM(cthd.so_luong * COALESCE(cthd.gia_von, 0)), 0) AS GiaTriXuatVon,
                COALESCE(SUM(cthd.thanh_tien), 0) AS DoanhThuXuat
            FROM chi_tiet_hoa_don_ban_hang cthd
            INNER JOIN hoa_don_ban_hang hd ON hd.id = cthd.hoa_don_id
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH' AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
            GROUP BY cthd.san_pham_id;
        """;

        var xuatBanDict = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlXuatBan, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)))
            .ToDictionary(
                r => (uint)Convert.ToUInt32(r.SanPhamId),
                r => (
                    SoLan: Convert.ToInt32(r.SoLanXuat),
                    SoLuong: Convert.ToDecimal(r.SoLuongXuat),
                    GiaTriVon: Convert.ToDecimal(r.GiaTriXuatVon),
                    DoanhThu: Convert.ToDecimal(r.DoanhThuXuat)
                ));

        // 6. Xuất trả NCC
        var xuatTraNccDict = new Dictionary<uint, (int SoLan, decimal SoLuong, decimal GiaTriVon)>();
        if (hasBangXuatTraNcc)
        {
            try
            {
                const string sqlXuatTraNcc = """
                    SELECT
                        ct.san_pham_id AS SanPhamId,
                        COUNT(DISTINCT p.id) AS SoLanXuat,
                        COALESCE(SUM(ct.so_luong), 0) AS SoLuongXuat,
                        COALESCE(SUM(ct.so_luong * COALESCE(ct.don_gia, 0)), 0) AS GiaTriXuatVon
                    FROM chi_tiet_phieu_xuat_tra_ncc ct
                    INNER JOIN phieu_xuat_tra_ncc p ON p.id = ct.phieu_xuat_tra_ncc_id
                    WHERE p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep
                    GROUP BY ct.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlXuatTraNcc, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    xuatTraNccDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToInt32(r.SoLanXuat),
                        Convert.ToDecimal(r.SoLuongXuat),
                        Convert.ToDecimal(r.GiaTriXuatVon)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_xuat_tra_ncc", ex);
            }
        }

        // 7. Xuất nội bộ
        var xuatNoiBoDict = new Dictionary<uint, (int SoLan, decimal SoLuong, decimal GiaTriVon)>();
        if (hasBangXuatNoiBo)
        {
            try
            {
                const string sqlXuatNoiBo = """
                    SELECT
                        ct.san_pham_id AS SanPhamId,
                        COUNT(DISTINCT p.id) AS SoLanXuat,
                        COALESCE(SUM(ct.so_luong), 0) AS SoLuongXuat,
                        COALESCE(SUM(ct.so_luong * COALESCE(ct.don_gia, 0)), 0) AS GiaTriXuatVon
                    FROM chi_tiet_phieu_xuat_noi_bo ct
                    INNER JOIN phieu_xuat_noi_bo p ON p.id = ct.phieu_xuat_id
                    WHERE p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep
                    GROUP BY ct.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlXuatNoiBo, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    xuatNoiBoDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToInt32(r.SoLanXuat),
                        Convert.ToDecimal(r.SoLuongXuat),
                        Convert.ToDecimal(r.GiaTriVon)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_xuat_noi_bo", ex);
            }
        }

        // TỔNG HỢP THEO SẢN PHẨM
        var spItems = new List<BaoCaoTinhHinhNhapXuatItem>();

        foreach (var sp in dsSanPham)
        {
            uint sid = (uint)Convert.ToUInt32(sp.SanPhamId);
            string ma = sp.MaSanPham ?? "";
            string ten = sp.TenSanPham ?? "";
            string nhom = sp.TenNhom ?? "Chưa phân nhóm";
            string dvt = sp.DonViTinh ?? "Cái";
            decimal giaVon = Convert.ToDecimal(sp.GiaVon);
            decimal tonHienTai = Convert.ToDecimal(sp.TonHienTai);

            // Nhập
            int soLanNhap = 0;
            decimal slNhap = 0m;
            decimal gtNhap = 0m;

            if (nhapMuaDict.TryGetValue(sid, out var nm))
            {
                soLanNhap += nm.SoLan;
                slNhap += nm.SoLuong;
                gtNhap += nm.GiaTri;
            }
            if (nhapTraLaiDict.TryGetValue(sid, out var ntl))
            {
                soLanNhap += ntl.SoLan;
                slNhap += ntl.SoLuong;
                gtNhap += ntl.GiaTri;
            }
            if (nhapKhacDict.TryGetValue(sid, out var nk))
            {
                soLanNhap += nk.SoLan;
                slNhap += nk.SoLuong;
                gtNhap += nk.GiaTri;
            }

            // Xuất
            int soLanXuat = 0;
            decimal slXuat = 0m;
            decimal gtXuatVon = 0m;
            decimal dtXuat = 0m;

            if (xuatBanDict.TryGetValue(sid, out var xb))
            {
                soLanXuat += xb.SoLan;
                slXuat += xb.SoLuong;
                gtXuatVon += xb.GiaTriVon;
                dtXuat += xb.DoanhThu;
            }
            if (xuatTraNccDict.TryGetValue(sid, out var xtncc))
            {
                soLanXuat += xtncc.SoLan;
                slXuat += xtncc.SoLuong;
                gtXuatVon += xtncc.GiaTriVon;
                dtXuat += xtncc.GiaTriVon; // Xuất trả NCC ghi nhận tương đương
            }
            if (xuatNoiBoDict.TryGetValue(sid, out var xnb))
            {
                soLanXuat += xnb.SoLan;
                slXuat += xnb.SoLuong;
                gtXuatVon += xnb.GiaTriVon;
            }

            // Chỉ lấy các mặt hàng có phát sinh nhập/xuất hoặc có tồn kho
            if (slNhap == 0 && slXuat == 0 && tonHienTai == 0)
                continue;

            var item = new BaoCaoTinhHinhNhapXuatItem
            {
                Id = sid,
                MaDoiTuong = ma,
                TenDoiTuong = ten,
                DonViTinh = dvt,
                NhomHang = nhom,
                SoLanNhap = soLanNhap,
                SoLuongNhap = slNhap,
                GiaTriNhap = gtNhap,
                SoLanXuat = soLanXuat,
                SoLuongXuat = slXuat,
                GiaTriXuatVon = gtXuatVon,
                DoanhThuXuat = dtXuat,
                TonHienTai = tonHienTai,
                GiaTriTonHienTai = tonHienTai * giaVon
            };

            spItems.Add(item);
        }

        if (cheDoXem == "THEO_NHOM_HANG")
        {
            // Nhóm theo NhomHang
            var grouped = spItems.GroupBy(x => x.NhomHang).OrderBy(g => g.Key).ToList();
            var nhomItems = new List<BaoCaoTinhHinhNhapXuatItem>();
            int nhomStt = 0;

            foreach (var g in grouped)
            {
                nhomStt++;
                var gItem = new BaoCaoTinhHinhNhapXuatItem
                {
                    Stt = nhomStt,
                    MaDoiTuong = $"NHOM_{nhomStt:D2}",
                    TenDoiTuong = g.Key,
                    NhomHang = g.Key,
                    DonViTinh = $"{g.Count()} mặt hàng",
                    SoLanNhap = g.Sum(x => x.SoLanNhap),
                    SoLuongNhap = g.Sum(x => x.SoLuongNhap),
                    GiaTriNhap = g.Sum(x => x.GiaTriNhap),
                    SoLanXuat = g.Sum(x => x.SoLanXuat),
                    SoLuongXuat = g.Sum(x => x.SoLuongXuat),
                    GiaTriXuatVon = g.Sum(x => x.GiaTriXuatVon),
                    DoanhThuXuat = g.Sum(x => x.DoanhThuXuat),
                    TonHienTai = g.Sum(x => x.TonHienTai),
                    GiaTriTonHienTai = g.Sum(x => x.GiaTriTonHienTai)
                };
                nhomItems.Add(gItem);
            }

            ketQua.Items = nhomItems;
        }
        else
        {
            for (int i = 0; i < spItems.Count; i++)
                spItems[i].Stt = i + 1;

            ketQua.Items = spItems;
        }

        // TỔNG HỢP KPI CHUNG
        ketQua.TongSoLuongNhap = ketQua.Items.Sum(x => x.SoLuongNhap);
        ketQua.TongGiaTriNhap = ketQua.Items.Sum(x => x.GiaTriNhap);
        ketQua.TongSoLanNhap = ketQua.Items.Sum(x => x.SoLanNhap);

        ketQua.TongSoLuongXuat = ketQua.Items.Sum(x => x.SoLuongXuat);
        ketQua.TongGiaTriXuatVon = ketQua.Items.Sum(x => x.GiaTriXuatVon);
        ketQua.TongDoanhThuXuat = ketQua.Items.Sum(x => x.DoanhThuXuat);
        ketQua.TongSoLanXuat = ketQua.Items.Sum(x => x.SoLanXuat);

        return ketQua;
    }

    /// <summary>
    /// Tổng hợp tình hình nhập/xuất theo ngày trong khoảng thời gian.
    /// </summary>
    private static async Task<BaoCaoTinhHinhNhapXuatDuLieu> TaiTheoNgayAsync(
        MySqlConnection conn,
        DateTime tu,
        DateTime denKeTiep,
        bool hasBangTraHang,
        bool hasBangNhapKhac,
        bool hasBangXuatTraNcc,
        bool hasBangXuatNoiBo,
        CancellationToken token)
    {
        var ketQua = new BaoCaoTinhHinhNhapXuatDuLieu
        {
            TuNgay = tu,
            DenNgay = denKeTiep.AddDays(-1),
            CheDoXem = "THEO_NGAY"
        };

        // 1. Nhập mua theo ngày
        const string sqlNhapNgay = """
            SELECT 
                DATE(pn.thoi_gian_nhap) AS Ngay,
                COUNT(DISTINCT pn.id) AS SoLan,
                COALESCE(SUM(ct.so_luong), 0) AS SoLuong,
                COALESCE(SUM(ct.thanh_tien), 0) AS GiaTri
            FROM phieu_nhap_hang pn
            JOIN chi_tiet_phieu_nhap_hang ct ON ct.phieu_nhap_hang_id = pn.id
            WHERE pn.trang_thai != 'HUY' AND pn.thoi_gian_nhap >= @TuNgay AND pn.thoi_gian_nhap < @DenKeTiep
            GROUP BY DATE(pn.thoi_gian_nhap);
        """;
        var nhapNgayRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlNhapNgay, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token))).AsList();

        // 2. Xuất bán theo ngày
        const string sqlXuatNgay = """
            SELECT 
                DATE(hd.thoi_gian_ban) AS Ngay,
                COUNT(DISTINCT hd.id) AS SoLan,
                COALESCE(SUM(ct.so_luong), 0) AS SoLuong,
                COALESCE(SUM(ct.so_luong * COALESCE(ct.gia_von, 0)), 0) AS GiaTriVon,
                COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu
            FROM hoa_don_ban_hang hd
            JOIN chi_tiet_hoa_don_ban_hang ct ON ct.hoa_don_id = hd.id
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH' AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
            GROUP BY DATE(hd.thoi_gian_ban);
        """;
        var xuatNgayRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlXuatNgay, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token))).AsList();

        var nhapByDate = nhapNgayRows.ToDictionary(r => ((DateTime)r.Ngay).Date, r => (SoLan: Convert.ToInt32(r.SoLan), SoLuong: Convert.ToDecimal(r.SoLuong), GiaTri: Convert.ToDecimal(r.GiaTri)));
        var xuatByDate = xuatNgayRows.ToDictionary(r => ((DateTime)r.Ngay).Date, r => (SoLan: Convert.ToInt32(r.SoLan), SoLuong: Convert.ToDecimal(r.SoLuong), GiaTriVon: Convert.ToDecimal(r.GiaTriVon), DoanhThu: Convert.ToDecimal(r.DoanhThu)));

        var allDates = new HashSet<DateTime>(nhapByDate.Keys);
        allDates.UnionWith(xuatByDate.Keys);

        // Duyệt theo trình tự thời gian giảm dần
        var sortedDates = allDates.OrderByDescending(d => d).ToList();
        var items = new List<BaoCaoTinhHinhNhapXuatItem>();
        int stt = 0;

        foreach (var d in sortedDates)
        {
            stt++;
            nhapByDate.TryGetValue(d, out var n);
            xuatByDate.TryGetValue(d, out var x);

            var it = new BaoCaoTinhHinhNhapXuatItem
            {
                Stt = stt,
                MaDoiTuong = d.ToString("yyyy-MM-dd"),
                TenDoiTuong = d.ToString("dd/MM/yyyy"),
                DonViTinh = "Ngày",
                NhomHang = $"Tháng {d.Month:D2}/{d.Year}",
                SoLanNhap = n.SoLan,
                SoLuongNhap = n.SoLuong,
                GiaTriNhap = n.GiaTri,
                SoLanXuat = x.SoLan,
                SoLuongXuat = x.SoLuong,
                GiaTriXuatVon = x.GiaTriVon,
                DoanhThuXuat = x.DoanhThu
            };
            items.Add(it);
        }

        ketQua.Items = items;
        ketQua.TongSoLuongNhap = items.Sum(i => i.SoLuongNhap);
        ketQua.TongGiaTriNhap = items.Sum(i => i.GiaTriNhap);
        ketQua.TongSoLanNhap = items.Sum(i => i.SoLanNhap);

        ketQua.TongSoLuongXuat = items.Sum(i => i.SoLuongXuat);
        ketQua.TongGiaTriXuatVon = items.Sum(i => i.GiaTriXuatVon);
        ketQua.TongDoanhThuXuat = items.Sum(i => i.DoanhThuXuat);
        ketQua.TongSoLanXuat = items.Sum(i => i.SoLanXuat);

        return ketQua;
    }

    /// <summary>
    /// Xuất file Excel báo cáo tổng hợp tình hình nhập/xuất chuyên nghiệp bằng ClosedXML.
    /// </summary>
    public static XLWorkbook TaoWorkbookTinhHinhNhapXuat(BaoCaoTinhHinhNhapXuatDuLieu data)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("TinhHinhNhapXuat");

        // 1. TIÊU ĐỀ
        string tenCheDo = data.CheDoXem switch
        {
            "THEO_NHOM_HANG" => "THEO NHÓM HÀNG",
            "THEO_NGAY" => "THEO NGÀY",
            _ => "THEO MẶT HÀNG"
        };

        ws.Cell("A1").Value = $"BÁO CÁO TỔNG HỢP TÌNH HÌNH NHẬP / XUẤT HÀNG HÓA ({tenCheDo})";
        ws.Range("A1:K1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml(MauHeaderBg);
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell("A2").Value = $"Khoảng thời gian: {data.KhoangHienThi} • Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Range("A2:K2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;
        ws.Cell("A2").Style.Font.FontSize = 10;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#555555");
        ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 2. KHỐI TỔNG QUAN KPI
        ws.Cell("A4").Value = "Tổng giá trị nhập:";
        ws.Cell("B4").Value = data.TongGiaTriNhap;
        ws.Cell("B4").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("B4").Style.Font.Bold = true;

        ws.Cell("D4").Value = "Giá trị xuất vốn:";
        ws.Cell("E4").Value = data.TongGiaTriXuatVon;
        ws.Cell("E4").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("E4").Style.Font.Bold = true;

        ws.Cell("G4").Value = "Doanh thu bán xuất:";
        ws.Cell("H4").Value = data.TongDoanhThuXuat;
        ws.Cell("H4").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("H4").Style.Font.Bold = true;

        ws.Cell("A5").Value = "Chênh lệch giá trị (Ròng):";
        ws.Cell("B5").Value = data.ChenhLechGiaTriTong;
        ws.Cell("B5").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("B5").Style.Font.Bold = true;

        ws.Cell("D5").Value = "Lợi nhuận gộp:";
        ws.Cell("E5").Value = data.TongLoiNhuanGop;
        ws.Cell("E5").Style.NumberFormat.Format = DinhDangTien;
        ws.Cell("E5").Style.Font.Bold = true;

        ws.Cell("G5").Value = "Tỷ lệ tiêu thụ TB:";
        ws.Cell("H5").Value = $"{data.TyLeTieuThuTrungBinh:0.#}%";
        ws.Cell("H5").Style.Font.Bold = true;

        ws.Range("A4:H5").Style.Font.FontSize = 10;
        ws.Range("A4:H5").Style.Fill.BackgroundColor = XLColor.FromHtml("#F4FAF8");

        // 3. TIÊU ĐỀ CỘT
        int headerRow = 7;
        string[] headers =
        [
            "STT", "Mã", "Tên đối tượng", "ĐVT / Phân loại",
            "SL Nhập", "Giá trị nhập",
            "SL Xuất", "Giá trị xuất vốn", "Doanh thu xuất",
            "Chênh lệch SL", "Chênh lệch Tiền", "Tỷ lệ tiêu thụ", "Tồn hiện tại"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            cell.Style.Alignment.Horizontal = (i == 0 || i == 1 || i == 3 || i == 11)
                ? XLAlignmentHorizontalValues.Center
                : (i >= 4)
                    ? XLAlignmentHorizontalValues.Right
                    : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(headerRow).Height = 24;

        // 4. DỮ LIỆU
        int row = headerRow + 1;
        foreach (var it in data.Items)
        {
            ws.Cell(row, 1).Value = it.Stt;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 2).Value = it.MaDoiTuong;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Style.Font.Bold = true;

            ws.Cell(row, 3).Value = it.TenDoiTuong;

            ws.Cell(row, 4).Value = it.DonViTinh;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 5).Value = it.SoLuongNhap;
            ws.Cell(row, 5).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 6).Value = it.GiaTriNhap;
            ws.Cell(row, 6).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 7).Value = it.SoLuongXuat;
            ws.Cell(row, 7).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 8).Value = it.GiaTriXuatVon;
            ws.Cell(row, 8).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 9).Value = it.DoanhThuXuat;
            ws.Cell(row, 9).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 10).Value = it.ChenhLechSoLuong;
            ws.Cell(row, 10).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 11).Value = it.ChenhLechGiaTri;
            ws.Cell(row, 11).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 12).Value = $"{it.TyLeTieuThu:0.#}%";
            ws.Cell(row, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 13).Value = it.TonHienTai;
            ws.Cell(row, 13).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(row, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (row % 2 == 0)
                ws.Range(row, 1, row, 13).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FCFB");

            ws.Row(row).Height = 20;
            row++;
        }

        // 5. DÒNG TỔNG CỘNG
        int totalRow = row;
        ws.Cell(totalRow, 1).Value = "TỔNG CỘNG";
        ws.Range(totalRow, 1, totalRow, 4).Merge();
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(totalRow, 5).Value = data.TongSoLuongNhap;
        ws.Cell(totalRow, 5).Style.NumberFormat.Format = DinhDangSo;
        ws.Cell(totalRow, 5).Style.Font.Bold = true;
        ws.Cell(totalRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 6).Value = data.TongGiaTriNhap;
        ws.Cell(totalRow, 6).Style.NumberFormat.Format = DinhDangTien;
        ws.Cell(totalRow, 6).Style.Font.Bold = true;
        ws.Cell(totalRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 7).Value = data.TongSoLuongXuat;
        ws.Cell(totalRow, 7).Style.NumberFormat.Format = DinhDangSo;
        ws.Cell(totalRow, 7).Style.Font.Bold = true;
        ws.Cell(totalRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 8).Value = data.TongGiaTriXuatVon;
        ws.Cell(totalRow, 8).Style.NumberFormat.Format = DinhDangTien;
        ws.Cell(totalRow, 8).Style.Font.Bold = true;
        ws.Cell(totalRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 9).Value = data.TongDoanhThuXuat;
        ws.Cell(totalRow, 9).Style.NumberFormat.Format = DinhDangTien;
        ws.Cell(totalRow, 9).Style.Font.Bold = true;
        ws.Cell(totalRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 10).Value = data.ChenhLechSoLuongTong;
        ws.Cell(totalRow, 10).Style.NumberFormat.Format = DinhDangSo;
        ws.Cell(totalRow, 10).Style.Font.Bold = true;
        ws.Cell(totalRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 11).Value = data.ChenhLechGiaTriTong;
        ws.Cell(totalRow, 11).Style.NumberFormat.Format = DinhDangTien;
        ws.Cell(totalRow, 11).Style.Font.Bold = true;
        ws.Cell(totalRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 12).Value = $"{data.TyLeTieuThuTrungBinh:0.#}%";
        ws.Cell(totalRow, 12).Style.Font.Bold = true;
        ws.Cell(totalRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(totalRow, 13).Value = data.Items.Sum(x => x.TonHienTai);
        ws.Cell(totalRow, 13).Style.NumberFormat.Format = DinhDangSo;
        ws.Cell(totalRow, 13).Style.Font.Bold = true;
        ws.Cell(totalRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Range(totalRow, 1, totalRow, 13).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F4F0");
        ws.Range(totalRow, 1, totalRow, 13).Style.Font.Bold = true;
        ws.Row(totalRow).Height = 22;

        var dataRange = ws.Range(headerRow, 1, totalRow, 13);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E5E0");
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauHeaderBg);

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 6;
        ws.Column(3).Width = 28;

        return wb;
    }
}
