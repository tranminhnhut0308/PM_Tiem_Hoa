namespace ShopHoa.Models;

public sealed class RoleWithPermissions
{
    public uint RoleId { get; set; }
    public List<uint> PermissionIds { get; set; } = new();
}
