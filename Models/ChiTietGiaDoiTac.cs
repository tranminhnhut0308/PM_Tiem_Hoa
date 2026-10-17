namespace ShopHoa.Models;

public sealed class ChiTietGiaDoiTac
{
    public uint Id { get; set; }
    public uint DoiTacId { get; set; }
    public uint HangHoaId { get; set; }
    public string MaHangHoa { get; set; } = "";
    public string TenHangHoa { get; set; } = "";
    public uint DonViTinhId { get; set; }
    public string DonViTinh { get; set; } = "";
    public decimal ChietKhau { get; set; }
    public decimal DonGia { get; set; }
    public decimal GiaBan => DonGia;
}
