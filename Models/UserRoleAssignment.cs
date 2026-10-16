namespace ShopHoa.Models;

public sealed class UserRoleAssignment
{
    public uint UserId { get; set; }
    public List<uint> RoleIds { get; set; } = new();
}
