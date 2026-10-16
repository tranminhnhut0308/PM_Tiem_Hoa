namespace ShopHoa.Models;

public sealed class AuditLog
{
    public ulong Id { get; set; }
    public uint? NguoiDungId { get; set; }
    public string? TenDangNhap { get; set; }
    public string? HoTen { get; set; }
    public string? VaiTro { get; set; }
    public string HanhDong { get; set; } = string.Empty;
    public string? DoiTuong { get; set; }
    public uint? DoiTuongId { get; set; }
    public string? MaChungTu { get; set; }
    public string? NoiDung { get; set; }
    public decimal? GiaTri { get; set; }
    public string? DiaChiIp { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.Now;
}
