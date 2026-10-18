using System;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Gợi ý hóa đơn gốc khi lập phiếu trả lại hàng.
/// </summary>
public sealed class HoaDonGocGoiY
{
    public uint Id { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGianBan { get; set; }
    public uint? KhachHangId { get; set; }
    public string? TenKhachHang { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }

    public string ThoiGianBanText =>
        ThoiGianBan.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string KhachCanTraText =>
        KhachCanTra.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string HienThi =>
        $"{MaHoaDon} - {ThoiGianBanText} - {TenKhachHang ?? "Khách lẻ"} ({KhachCanTraText})";
}
