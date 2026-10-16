namespace ShopHoa.Models;

public sealed class UserPermissionsCache
{
    public uint UserId { get; set; }
    public HashSet<string> PermissionKeys { get; set; } = new();
    public List<string> PermissionList { get; set; } = new();
    public DateTime LoadedAt { get; set; } = DateTime.Now;
}
