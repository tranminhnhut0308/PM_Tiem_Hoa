using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopHoa.Api.Services;

namespace ShopHoa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ReportController : ControllerBase
{
    private readonly IDbConnectionFactory _db;

    public ReportController(IDbConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Báo cáo cuối ngày: Doanh thu, số đơn, phân loại tiền mặt vs chuyển khoản, tổng chi và tồn quỹ.
    /// </summary>
    [HttpGet("end-of-day")]
    public async Task<IActionResult> GetEndOfDayReport([FromQuery] DateTime? date = null)
    {
        var targetDate = date?.Date ?? DateTime.Today;
        var start = targetDate;
        var end = targetDate.AddDays(1).AddTicks(-1);

        await using var conn = await _db.CreateConnectionAsync();

        // 1. Tổng quan doanh số bán hàng trong ngày
        const string salesSql = """
            SELECT
                COALESCE(SUM(khach_can_tra), 0) AS TongDoanhThu,
                COUNT(*) AS SoHoaDon,
                COALESCE(SUM(khach_da_thanh_toan), 0) AS DaThu,
                COALESCE(SUM(con_no), 0) AS ConNo,
                COALESCE(SUM(giam_gia), 0) AS TongGiamGia
            FROM hoa_don_ban_hang
            WHERE trang_thai_hoa_don = 'HOAN_THANH'
              AND thoi_gian_ban >= @Start AND thoi_gian_ban <= @End;
            """;

        var sales = await conn.QueryFirstOrDefaultAsync<dynamic>(salesSql, new { Start = start, End = end });

        // 2. Dòng tiền thực tế trong ngày (Phiếu thu & Phiếu chi từ sổ quỹ)
        const string cashFlowSql = """
            SELECT
                COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PT%' AND phuong_thuc = 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ThuTienMat,
                COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PT%' AND phuong_thuc <> 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ThuChuyenKhoan,
                COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PT%' THEN so_tien ELSE 0 END), 0) AS TongThu,
                COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PC%' AND phuong_thuc = 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ChiTienMat,
                COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PC%' AND phuong_thuc <> 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ChiChuyenKhoan,
                COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PC%' THEN so_tien ELSE 0 END), 0) AS TongChi
            FROM phieu_chi
            WHERE trang_thai = 'HOAN_THANH'
              AND thoi_gian_chi >= @Start AND thoi_gian_chi <= @End;
            """;

        var flow = await conn.QueryFirstOrDefaultAsync<dynamic>(cashFlowSql, new { Start = start, End = end });

        // 3. Danh sách hóa đơn bán trong ngày
        const string invoicesSql = """
            SELECT
                hd.id AS Id,
                hd.ma_hoa_don AS MaHoaDon,
                hd.thoi_gian_ban AS ThoiGianBan,
                COALESCE(kh.ten_khach_hang, 'Khách lẻ') AS TenKhachHang,
                hd.khach_can_tra AS KhachCanTra,
                hd.khach_da_thanh_toan AS KhachDaThanhToan,
                hd.con_no AS ConNo,
                hd.trang_thai_thanh_toan AS TrangThaiThanhToan
            FROM hoa_don_ban_hang hd
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
              AND hd.thoi_gian_ban >= @Start AND hd.thoi_gian_ban <= @End
            ORDER BY hd.thoi_gian_ban DESC, hd.id DESC;
            """;

        var invoices = await conn.QueryAsync<dynamic>(invoicesSql, new { Start = start, End = end });

        return Ok(new
        {
            Ngay = targetDate.ToString("yyyy-MM-dd"),
            NgayHienThi = targetDate.ToString("dd/MM/yyyy"),
            TongDoanhThu = (decimal)(sales?.TongDoanhThu ?? 0m),
            SoHoaDon = (long)(sales?.SoHoaDon ?? 0),
            DaThu = (decimal)(sales?.DaThu ?? 0m),
            ConNo = (decimal)(sales?.ConNo ?? 0m),
            TongGiamGia = (decimal)(sales?.TongGiamGia ?? 0m),
            ThuTienMat = (decimal)(flow?.ThuTienMat ?? 0m),
            ThuChuyenKhoan = (decimal)(flow?.ThuChuyenKhoan ?? 0m),
            TongThu = (decimal)(flow?.TongThu ?? 0m),
            ChiTienMat = (decimal)(flow?.ChiTienMat ?? 0m),
            ChiChuyenKhoan = (decimal)(flow?.ChiChuyenKhoan ?? 0m),
            TongChi = (decimal)(flow?.TongChi ?? 0m),
            ChenhLechThuChi = (decimal)((flow?.TongThu ?? 0m) - (flow?.TongChi ?? 0m)),
            DanhSachHoaDon = invoices
        });
    }

    /// <summary>
    /// Báo cáo khách hàng nợ: Xem ai đang nợ nhiều nhất.
    /// </summary>
    [HttpGet("debtors")]
    public async Task<IActionResult> GetDebtors()
    {
        await using var conn = await _db.CreateConnectionAsync();

        const string sql = """
            SELECT
                kh.id AS Id,
                kh.ma_khach_hang AS MaKhachHang,
                kh.ten_khach_hang AS TenKhachHang,
                kh.so_dien_thoai AS SoDienThoai,
                COALESCE(SUM(hd.con_no), 0) AS TongNo,
                COUNT(hd.id) AS SoDonChuaThanhToan,
                MAX(hd.thoi_gian_ban) AS NgayMuaGanNhat
            FROM khach_hang kh
            INNER JOIN hoa_don_ban_hang hd ON hd.khach_hang_id = kh.id
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
              AND hd.con_no > 0
              AND kh.trang_thai = 1
            GROUP BY kh.id, kh.ma_khach_hang, kh.ten_khach_hang, kh.so_dien_thoai
            ORDER BY TongNo DESC
            LIMIT 50;
            """;

        var debtors = await conn.QueryAsync<dynamic>(sql);
        var totalDebt = debtors.Sum(d => (decimal)d.TongNo);

        return Ok(new
        {
            TongNoToanBo = totalDebt,
            SoKhachNo = debtors.Count(),
            DanhSach = debtors
        });
    }

    /// <summary>
    /// Báo cáo sổ quỹ thu chi theo khoảng ngày.
    /// </summary>
    [HttpGet("cash-book")]
    public async Task<IActionResult> GetCashBook(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var start = fromDate?.Date ?? DateTime.Today.AddDays(-7);
        var end = (toDate?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

        await using var conn = await _db.CreateConnectionAsync();

        const string sql = """
            SELECT
                pc.id AS Id,
                pc.ma_phieu_chi AS MaPhieu,
                pc.thoi_gian_chi AS ThoiGian,
                CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN 'THU' ELSE 'CHI' END AS Loai,
                pc.so_tien AS SoTien,
                pc.phuong_thuc AS PhuongThuc,
                pc.nguoi_nhan_nop AS NguoiNhanNop,
                pc.ghi_chu AS GhiChu
            FROM phieu_chi pc
            WHERE pc.trang_thai = 'HOAN_THANH'
              AND pc.thoi_gian_chi >= @Start AND pc.thoi_gian_chi <= @End
            ORDER BY pc.thoi_gian_chi DESC, pc.id DESC
            LIMIT 100;
            """;

        var transactions = (await conn.QueryAsync<dynamic>(sql, new { Start = start, End = end })).ToList();

        var tongThu = transactions.Where(t => (string)t.Loai == "THU").Sum(t => (decimal)t.SoTien);
        var tongChi = transactions.Where(t => (string)t.Loai == "CHI").Sum(t => (decimal)t.SoTien);

        return Ok(new
        {
            FromDate = start.ToString("yyyy-MM-dd"),
            ToDate = end.ToString("yyyy-MM-dd"),
            TongThu = tongThu,
            TongChi = tongChi,
            ChenhLech = tongThu - tongChi,
            GiaoDich = transactions
        });
    }
}
