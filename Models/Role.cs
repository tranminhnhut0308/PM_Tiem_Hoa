namespace ShopHoa.Models;

public sealed class Role
{
    public uint Id { get; set; }
    public string MaVaiTro { get; set; } = string.Empty;
    public string TenVaiTro { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public bool TrangThai { get; set; } = true;
}
