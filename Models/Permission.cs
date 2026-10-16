namespace ShopHoa.Models;

public sealed class Permission
{
    public uint Id { get; set; }
    public string MaQuyen { get; set; } = string.Empty;
    public string TenQuyen { get; set; } = string.Empty;
    public string Nhom { get; set; } = string.Empty;
    public string? Loai { get; set; }
    public string? MoTa { get; set; }
    public uint ThuTu { get; set; }
    public bool TrangThai { get; set; } = true;
}
