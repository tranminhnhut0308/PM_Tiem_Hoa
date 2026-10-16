namespace ShopHoa.Models;

/// <summary>
/// Số liệu tổng quan cho thanh thống kê phía trên bảng danh sách nhà cung cấp.
///
/// Cả ba chỉ số đều tính thẳng từ database theo đúng bộ lọc đang áp dụng,
/// nên thẻ thống kê và danh sách luôn nói về cùng một tập nhà cung cấp.
/// </summary>
public sealed class ThongKeDanhSachNhaCungCap
{
    /// <summary>Số nhà cung cấp khớp điều kiện lọc.</summary>
    public int TongSoNhaCungCap { get; set; }

    /// <summary>Tổng công nợ còn lại của các nhà cung cấp khớp điều kiện lọc.</summary>
    public decimal TongCongNo { get; set; }

    /// <summary>Số nhà cung cấp đang có nợ.</summary>
    public int SoNhaCungCapConNo { get; set; }

    /// <summary>Tổng tiền đã mua của các nhà cung cấp khớp điều kiện lọc.</summary>
    public decimal TongMua { get; set; }

    /// <summary>Tổng tiền đã mua trừ trả hàng của các nhà cung cấp khớp điều kiện lọc.</summary>
    public decimal TongMuaTruTraHang { get; set; }
}
