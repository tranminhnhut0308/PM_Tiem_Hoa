namespace ShopHoa.Models;

public sealed class MenuGroup
{
    public string Title { get; set; } = string.Empty;
    public List<MenuItemPermission> Items { get; set; } = new();
}

public sealed class MenuItemPermission
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public string Icon { get; set; } = string.Empty;
}
