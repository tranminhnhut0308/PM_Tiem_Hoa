using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng hàng hóa trong phiếu nhập hàng.
/// Số lượng và đơn giá nhập sửa trực tiếp được trên lưới,
/// thành tiền tự tính lại theo từng thay đổi.
/// </summary>
public partial class ChiTietPhieuNhapHang : ObservableObject
{
    public SanPham SanPham { get; set; } = new();

    /// <summary>Danh sách đơn vị tính dùng cho ComboBox chọn nhanh trên dòng hàng chưa có ĐVT.</summary>
    public ObservableCollection<ProductOption>? DonViTinhOptions { get; set; }

    /// <summary>STT hiển thị trên lưới, được ViewModel đánh số lại sau mỗi thay đổi.</summary>
    [ObservableProperty]
    private int index;

    [ObservableProperty]
    private decimal soLuong = 1;

    /// <summary>Giá nhập - nguồn sự thật dùng để tính Thành tiền và lưu database.</summary>
    [ObservableProperty]
    private decimal donGiaNhap;

    [ObservableProperty]
    private decimal giamGia;

    /// <summary>
    /// Cho phép chọn ĐVT trực tiếp trên dòng phiếu nhập khi hàng hóa ban đầu chưa có ĐVT.
    /// Giữ nguyên hiển thị ComboBox trong suốt quá trình lập phiếu để người dùng có thể đổi lại trước khi lưu.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoDonViTinhCoSan))]
    private bool choPhepChonDonViTinh;

    public bool CoDonViTinhCoSan => !ChoPhepChonDonViTinh;

    /// <summary>
    /// Đơn vị tính được người dùng chọn trực tiếp lúc nhập hàng (dành cho hàng chưa có ĐVT).
    /// Khi hoàn thành phiếu nhập, giá trị này sẽ được cập nhật thẳng vào sản phẩm trong danh mục hàng hóa.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DonViTinh))]
    [NotifyPropertyChangedFor(nameof(DonViTinhIdMoi))]
    [NotifyPropertyChangedFor(nameof(DaChonDonViTinhMoi))]
    private ProductOption? selectedDonViTinh;

    public uint? DonViTinhIdMoi => SelectedDonViTinh is { Id: > 0 } ? SelectedDonViTinh.Id : null;

    public bool DaChonDonViTinhMoi => SelectedDonViTinh is { Id: > 0 };

    public string MaSanPham => SanPham.MaSanPham;

    public string TenSanPham => SanPham.TenSanPham;

    public string? DonViTinh => !string.IsNullOrWhiteSpace(SelectedDonViTinh?.Name)
        ? SelectedDonViTinh.Name
        : SanPham.DonViTinh;

    public decimal ThanhTien => Math.Max(0, SoLuong * DonGiaNhap - GiamGia);

    public ChiTietPhieuNhapHang()
    {
    }

    public ChiTietPhieuNhapHang(SanPham product, ObservableCollection<ProductOption>? donViTinhOptions = null)
    {
        SanPham = product;
        DonGiaNhap = product.GiaVon;
        DonViTinhOptions = donViTinhOptions;
        ChoPhepChonDonViTinh = (product.DonViTinhId is null or 0)
                               && string.IsNullOrWhiteSpace(product.DonViTinh);
    }

    partial void OnSoLuongChanged(decimal value)
    {
        var safeValue = value < 0 ? 0 : value;
        if (safeValue != value)
            SoLuong = safeValue;

        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnDonGiaNhapChanged(decimal value)
    {
        if (value < 0)
            DonGiaNhap = 0;

        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnGiamGiaChanged(decimal value)
    {
        if (value < 0)
            GiamGia = 0;

        OnPropertyChanged(nameof(ThanhTien));
    }
}
