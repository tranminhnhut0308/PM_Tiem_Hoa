using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class BaoCaoChiTietCongNoTheoPhieuService
{
    private readonly Func<Task<MySqlConnection>> _connectionFactory;

    public BaoCaoChiTietCongNoTheoPhieuService(Func<Task<MySqlConnection>> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Đọc danh sách chi tiết công nợ theo từng chứng từ/phiếu với đầy đủ bộ lọc, phân trang và thống kê KPI.
    /// Hợp nhất dữ liệu hóa đơn bán hàng, phiếu nhập hàng và công nợ đầu kỳ.
    /// </summary>
    public async Task<(IReadOnlyList<BaoCaoChiTietCongNoPhieuItem> Items, int TotalCount, BaoCaoChiTietCongNoPhieuThongKe ThongKe)> LayBaoCaoPagedAsync(
        BaoCaoChiTietCongNoPhieuFilter filter)
    {
        filter ??= new BaoCaoChiTietCongNoPhieuFilter();
        var loaiDoiTac = (filter.LoaiDoiTac ?? "TAT_CA").Trim().ToUpperInvariant();
        var loaiCongNo = (filter.LoaiCongNo ?? "TAT_CA").Trim().ToUpperInvariant();
        var trangThai = (filter.TrangThaiThanhToan ?? "TAT_CA").Trim().ToUpperInvariant();
        var tuKhoa = string.IsNullOrWhiteSpace(filter.TuKhoa) ? null : filter.TuKhoa.Trim();

        await using var connection = await _connectionFactory();

        bool hasBangCongNoDauKy = false;
        try
        {
            hasBangCongNoDauKy = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_cong_no_dau_ky';") > 0;
        }
        catch { }

        var unionParts = new List<string>();

        // ============================================================
        // 1. HÓA ĐƠN BÁN HÀNG (Phải thu khách hàng)
        // ============================================================
        bool baoGomKhachHang = (loaiDoiTac is "TAT_CA" or "KHACH_HANG") && (loaiCongNo is "TAT_CA" or "PHAI_THU");
        if (baoGomKhachHang)
        {
            var khWhere = new StringBuilder("h.trang_thai_hoa_don != 'DA_HUY'");

            if (trangThai == "CON_NO")
                khWhere.Append(" AND h.con_no > 0");
            else if (trangThai == "DA_THANH_TOAN")
                khWhere.Append(" AND h.con_no <= 0");

            if (filter.TuNgay.HasValue)
                khWhere.Append(" AND h.thoi_gian_ban >= @TuNgay");

            if (filter.DenNgay.HasValue)
                khWhere.Append(" AND h.thoi_gian_ban <= @DenNgay");

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                khWhere.Append("""
                 AND (
                    h.ma_hoa_don LIKE @SearchLike
                    OR k.ma_khach_hang LIKE @SearchLike
                    OR k.ten_khach_hang LIKE @SearchLike
                    OR k.so_dien_thoai LIKE @SearchLike
                 )
                """);
            }

            unionParts.Add($"""
            SELECT 
                h.id                    AS Id,
                h.thoi_gian_ban         AS NgayChungTu,
                h.ma_hoa_don            AS MaChungTu,
                'Hóa đơn bán hàng'      AS LoaiChungTu,
                'HOA_DON'               AS MaLoaiChungTu,
                'KHACH_HANG'            AS LoaiDoiTac,
                'Phải thu'              AS LoaiCongNo,
                h.khach_hang_id         AS DoiTacId,
                COALESCE(k.ma_khach_hang, CONCAT('KH', LPAD(h.khach_hang_id, 4, '0'))) AS MaDoiTac,
                COALESCE(k.ten_khach_hang, 'Khách lẻ') AS TenDoiTac,
                COALESCE(k.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(h.khach_can_tra, 0) AS GiaTriPhatSinh,
                COALESCE(h.khach_da_thanh_toan, 0) AS DaThanhToan,
                COALESCE(h.con_no, 0)   AS ConNo,
                COALESCE(h.ghi_chu, '') AS GhiChu
            FROM hoa_don_ban_hang h
            LEFT JOIN khach_hang k ON k.id = h.khach_hang_id
            WHERE {khWhere}
            """);
        }

        // ============================================================
        // 2. PHIẾU NHẬP HÀNG (Phải trả nhà cung cấp)
        // ============================================================
        bool baoGomNhaCungCap = (loaiDoiTac is "TAT_CA" or "NHA_CUNG_CAP") && (loaiCongNo is "TAT_CA" or "PHAI_TRA");
        if (baoGomNhaCungCap)
        {
            var nccWhere = new StringBuilder("p.trang_thai != 'DA_HUY'");

            if (trangThai == "CON_NO")
                nccWhere.Append(" AND p.con_no > 0");
            else if (trangThai == "DA_THANH_TOAN")
                nccWhere.Append(" AND p.con_no <= 0");

            if (filter.TuNgay.HasValue)
                nccWhere.Append(" AND p.thoi_gian_nhap >= @TuNgay");

            if (filter.DenNgay.HasValue)
                nccWhere.Append(" AND p.thoi_gian_nhap <= @DenNgay");

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                nccWhere.Append("""
                 AND (
                    p.ma_phieu_nhap LIKE @SearchLike
                    OR n.ma_nha_cung_cap LIKE @SearchLike
                    OR n.ten_nha_cung_cap LIKE @SearchLike
                    OR n.so_dien_thoai LIKE @SearchLike
                 )
                """);
            }

            unionParts.Add($"""
            SELECT 
                p.id                    AS Id,
                p.thoi_gian_nhap        AS NgayChungTu,
                p.ma_phieu_nhap         AS MaChungTu,
                'Phiếu nhập hàng'       AS LoaiChungTu,
                'PHIEU_NHAP'            AS MaLoaiChungTu,
                'NHA_CUNG_CAP'          AS LoaiDoiTac,
                'Phải trả'              AS LoaiCongNo,
                p.nha_cung_cap_id       AS DoiTacId,
                COALESCE(n.ma_nha_cung_cap, CONCAT('NCC', LPAD(p.nha_cung_cap_id, 4, '0'))) AS MaDoiTac,
                COALESCE(n.ten_nha_cung_cap, 'Chưa rõ NCC') AS TenDoiTac,
                COALESCE(n.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(p.tong_tien_nhap, 0) AS GiaTriPhatSinh,
                COALESCE(p.da_thanh_toan, 0) AS DaThanhToan,
                COALESCE(p.con_no, 0)   AS ConNo,
                COALESCE(p.ghi_chu, '') AS GhiChu
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap n ON n.id = p.nha_cung_cap_id
            WHERE {nccWhere}
            """);
        }

        // ============================================================
        // 3. CÔNG NỢ ĐẦU KỲ
        // ============================================================
        if (hasBangCongNoDauKy)
        {
            var dkWhere = new StringBuilder("1 = 1");

            if (loaiDoiTac != "TAT_CA")
            {
                dkWhere.Append(" AND cndk.loai_doi_tac = @LoaiDoiTac");
            }
            if (loaiCongNo == "PHAI_THU")
            {
                dkWhere.Append(" AND cndk.loai_doi_tac = 'KHACH_HANG'");
            }
            else if (loaiCongNo == "PHAI_TRA")
            {
                dkWhere.Append(" AND cndk.loai_doi_tac = 'NHA_CUNG_CAP'");
            }

            if (trangThai == "CON_NO")
                dkWhere.Append(" AND cndk.so_tien > 0");

            if (filter.TuNgay.HasValue)
                dkWhere.Append(" AND cndk.ngay_dau_ky >= @TuNgay");

            if (filter.DenNgay.HasValue)
                dkWhere.Append(" AND cndk.ngay_dau_ky <= @DenNgay");

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                dkWhere.Append("""
                 AND (
                    cndk.ma_doi_tac LIKE @SearchLike
                    OR cndk.ten_doi_tac LIKE @SearchLike
                    OR cndk.so_dien_thoai LIKE @SearchLike
                    OR CONCAT('CNDK_', cndk.ma_doi_tac) LIKE @SearchLike
                 )
                """);
            }

            unionParts.Add($"""
            SELECT 
                cndk.id                 AS Id,
                cndk.ngay_dau_ky        AS NgayChungTu,
                CONCAT('CNDK_', cndk.ma_doi_tac) AS MaChungTu,
                'Công nợ đầu kỳ'        AS LoaiChungTu,
                'NO_DAU_KY'             AS MaLoaiChungTu,
                cndk.loai_doi_tac       AS LoaiDoiTac,
                CASE WHEN cndk.loai_doi_tac = 'KHACH_HANG' THEN 'Phải thu' ELSE 'Phải trả' END AS LoaiCongNo,
                cndk.doi_tac_id         AS DoiTacId,
                cndk.ma_doi_tac         AS MaDoiTac,
                cndk.ten_doi_tac        AS TenDoiTac,
                COALESCE(cndk.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(cndk.so_tien, 0) AS GiaTriPhatSinh,
                0                       AS DaThanhToan,
                COALESCE(cndk.so_tien, 0) AS ConNo,
                COALESCE(cndk.noi_dung, 'Số dư đầu kỳ') AS GhiChu
            FROM chi_tiet_cong_no_dau_ky cndk
            WHERE {dkWhere}
            """);
        }

        if (unionParts.Count == 0)
        {
            return (Array.Empty<BaoCaoChiTietCongNoPhieuItem>(), 0, new BaoCaoChiTietCongNoPhieuThongKe());
        }

        var fullUnion = string.Join("\nUNION ALL\n", unionParts);

        var queryParams = new
        {
            SearchLike = $"%{tuKhoa}%",
            TuNgay = filter.TuNgay?.Date,
            DenNgay = filter.DenNgay?.Date.AddDays(1).AddTicks(-1),
            LoaiDoiTac = loaiDoiTac,
            Offset = Math.Max(0, (filter.Page - 1) * filter.PageSize),
            Limit = Math.Max(1, filter.PageSize)
        };

        // 1. Thống kê KPI
        var summarySql = $"""
        SELECT 
            COUNT(*)                                AS TongSoChungTu,
            COALESCE(SUM(GiaTriPhatSinh), 0)        AS TongPhatSinh,
            COALESCE(SUM(DaThanhToan), 0)           AS TongDaThanhToan,
            COALESCE(SUM(ConNo), 0)                 AS TongConNo
        FROM (
            {fullUnion}
        ) AS T;
        """;

        var summary = await connection.QueryFirstOrDefaultAsync<BaoCaoChiTietCongNoPhieuThongKe>(summarySql, queryParams)
                      ?? new BaoCaoChiTietCongNoPhieuThongKe();

        // 2. Phân trang
        var pagedSql = $"""
        SELECT *
        FROM (
            {fullUnion}
        ) AS T
        ORDER BY NgayChungTu DESC, Id DESC
        LIMIT @Limit OFFSET @Offset;
        """;

        var rows = (await connection.QueryAsync<BaoCaoChiTietCongNoPhieuItem>(pagedSql, queryParams)).ToList();

        var stt = Math.Max(0, (filter.Page - 1) * filter.PageSize);
        foreach (var r in rows)
        {
            r.Stt = ++stt;
        }

        return (rows, summary.TongSoChungTu, summary);
    }

    /// <summary>
    /// Xuất toàn bộ danh sách chi tiết công nợ ra file Excel định dạng chuẩn nghiệp vụ.
    /// </summary>
    public async Task<int> XuatExcelAsync(
        BaoCaoChiTietCongNoPhieuFilter filter,
        string filePath)
    {
        // Lấy tất cả bản ghi không phân trang để xuất file
        var exportFilter = new BaoCaoChiTietCongNoPhieuFilter
        {
            TuNgay = filter.TuNgay,
            DenNgay = filter.DenNgay,
            LoaiDoiTac = filter.LoaiDoiTac,
            LoaiCongNo = filter.LoaiCongNo,
            TrangThaiThanhToan = filter.TrangThaiThanhToan,
            TuKhoa = filter.TuKhoa,
            Page = 1,
            PageSize = 100000
        };

        var (items, total, thongKe) = await LayBaoCaoPagedAsync(exportFilter);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Chi tiết công nợ");
        ws.ShowGridLines = true;

        // 1. Tiêu đề
        ws.Cell("A1").Value = "BÁO CÁO CHI TIẾT CÔNG NỢ THEO PHIẾU";
        ws.Range("A1:K1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#164B40");
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 2. Thời gian xuất & bộ lọc
        var tuNgayText = filter.TuNgay.HasValue ? filter.TuNgay.Value.ToString("dd/MM/yyyy") : "Đầu kỳ";
        var denNgayText = filter.DenNgay.HasValue ? filter.DenNgay.Value.ToString("dd/MM/yyyy") : "Hiện tại";
        ws.Cell("A2").Value = $"Khoảng thời gian: {tuNgayText} - {denNgayText}  •  Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Range("A2:K2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;
        ws.Cell("A2").Style.Font.FontSize = 11;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#555555");
        ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 3. Header bảng
        var headers = new[]
        {
            "STT", "Ngày chứng từ", "Mã chứng từ", "Loại chứng từ", "Loại nợ",
            "Mã đối tác", "Tên đối tác", "Giá trị phát sinh (đ)", "Đã thanh toán (đ)",
            "Còn nợ (đ)", "Trạng thái thanh toán", "Ghi chú"
        };

        const int startRow = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(startRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 11;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#087F6E");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#06695A");
        }
        ws.Row(startRow).Height = 26;

        // 4. Dữ liệu
        int curRow = startRow + 1;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            ws.Cell(curRow, 1).Value = i + 1;
            ws.Cell(curRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(curRow, 2).Value = item.NgayChungTu.ToString("dd/MM/yyyy HH:mm");
            ws.Cell(curRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(curRow, 3).Value = item.MaChungTu;
            ws.Cell(curRow, 3).Style.Font.Bold = true;
            ws.Cell(curRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(curRow, 4).Value = item.LoaiChungTu;
            ws.Cell(curRow, 5).Value = item.LoaiCongNo;
            ws.Cell(curRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(curRow, 6).Value = item.MaDoiTac;
            ws.Cell(curRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(curRow, 7).Value = item.TenDoiTac;

            ws.Cell(curRow, 8).Value = item.GiaTriPhatSinh;
            ws.Cell(curRow, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(curRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(curRow, 9).Value = item.DaThanhToan;
            ws.Cell(curRow, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(curRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(curRow, 10).Value = item.ConNo;
            ws.Cell(curRow, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(curRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            if (item.ConNo > 0)
            {
                ws.Cell(curRow, 10).Style.Font.Bold = true;
                ws.Cell(curRow, 10).Style.Font.FontColor = XLColor.FromHtml("#B42318");
            }

            ws.Cell(curRow, 11).Value = item.TrangThaiThanhToan;
            ws.Cell(curRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(curRow, 12).Value = item.GhiChu;

            // Kẻ viền và màu dòng chẵn lẻ
            var rowRange = ws.Range(curRow, 1, curRow, headers.Length);
            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2ECE8");
            rowRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2ECE8");

            if (i % 2 == 1)
            {
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FBFB");
            }

            curRow++;
        }

        // 5. Dòng tổng cộng
        var sumRow = curRow;
        ws.Cell(sumRow, 1).Value = "TỔNG CỘNG";
        ws.Range(sumRow, 1, sumRow, 7).Merge();
        ws.Cell(sumRow, 1).Style.Font.Bold = true;
        ws.Cell(sumRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(sumRow, 8).Value = thongKe.TongPhatSinh;
        ws.Cell(sumRow, 8).Style.Font.Bold = true;
        ws.Cell(sumRow, 8).Style.NumberFormat.Format = "#,##0";
        ws.Cell(sumRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(sumRow, 9).Value = thongKe.TongDaThanhToan;
        ws.Cell(sumRow, 9).Style.Font.Bold = true;
        ws.Cell(sumRow, 9).Style.NumberFormat.Format = "#,##0";
        ws.Cell(sumRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(sumRow, 10).Value = thongKe.TongConNo;
        ws.Cell(sumRow, 10).Style.Font.Bold = true;
        ws.Cell(sumRow, 10).Style.Font.FontColor = XLColor.FromHtml("#B42318");
        ws.Cell(sumRow, 10).Style.NumberFormat.Format = "#,##0";
        ws.Cell(sumRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var sumRange = ws.Range(sumRow, 1, sumRow, headers.Length);
        sumRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF5F1");
        sumRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        sumRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#087F6E");

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);

        return items.Count;
    }
}
