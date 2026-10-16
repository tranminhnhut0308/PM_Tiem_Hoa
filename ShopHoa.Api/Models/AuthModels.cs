namespace ShopHoa.Api.Models;

public sealed class LoginRequest
{
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public UserDto User { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public sealed class UserDto
{
    public uint Id { get; set; }
    public string MaNguoiDung { get; set; } = string.Empty;
    public string TenDangNhap { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public uint? ChiNhanhId { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? VaiTro { get; set; }
    public string? MaVaiTro { get; set; }
    public bool TrangThai { get; set; } = true;
}

public sealed class ChangePasswordRequest
{
    public string? OldPassword { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
