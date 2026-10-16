using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopHoa.Api.Models;
using ShopHoa.Api.Services;

namespace ShopHoa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IDbConnectionFactory _db;

    public UsersController(IDbConnectionFactory db)
    {
        _db = db;
    }

    private bool IsAdmin() => User.IsInRole("QUAN_TRI") || User.HasClaim("Permission", "UsersView");

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? search = null)
    {
        if (!IsAdmin())
            return Forbid();

        await using var conn = await _db.CreateConnectionAsync();
        var keyword = (search ?? "").Trim();

        var sql = """
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
            WHERE (@Search = '' OR nd.ten_dang_nhap LIKE @Pattern OR nd.ho_ten LIKE @Pattern OR nd.so_dien_thoai LIKE @Pattern)
            GROUP BY nd.id, nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten,
                     nd.so_dien_thoai, nd.email, nd.chi_nhanh_id, nd.trang_thai, cn.ten_chi_nhanh
            ORDER BY nd.id DESC;
            """;

        var users = (await conn.QueryAsync<UserDto>(sql, new
        {
            Search = keyword,
            Pattern = $"%{keyword}%"
        })).AsList();

        return Ok(users);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetUserById(uint id)
    {
        if (!IsAdmin())
            return Forbid();

        await using var conn = await _db.CreateConnectionAsync();
        var sql = """
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
                     nd.so_dien_thoai, nd.email, nd.chi_nhanh_id, nd.trang_thai, cn.ten_chi_nhanh;
            """;

        var user = await conn.QuerySingleOrDefaultAsync<UserDto>(sql, new { Id = id });
        if (user is null)
            return NotFound(new { Message = "Không tìm thấy người dùng." });

        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!User.IsInRole("QUAN_TRI") && !User.HasClaim("Permission", "UsersCreate"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.TenDangNhap) || string.IsNullOrWhiteSpace(request.MatKhau))
            return BadRequest(new { Message = "Tên đăng nhập và mật khẩu không được để trống." });

        await using var conn = await _db.CreateConnectionAsync();

        // Kiểm tra trùng tên đăng nhập
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM nguoi_dung WHERE ten_dang_nhap = @TenDangNhap;",
            new { TenDangNhap = request.TenDangNhap.Trim() });

        if (exists > 0)
            return Conflict(new { Message = "Tên đăng nhập đã tồn tại trong hệ thống." });

        var hashed = BCrypt.Net.BCrypt.HashPassword(request.MatKhau);
        var maNguoiDung = "NV" + DateTime.Now.ToString("yyMMddHHmmss");

        const string insertSql = """
            INSERT INTO nguoi_dung
            (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten, so_dien_thoai, email, chi_nhanh_id, trang_thai, ngay_tao, ngay_cap_nhat)
            VALUES
            (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen, @SoDienThoai, @Email, @ChiNhanhId, @TrangThai, NOW(), NOW());
            SELECT LAST_INSERT_ID();
            """;

        var newId = await conn.ExecuteScalarAsync<uint>(insertSql, new
        {
            MaNguoiDung = maNguoiDung,
            TenDangNhap = request.TenDangNhap.Trim(),
            MatKhau = hashed,
            HoTen = request.HoTen.Trim(),
            SoDienThoai = request.SoDienThoai,
            Email = request.Email,
            ChiNhanhId = request.ChiNhanhId,
            TrangThai = request.TrangThai ? 1 : 0
        });

        // Gán vai trò
        if (request.VaiTroIds.Count > 0)
        {
            foreach (var roleId in request.VaiTroIds)
            {
                await conn.ExecuteAsync(
                    "INSERT IGNORE INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId);",
                    new { UserId = newId, RoleId = roleId });
            }
        }

        return CreatedAtAction(nameof(GetUserById), new { id = newId }, new { Id = newId, Message = "Tạo tài khoản thành công." });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateUser(uint id, [FromBody] UpdateUserRequest request)
    {
        if (!User.IsInRole("QUAN_TRI") && !User.HasClaim("Permission", "UsersEdit"))
            return Forbid();

        await using var conn = await _db.CreateConnectionAsync();

        const string updateSql = """
            UPDATE nguoi_dung
            SET
                ho_ten = @HoTen,
                so_dien_thoai = @SoDienThoai,
                email = @Email,
                chi_nhanh_id = @ChiNhanhId,
                trang_thai = @TrangThai,
                ngay_cap_nhat = NOW()
            WHERE id = @Id;
            """;

        var rows = await conn.ExecuteAsync(updateSql, new
        {
            HoTen = request.HoTen.Trim(),
            SoDienThoai = request.SoDienThoai,
            Email = request.Email,
            ChiNhanhId = request.ChiNhanhId,
            TrangThai = request.TrangThai ? 1 : 0,
            Id = id
        });

        if (rows == 0)
            return NotFound(new { Message = "Không tìm thấy người dùng." });

        // Cập nhật lại vai trò
        await conn.ExecuteAsync("DELETE FROM nguoi_dung_vai_tro WHERE nguoi_dung_id = @UserId;", new { UserId = id });
        foreach (var roleId in request.VaiTroIds)
        {
            await conn.ExecuteAsync(
                "INSERT INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId);",
                new { UserId = id, RoleId = roleId });
        }

        return Ok(new { Message = "Cập nhật tài khoản thành công." });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        await using var conn = await _db.CreateConnectionAsync();
        var roles = (await conn.QueryAsync<RoleDto>(
            "SELECT id AS Id, ma_vai_tro AS MaVaiTro, ten_vai_tro AS TenVaiTro, mo_ta AS MoTa FROM vai_tro ORDER BY id;"
        )).AsList();

        return Ok(roles);
    }
}
