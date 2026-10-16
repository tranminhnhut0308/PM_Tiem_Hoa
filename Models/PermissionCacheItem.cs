namespace ShopHoa.Models;

public sealed class PermissionCacheItem
{
    public string Key { get; set; } = string.Empty;
    public bool Allowed { get; set; }
}
