namespace ShopHoa.Models;

/// <summary>
/// Nhóm khách hàng, ánh xạ với bảng nhom_khach_hang.
/// Dùng cho danh sách chọn nhóm trong form thêm/sửa khách hàng.
/// </summary>
public sealed class NhomKhachHang
{
    public uint Id { get; set; }

    public string TenNhom { get; set; } = "";

    public decimal PhanTramGiamGia { get; set; }

    public bool TrangThai { get; set; } = true;

    public override string ToString() => TenNhom;
}
