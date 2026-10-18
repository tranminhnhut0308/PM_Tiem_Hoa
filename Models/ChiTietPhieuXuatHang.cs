using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Chi tiết dòng hàng trong phiếu xuất kho.
/// </summary>
public partial class ChiTietPhieuXuatHang : ObservableObject
{
    public SanPham SanPham { get; set; } = new();

    /// <summary>STT hiển thị trên bảng, tự động đánh số lại khi thêm/xóa dòng.</summary>
    [ObservableProperty]
    private int index;

    /// <summary>Số lượng xuất hàng, người dùng có thể nhập hoặc bấm tăng/giảm.</summary>
    [ObservableProperty]
    private decimal soLuong = 1;

    /// <summary>Đơn giá xuất (mặc định lấy theo giá vốn hoặc giá xuất tùy loại phiếu).</summary>
    [ObservableProperty]
    private decimal donGia;

    /// <summary>Số lượng tồn kho khả dụng hiện tại tại kho xuất.</summary>
    [ObservableProperty]
    private decimal tonKhoHienTai;

    [ObservableProperty]
    private string? ghiChu;

    public string MaSanPham => SanPham.MaSanPham;

    public string TenSanPham => SanPham.TenSanPham;

    public string? DonViTinh => SanPham.DonViTinh;

    public decimal ThanhTien => Math.Max(0, SoLuong * DonGia);

    public string ThanhTienText => ThanhTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string DonGiaText => DonGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string TonKhoText => TonKhoHienTai.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Cảnh báo khi số lượng xuất vượt quá số lượng tồn kho khả dụng.</summary>
    public bool VuotTonKho => SoLuong > TonKhoHienTai;

    public ChiTietPhieuXuatHang()
    {
    }

    public ChiTietPhieuXuatHang(SanPham product, decimal tonKho)
    {
        SanPham = product;
        TonKhoHienTai = tonKho;
        DonGia = product.GiaVon > 0 ? product.GiaVon : product.GiaBan;
        SoLuong = 1;
    }

    partial void OnSoLuongChanged(decimal value)
    {
        var safeValue = value < 0 ? 0 : value;
        if (safeValue != value)
            SoLuong = safeValue;

        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(ThanhTienText));
        OnPropertyChanged(nameof(VuotTonKho));
    }

    partial void OnDonGiaChanged(decimal value)
    {
        if (value < 0)
            DonGia = 0;

        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(ThanhTienText));
        OnPropertyChanged(nameof(DonGiaText));
    }

    partial void OnTonKhoHienTaiChanged(decimal value)
    {
        OnPropertyChanged(nameof(TonKhoText));
        OnPropertyChanged(nameof(VuotTonKho));
    }
}
