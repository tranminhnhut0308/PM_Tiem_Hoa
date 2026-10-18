using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Chi tiết mặt hàng trong phiếu trả hàng.
/// Ánh xạ với bảng chi_tiet_phieu_tra_hang.
/// </summary>
public sealed partial class ChiTietPhieuTraHang : ObservableObject
{
    public uint Id { get; set; }

    public int SoThuTu { get; set; }

    public uint PhieuTraId { get; set; }

    public uint SanPhamId { get; set; }

    public string MaSanPham { get; set; } = "";

    public string TenSanPham { get; set; } = "";

    public string DonViTinh { get; set; } = "";

    public decimal SoLuong { get; set; } = 1;

    public decimal DonGia { get; set; }

    public decimal ThanhTien { get; set; }

    public string? LyDoTra { get; set; }

    // ============================================================
    // THÔNG TIN HIỂN THỊ
    // ============================================================

    public string SoLuongText =>
        SoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string DonGiaText =>
        DonGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string ThanhTienText =>
        ThanhTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}
