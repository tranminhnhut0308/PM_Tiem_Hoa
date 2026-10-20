namespace ShopHoa.Models;

/// <summary>
/// Số liệu tổng quan của danh mục khách hàng, hiển thị ở thanh thống kê
/// phía trên bảng quản lý khách hàng.
/// </summary>
public sealed class ThongKeDanhSachKhachHang
{
    /// <summary>Tổng số khách hàng đang hoạt động.</summary>
    public int TongSoKhachHang { get; set; }

    /// <summary>Số khách hàng được tạo trong khoảng số ngày gần nhất.</summary>
    public int SoKhachMoi { get; set; }

    /// <summary>Số khách hàng cá nhân.</summary>
    public int SoKhachCaNhan { get; set; }

    /// <summary>Số khách hàng doanh nghiệp.</summary>
    public int SoKhachDoanhNghiep { get; set; }

    /// <summary>Tổng nợ hiện tại của toàn bộ khách hàng (phục vụ module công nợ).</summary>
    public decimal TongNoHienTai { get; set; }

    /// <summary>Tổng tiền bán của toàn bộ khách hàng.</summary>
    public decimal TongBan { get; set; }
}
