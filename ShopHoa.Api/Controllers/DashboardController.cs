using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopHoa.Api.Services;

namespace ShopHoa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDbConnectionFactory _db;

    public DashboardController(IDbConnectionFactory db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string range = "today",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var now = DateTime.Now;
        DateTime startDate;
        DateTime endDate;
        bool isHourly = false;

        switch (range.ToLowerInvariant())
        {
            case "yesterday":
                startDate = DateTime.Today.AddDays(-1);
                endDate = DateTime.Today.AddTicks(-1);
                isHourly = true;
                break;
            case "7days":
                startDate = DateTime.Today.AddDays(-6);
                endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                break;
            case "this_month":
                startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                break;
            case "custom":
                startDate = fromDate?.Date ?? DateTime.Today;
                endDate = (toDate?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);
                if ((endDate - startDate).TotalDays <= 2)
                    isHourly = true;
                break;
            case "today":
            default:
                startDate = DateTime.Today;
                endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                isHourly = true;
                break;
        }

        await using var conn = await _db.CreateConnectionAsync();

        // 1. Thống kê tổng hợp
        const string summarySql = """
            SELECT
                COALESCE(SUM(khach_can_tra), 0) AS TongDoanhThu,
                COUNT(*) AS SoHoaDon,
                COALESCE(SUM(khach_da_thanh_toan), 0) AS DaThu,
                COALESCE(SUM(con_no), 0) AS ConNo
            FROM hoa_don_ban_hang
            WHERE trang_thai_hoa_don = 'HOAN_THANH'
              AND thoi_gian_ban >= @StartDate AND thoi_gian_ban <= @EndDate;
            """;

        var summary = await conn.QueryFirstOrDefaultAsync<dynamic>(
            summarySql, new { StartDate = startDate, EndDate = endDate });

        // 2. Dữ liệu biểu đồ (Theo giờ nếu xem ngày, theo ngày nếu xem khoảng nhiều ngày)
        dynamic chartData;
        if (isHourly)
        {
            const string hourlySql = """
                SELECT
                    HOUR(thoi_gian_ban) AS Label,
                    COALESCE(SUM(khach_can_tra), 0) AS DoanhThu,
                    COUNT(*) AS SoDon
                FROM hoa_don_ban_hang
                WHERE trang_thai_hoa_don = 'HOAN_THANH'
                  AND thoi_gian_ban >= @StartDate AND thoi_gian_ban <= @EndDate
                GROUP BY HOUR(thoi_gian_ban)
                ORDER BY HOUR(thoi_gian_ban);
                """;

            var rows = (await conn.QueryAsync<dynamic>(
                hourlySql, new { StartDate = startDate, EndDate = endDate })).ToList();

            var full24Hours = new List<object>();
            var dict = rows.ToDictionary(r => (int)r.Label, r => r);

            for (int h = 6; h <= 22; h++) // Khung giờ mở cửa tiệm hoa thông thường 6h-22h
            {
                if (dict.TryGetValue(h, out var r))
                {
                    full24Hours.Add(new { Label = $"{h}h", DoanhThu = (decimal)r.DoanhThu, SoDon = (long)r.SoDon });
                }
                else
                {
                    full24Hours.Add(new { Label = $"{h}h", DoanhThu = 0m, SoDon = 0 });
                }
            }
            chartData = full24Hours;
        }
        else
        {
            const string dailySql = """
                SELECT
                    DATE_FORMAT(thoi_gian_ban, '%d/%m') AS Label,
                    COALESCE(SUM(khach_can_tra), 0) AS DoanhThu,
                    COUNT(*) AS SoDon
                FROM hoa_don_ban_hang
                WHERE trang_thai_hoa_don = 'HOAN_THANH'
                  AND thoi_gian_ban >= @StartDate AND thoi_gian_ban <= @EndDate
                GROUP BY DATE(thoi_gian_ban), DATE_FORMAT(thoi_gian_ban, '%d/%m')
                ORDER BY DATE(thoi_gian_ban);
                """;

            chartData = await conn.QueryAsync<dynamic>(
                dailySql, new { StartDate = startDate, EndDate = endDate });
        }

        // 3. Top 10 sản phẩm bán chạy
        const string topProductsSql = """
            SELECT
                ct.ten_san_pham AS TenSanPham,
                COALESCE(SUM(ct.so_luong), 0) AS SoLuong,
                COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu
            FROM chi_tiet_hoa_don_ban_hang ct
            INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
              AND hd.thoi_gian_ban >= @StartDate AND hd.thoi_gian_ban <= @EndDate
            GROUP BY ct.ten_san_pham
            ORDER BY DoanhThu DESC
            LIMIT 10;
            """;

        var topProducts = await conn.QueryAsync<dynamic>(
            topProductsSql, new { StartDate = startDate, EndDate = endDate });

        // 4. 10 đơn hàng mới nhất
        const string recentOrdersSql = """
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
            ORDER BY hd.thoi_gian_ban DESC, hd.id DESC
            LIMIT 10;
            """;

        var recentOrders = await conn.QueryAsync<dynamic>(recentOrdersSql);

        return Ok(new
        {
            Range = range,
            StartDate = startDate,
            EndDate = endDate,
            TongDoanhThu = (decimal)(summary?.TongDoanhThu ?? 0m),
            SoHoaDon = (long)(summary?.SoHoaDon ?? 0),
            DaThu = (decimal)(summary?.DaThu ?? 0m),
            ConNo = (decimal)(summary?.ConNo ?? 0m),
            BieuDo = chartData,
            Top10SanPham = topProducts,
            DonHangMoiNhat = recentOrders
        });
    }
}
