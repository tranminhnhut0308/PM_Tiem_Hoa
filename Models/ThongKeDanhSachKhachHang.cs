namespace ShopHoa.Models;

/// <summary>
/// Số liệu tổng quan của toàn bộ danh sách khách hàng, hiển thị ở thanh thống kê
/// phía trên bảng. Các số tiền được tính đúng theo cùng quy tắc với các cột
/// "Nợ hiện tại" và "Tổng bán" của bảng danh sách.
/// </summary>
public sealed class ThongKeDanhSachKhachHang
{
    /// <summary>Tổng số khách hàng đang hoạt động.</summary>
    public int TongSoKhachHang { get; set; }

    /// <summary>Số khách hàng được tạo trong khoảng số ngày gần nhất.</summary>
    public int SoKhachMoi { get; set; }

    /// <summary>Tổng nợ hiện tại của toàn bộ khách hàng.</summary>
    public decimal TongNoHienTai { get; set; }

    /// <summary>Tổng tiền bán của toàn bộ khách hàng.</summary>
    public decimal TongBan { get; set; }
}
