using System;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Gợi ý phiếu nhập hàng gốc từ Nhà cung cấp khi lập phiếu xuất hàng trả lại.
/// Giúp người dùng tra cứu nhanh mã phiếu nhập, nhà cung cấp và tổng tiền nhập.
/// </summary>
public sealed class PhieuNhapGocGoiY
{
    public uint Id { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public DateTime ThoiGianNhap { get; set; }
    public uint? NhaCungCapId { get; set; }
    public string? TenNhaCungCap { get; set; }
    public string? MaNhaCungCap { get; set; }
    public string? SoDienThoaiNcc { get; set; }
    public decimal TongTienNhap { get; set; }

    public string ThoiGianNhapText =>
        ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TongTienNhapText =>
        TongTienNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string HienThi =>
        $"{MaPhieuNhap} - {ThoiGianNhapText} - {TenNhaCungCap ?? "NCC"} ({TongTienNhapText})";
}
