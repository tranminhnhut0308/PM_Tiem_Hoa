using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopHoa.Api.Models;
using ShopHoa.Api.Services;

namespace ShopHoa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    private readonly IJwtTokenService _jwt;

    public AuthController(IDbConnectionFactory db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenDangNhap) || string.IsNullOrWhiteSpace(request.MatKhau))
        {
            return BadRequest(new { Message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu." });
        }

        await using var conn = await _db.CreateConnectionAsync();

        const string sql = """
            SELECT
                nd.id AS Id,
                nd.ma_nguoi_dung AS MaNguoiDung,
                nd.ten_dang_nhap AS TenDangNhap,
                nd.mat_khau AS MatKhau,
                nd.ho_ten AS HoTen,
                nd.so_dien_thoai AS SoDienThoai,
                nd.email AS Email,
                nd.chi_nhanh_id AS ChiNhanhId,
                cn.ten_chi_nhanh AS TenChiNhanh,
                nd.trang_thai AS TrangThai,
                GROUP_CONCAT(vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro,
                GROUP_CONCAT(vt.ma_vai_tro ORDER BY vt.id SEPARATOR ',') AS MaVaiTro
            FROM nguoi_dung nd
            LEFT JOIN chi_nhanh cn ON cn.id = nd.chi_nhanh_id
            LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
            LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
            WHERE nd.ten_dang_nhap = @TenDangNhap
            GROUP BY nd.id, nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.mat_khau, nd.ho_ten,
                     nd.so_dien_thoai, nd.email, nd.chi_nhanh_id, nd.trang_thai, cn.ten_chi_nhanh
            LIMIT 1;
            """;

        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(sql, new { TenDangNhap = request.TenDangNhap.Trim() });
        if (row is null)
        {
            return Unauthorized(new { Message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
        }

        bool trangThai = true;
        if (row.TrangThai != null)
        {
            var t = row.TrangThai;
            if (t is bool b) trangThai = b;
            else if (int.TryParse(t.ToString(), out int iv)) trangThai = iv == 1;
            else if (bool.TryParse(t.ToString(), out bool bv)) trangThai = bv;
            else trangThai = t.ToString() == "1";
        }

        if (!trangThai)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });
        }

        string dbPassword = row.MatKhau ?? string.Empty;
        bool validPassword;
        if (dbPassword.StartsWith("$2a$") || dbPassword.StartsWith("$2b$") || dbPassword.StartsWith("$2y$"))
        {
            validPassword = BCrypt.Net.BCrypt.Verify(request.MatKhau, dbPassword);
        }
        else
        {
            validPassword = string.Equals(request.MatKhau, dbPassword, StringComparison.Ordinal);
        }

        if (!validPassword)
        {
            return Unauthorized(new { Message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
        }

        var user = new UserDto
        {
            Id = (uint)row.Id,
            MaNguoiDung = row.MaNguoiDung ?? string.Empty,
            TenDangNhap = row.TenDangNhap ?? string.Empty,
            HoTen = row.HoTen ?? string.Empty,
            SoDienThoai = row.SoDienThoai,
            Email = row.Email,
            ChiNhanhId = row.ChiNhanhId != null ? (uint)row.ChiNhanhId : null,
            TenChiNhanh = row.TenChiNhanh,
            VaiTro = row.VaiTro,
            MaVaiTro = row.MaVaiTro,
            TrangThai = trangThai
        };

        // Lấy danh sách quyền chi tiết từ bảng vai_tro_quyen / nguoi_dung_quyen
        var permissions = new List<string>();
        try
        {
            const string permSql = """
                SELECT DISTINCT q.ma_quyen
                FROM quyen q
                JOIN vai_tro_quyen vtq ON vtq.quyen_id = q.id
                JOIN nguoi_dung_vai_tro ndvt ON ndvt.vai_tro_id = vtq.vai_tro_id
                WHERE ndvt.nguoi_dung_id = @UserId;
                """;
            permissions = (await conn.QueryAsync<string>(permSql, new { UserId = user.Id })).AsList();
        }
        catch
        {
            // Bảng quyền có thể chưa có hoặc đang dùng vai trò
        }

        // Cập nhật lần đăng nhập cuối
        try
        {
            await conn.ExecuteAsync(
                "UPDATE nguoi_dung SET lan_dang_nhap_cuoi = NOW(), ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = user.Id });
        }
        catch { }

        var (token, expiration) = _jwt.GenerateToken(user, permissions);

        return Ok(new LoginResponse
        {
            Token = token,
            Expiration = expiration,
            User = user,
            Permissions = permissions
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!uint.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        await using var conn = await _db.CreateConnectionAsync();
        const string sql = """
            SELECT
                nd.id AS Id,
                nd.ma_nguoi_dung AS MaNguoiDung,
                nd.ten_dang_nhap AS TenDangNhap,
                nd.ho_ten AS HoTen,
                nd.so_dien_thoai AS SoDienThoai,
                nd.email AS Email,
                nd.chi_nhanh_id AS ChiNhanhId,
                cn.ten_chi_nhanh AS TenChiNhanh,
                nd.trang_thai AS TrangThai,
                GROUP_CONCAT(vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro,
                GROUP_CONCAT(vt.ma_vai_tro ORDER BY vt.id SEPARATOR ',') AS MaVaiTro
            FROM nguoi_dung nd
            LEFT JOIN chi_nhanh cn ON cn.id = nd.chi_nhanh_id
            LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
            LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
            WHERE nd.id = @Id
            GROUP BY nd.id, nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten,
                     nd.so_dien_thoai, nd.email, nd.chi_nhanh_id, nd.trang_thai, cn.ten_chi_nhanh
            LIMIT 1;
            """;

        var user = await conn.QuerySingleOrDefaultAsync<UserDto>(sql, new { Id = userId });
        if (user is null)
        {
            return NotFound(new { Message = "Không tìm thấy người dùng." });
        }

        return Ok(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!uint.TryParse(userIdStr, out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
            return BadRequest(new { Message = "Mật khẩu mới phải có ít nhất 4 ký tự." });

        await using var conn = await _db.CreateConnectionAsync();
        var currentPwd = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT mat_khau FROM nguoi_dung WHERE id = @Id;", new { Id = userId });

        if (!string.IsNullOrEmpty(request.OldPassword))
        {
            bool match = (currentPwd?.StartsWith("$2a$") == true || currentPwd?.StartsWith("$2b$") == true)
                ? BCrypt.Net.BCrypt.Verify(request.OldPassword, currentPwd)
                : string.Equals(request.OldPassword, currentPwd, StringComparison.Ordinal);

            if (!match)
                return BadRequest(new { Message = "Mật khẩu cũ không chính xác." });
        }

        var hashed = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await conn.ExecuteAsync(
            "UPDATE nguoi_dung SET mat_khau = @Password, ngay_cap_nhat = NOW() WHERE id = @Id;",
            new { Password = hashed, Id = userId });

        return Ok(new { Message = "Đổi mật khẩu thành công." });
    }
}
