using CommunityToolkit.Mvvm.ComponentModel;
using ShopHoa.Helpers;

namespace ShopHoa.Models;

public partial class SaleCartItem : ObservableObject
{
    public event Action? QuantityChanged;

    public required SanPham SanPham { get; set; }

    [ObservableProperty]
    private decimal soLuong = 1;

    /// <summary>
    /// Đơn giá bán riêng áp dụng cho khách hàng đang chọn (nếu có).
    /// Nếu null, sử dụng giá bán chuẩn SanPham.GiaBan.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DonGia))]
    [NotifyPropertyChangedFor(nameof(DonGiaText))]
    [NotifyPropertyChangedFor(nameof(GiaGocText))]
    [NotifyPropertyChangedFor(nameof(CoGiaRieng))]
    [NotifyPropertyChangedFor(nameof(ThanhTien))]
    private decimal? donGiaRieng;

    /// <summary>Đơn giá áp dụng thực tế (ưu tiên giá riêng nếu có).</summary>
    public decimal DonGia => DonGiaRieng ?? SanPham.GiaBan;

    /// <summary>Sản phẩm này có giá riêng cho khách hàng đang chọn.</summary>
    public bool CoGiaRieng => DonGiaRieng.HasValue && DonGiaRieng.Value != SanPham.GiaBan;

    public string DonGiaText => MoneyInput.Format(DonGia) + " đ";

    public string GiaGocText => MoneyInput.Format(SanPham.GiaBan) + " đ";

    public decimal ThanhTien => SoLuong * DonGia;

    /// <summary>
    /// Báo lại thành tiền khi giá bán của sản phẩm vừa được sửa ở màn hình khác.
    /// </summary>
    public void CapNhatThanhTien()
    {
        OnPropertyChanged(nameof(DonGia));
        OnPropertyChanged(nameof(DonGiaText));
        OnPropertyChanged(nameof(GiaGocText));
        OnPropertyChanged(nameof(CoGiaRieng));
        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnSoLuongChanged(decimal value)
    {
        var safeValue = value <= 0 ? 1 : value;
        if (safeValue != value)
            SoLuong = safeValue;

        OnPropertyChanged(nameof(ThanhTien));
        QuantityChanged?.Invoke();
    }
}
