namespace ShopHoa.Models;

/// <summary>
/// Kết quả sau khi thu công nợ của một khách hàng. Số tiền được phân bổ lần lượt
/// cho các hóa đơn còn nợ (cũ nhất trước), phần còn lại (nếu có) ghi vào sổ cái
/// công nợ của khách hàng.
/// </summary>
public sealed class ThuNoKhachHangKetQua
{
    /// <summary>Id khách hàng vừa được thu nợ.</summary>
    public uint KhachHangId { get; init; }

    /// <summary>Tên khách hàng (để hiển thị thông báo).</summary>
    public string TenKhachHang { get; init; } = "";

    /// <summary>Tổng số tiền thực thu được.</summary>
    public decimal TongDaThu { get; init; }

    /// <summary>Công nợ còn lại của khách hàng sau khi thu.</summary>
    public decimal ConNoConLai { get; init; }

    /// <summary>Số hóa đơn được cấn trừ nợ trong lần thu này.</summary>
    public int SoHoaDonDaThu { get; init; }

    /// <summary>Danh sách mã phiếu thu đã ghi vào sổ quỹ.</summary>
    public IReadOnlyList<string> MaPhieuThus { get; init; } = [];
}
