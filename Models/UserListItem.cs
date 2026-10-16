namespace ShopHoa.Models;

public sealed class UserListItem
{
    public uint Id { get; set; }
    public string MaNguoiDung { get; set; } = string.Empty;
    public string TenDangNhap { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? VaiTro { get; set; }
    public string? MaVaiTro { get; set; }
    public bool TrangThai { get; set; }
    public string TrangThaiText => TrangThai ? "Hoạt động" : "Bị khóa";
    public DateTime? NgayTao { get; set; }
    public string? NguoiTao { get; set; }
    public DateTime? LanDangNhapCuoi { get; set; }
}
