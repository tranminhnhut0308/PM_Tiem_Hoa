namespace ShopHoa.Models;

/// <summary>
/// Kết quả người dùng nhập ở hộp thoại thu công nợ (của một hóa đơn hoặc của
/// một khách hàng). Trả về null khi người dùng hủy.
/// </summary>
public sealed class ThuNoHoaDonKetQua
{
    /// <summary>Số tiền khách trả thêm cho hóa đơn.</summary>
    public decimal SoTien { get; init; }

    /// <summary>Mã phương thức thanh toán (TIEN_MAT, CHUYEN_KHOAN...).</summary>
    public string PhuongThuc { get; init; } = "TIEN_MAT";

    /// <summary>Ghi chú của lần thu nợ (không bắt buộc).</summary>
    public string? GhiChu { get; init; }
}
