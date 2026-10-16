namespace ShopHoa.Models;

public sealed class UserEditDto
{
    public uint Id { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? MatKhau { get; set; } // null nếu không đổi
    public string? VaiTroText { get; set; }
    public string? MaVaiTro { get; set; }
    public bool TrangThai { get; set; } = true;
    public List<uint> RoleIds { get; set; } = new();
    public List<uint> PermissionIds { get; set; } = new();
}
