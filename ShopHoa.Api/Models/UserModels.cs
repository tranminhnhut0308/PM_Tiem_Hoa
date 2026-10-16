namespace ShopHoa.Api.Models;

public sealed class CreateUserRequest
{
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public uint? ChiNhanhId { get; set; }
    public List<uint> VaiTroIds { get; set; } = new();
    public bool TrangThai { get; set; } = true;
}

public sealed class UpdateUserRequest
{
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public uint? ChiNhanhId { get; set; }
    public List<uint> VaiTroIds { get; set; } = new();
    public bool TrangThai { get; set; } = true;
}

public sealed class RoleDto
{
    public uint Id { get; set; }
    public string MaVaiTro { get; set; } = string.Empty;
    public string TenVaiTro { get; set; } = string.Empty;
    public string? MoTa { get; set; }
}

public sealed class PermissionCatalogItemDto
{
    public uint Id { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
