namespace ShopHoa.Models;

public sealed class UserWithRoles
{
    public uint Id { get; set; }
    public string MaNguoiDung { get; set; } = string.Empty;
    public string TenDangNhap { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public bool TrangThai { get; set; }
    public string? VaiTro { get; set; }
    public string? MaVaiTro { get; set; }
    public List<uint> RoleIds { get; set; } = new();
}
