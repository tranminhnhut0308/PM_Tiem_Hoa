namespace ShopHoa.Models;

public sealed class AccountStatus
{
    public uint UserId { get; set; }
    public bool IsActive { get; set; }
    public string StatusText { get; set; } = string.Empty;
}
