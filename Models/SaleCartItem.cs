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

    /// <summary>
    /// Dòng này là hàng TẶNG KÈM theo chương trình khuyến mãi.
    /// <para>
    /// Hàng tặng luôn có đơn giá 0 nên không tính vào doanh thu, nhưng vẫn được
    /// xuất kho và ghi vào chi tiết hóa đơn để thống kê được "đã tặng bao nhiêu".
    /// Người bán không được sửa số lượng hay xoá riêng dòng này — số lượng do
    /// chương trình khuyến mãi quyết định, muốn đổi thì sửa dòng hàng mua.
    /// </para>
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DonGia))]
    [NotifyPropertyChangedFor(nameof(DonGiaText))]
    [NotifyPropertyChangedFor(nameof(CoGiaRieng))]
    [NotifyPropertyChangedFor(nameof(ThanhTien))]
    [NotifyPropertyChangedFor(nameof(CanXoa))]
    [NotifyPropertyChangedFor(nameof(CanSuaSoLuong))]
    private bool isHangTang;

    /// <summary>Id chương trình khuyến mãi đã sinh ra dòng hàng tặng này.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChuongTrinhKhuyenMaiHienThi))]
    private uint? chuongTrinhKhuyenMaiId;

    /// <summary>Tên chương trình khuyến mãi (hiển thị trên hóa đơn và trong giỏ).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChuongTrinhKhuyenMaiHienThi))]
    private string? chuongTrinhKhuyenMaiTen;

    /// <summary>Id sản phẩm đã mua (dòng "cha") kích hoạt dòng hàng tặng này.</summary>
    [ObservableProperty]
    private uint? sanPhamIdMua;

    /// <summary>
    /// Số lượng sản phẩm đã mua kích hoạt dòng hàng tặng này (chỉ dòng hàng tặng).
    /// Lưu kèm vào chi tiết hóa đơn để đối chiếu điều kiện tặng.
    /// </summary>
    [ObservableProperty]
    private decimal soLuongMua;

    /// <summary>Ghi chú dòng hàng (ví dụ: "Hàng tặng kèm – Mua 1 Bó Tulip tặng 1 Lưu niệm").</summary>
    [ObservableProperty]
    private string? ghiChu;

    /// <summary>Đơn giá áp dụng thực tế (ưu tiên giá riêng nếu có; hàng tặng luôn bằng 0).</summary>
    public decimal DonGia => IsHangTang ? 0m : (DonGiaRieng ?? SanPham.GiaBan);

    /// <summary>Sản phẩm này có giá riêng cho khách hàng đang chọn.</summary>
    public bool CoGiaRieng => !IsHangTang && DonGiaRieng.HasValue && DonGiaRieng.Value != SanPham.GiaBan;

    public string DonGiaText => MoneyInput.Format(DonGia) + " đ";

    public string GiaGocText => MoneyInput.Format(SanPham.GiaBan) + " đ";

    public decimal ThanhTien => SoLuong * DonGia;

    /// <summary>Dòng hàng tặng không được xoá riêng (phải sửa dòng hàng mua).</summary>
    public bool CanXoa => !IsHangTang;

    /// <summary>Dòng hàng tặng không được sửa số lượng tay (do chương trình quyết định).</summary>
    public bool CanSuaSoLuong => !IsHangTang;

    /// <summary>Nhãn hiển thị trên dòng hàng tặng.</summary>
    public string NhanHangTang => "TẶNG KÈM";

    /// <summary>Tên chương trình khuyến mãi (rỗng nếu dòng không phải hàng tặng).</summary>
    public string ChuongTrinhKhuyenMaiHienThi =>
        string.IsNullOrWhiteSpace(ChuongTrinhKhuyenMaiTen)
            ? ""
            : ChuongTrinhKhuyenMaiTen!;

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
