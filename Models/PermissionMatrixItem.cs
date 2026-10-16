namespace ShopHoa.Models;

public sealed class PermissionMatrixItem
{
    public uint QuyenId { get; set; }
    public string MaQuyen { get; set; } = string.Empty;
    public string TenQuyen { get; set; } = string.Empty;
    public string Nhom { get; set; } = string.Empty;
    public string? Loai { get; set; }
    public bool Selected { get; set; }
    public bool FromRole { get; set; }
}
