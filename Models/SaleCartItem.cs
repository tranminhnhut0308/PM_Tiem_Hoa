using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public partial class SaleCartItem : ObservableObject
{
    public event Action? QuantityChanged;

    public required SanPham SanPham { get; set; }

    [ObservableProperty]
    private decimal soLuong = 1;

    public decimal ThanhTien => SoLuong * SanPham.GiaBan;

    /// <summary>
    /// Báo lại thành tiền khi giá bán của sản phẩm vừa được sửa ở màn hình khác.
    /// Không có hàm này thì giỏ hàng sẽ giữ tổng tiền của giá cũ.
    /// </summary>
    public void CapNhatThanhTien()
        => OnPropertyChanged(nameof(ThanhTien));

    partial void OnSoLuongChanged(decimal value)
    {
        var safeValue = value <= 0 ? 1 : value;
        if (safeValue != value)
            SoLuong = safeValue;

        OnPropertyChanged(nameof(ThanhTien));
        QuantityChanged?.Invoke();
    }
}
