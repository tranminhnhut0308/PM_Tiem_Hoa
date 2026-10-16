using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Một dòng trong bảng danh sách khách hàng.
///
/// Mỗi dòng tự quản lý trạng thái "đang mở rộng" của mình nên vùng chi tiết
/// nằm ngay dưới chính dòng đó, giống kiểu expandable row của KiotViet.
/// Chỉ một dòng được mở tại một thời điểm (do MainWindowViewModel điều phối).
/// </summary>
public sealed partial class KhachHangRowViewModel : ObservableObject
{
    /// <summary>Giá trị hiển thị khi một trường dữ liệu chưa có.</summary>
    public const string ChuaCo = "Chưa có";

    private static readonly IBrush ChuCoNo = new SolidColorBrush(Color.Parse("#D64545"));
    private static readonly IBrush ChuKhongCoNo = new SolidColorBrush(Color.Parse("#163F38"));

    public KhachHang Customer { get; private set; }

    [ObservableProperty] private bool isExpanded;

    /// <summary>Ô tick chọn khách hàng này, độc lập với việc mở xem chi tiết.</summary>
    [ObservableProperty] private bool isSelected;

    /// <summary>Vùng chi tiết của dòng (null khi dòng đang đóng).</summary>
    [ObservableProperty]
    private KhachHangDetailViewModel? detail;

    /// <summary>
    /// ViewModel chính, dùng để chuyển các thao tác
    /// "Sửa" / "Xóa" của dòng về đúng chỗ xử lý nghiệp vụ.
    /// </summary>
    private readonly MainWindowViewModel? _chuSo;

    public KhachHangRowViewModel(
        KhachHang customer,
        MainWindowViewModel? chuSo = null)
    {
        Customer = customer;
        _chuSo = chuSo;
    }

    /// <summary>Mở form sửa khách hàng của dòng này.</summary>
    [RelayCommand]
    private void Sua()
        => _chuSo?.SuaKhachHangCommand.Execute(this);

    /// <summary>Xóa khách hàng của dòng này (có xác nhận trước).</summary>
    [RelayCommand]
    private void Xoa()
        => _chuSo?.XoaKhachHangCommand.Execute(this);

    public uint CustomerId => Customer.Id;

    public string MaKhachHang => HienThi(Customer.MaKhachHang);

    public string TenKhachHang => HienThi(Customer.TenKhachHang);

    public string SoDienThoai => HienThi(Customer.SoDienThoai);

    public string Email => HienThi(Customer.Email);

    public string DiaChi => HienThi(Customer.DiaChi);

    public string NhomKhachHang => HienThi(Customer.NhomKhachHang);

    public string NgayTaoText => Customer.NgayTao?.ToString("dd/MM/yyyy") ?? ChuaCo;

    /// <summary>Nợ hiện tại, lấy từ sổ cái công nợ (hoặc tổng nợ trên hóa đơn).</summary>
    public decimal NoHienTai => Customer.NoHienTai;

    /// <summary>Khách hàng đang còn nợ hay không (nợ hiện tại lớn hơn 0).</summary>
    public bool CoNo => Customer.NoHienTai > 0;

    /// <summary>Màu chữ ô nợ: 0 đ hiển thị bình thường, còn nợ thì tô đỏ cảnh báo.</summary>
    public IBrush NoHienTaiForeground => CoNo ? ChuCoNo : ChuKhongCoNo;

    /// <summary>Tổng tiền bán của khách hàng.</summary>
    public decimal TongBan => Customer.TongBan;

    /// <summary>Tổng tiền bán đã trừ các phiếu trả hàng.</summary>
    public decimal TongBanTruTraHang => Customer.TongBanTruTraHang;

    public string NoHienTaiText => FormatTien(Customer.NoHienTai);

    public string TongBanText => FormatTien(Customer.TongBan);

    public string TongBanTruTraHangText => FormatTien(Customer.TongBanTruTraHang);

    /// <summary>Chữ cái đầu để dựng vòng tròn avatar nhỏ ở cột tên.</summary>
    public string ChuCaiDau
    {
        get
        {
            var ten = (Customer.TenKhachHang ?? "").Trim();

            if (ten.Length == 0)
                return "?";

            return ten[..1].ToUpperInvariant();
        }
    }

    /// <summary>Ký hiệu mũi tên báo trạng thái mở/đóng chi tiết.</summary>
    public string KyHieuMoRong => IsExpanded ? "▲" : "▼";

    partial void OnIsExpandedChanged(bool value)
        => OnPropertyChanged(nameof(KyHieuMoRong));

    /// <summary>
    /// Nạp lại dữ liệu dòng sau khi vừa lưu xong, chỉ cập nhật đúng dòng này
    /// thay vì tải lại toàn bộ danh sách.
    /// </summary>
    public void NapLai(KhachHang customer)
    {
        // Bản ghi lấy lại từ form thêm/sửa không kèm số tiền tổng hợp,
        // nên giữ lại số tiền đang hiển thị cho tới lần tải danh sách kế tiếp.
        customer.NoHienTai = Customer.NoHienTai;
        customer.TongBan = Customer.TongBan;
        customer.TongBanTruTraHang = Customer.TongBanTruTraHang;

        Customer = customer;

        OnPropertyChanged(nameof(Customer));
        OnPropertyChanged(nameof(CustomerId));
        OnPropertyChanged(nameof(MaKhachHang));
        OnPropertyChanged(nameof(TenKhachHang));
        OnPropertyChanged(nameof(SoDienThoai));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(DiaChi));
        OnPropertyChanged(nameof(NhomKhachHang));
        OnPropertyChanged(nameof(NgayTaoText));
        OnPropertyChanged(nameof(ChuCaiDau));
        OnPropertyChanged(nameof(NoHienTai));
        OnPropertyChanged(nameof(CoNo));
        OnPropertyChanged(nameof(NoHienTaiForeground));
        OnPropertyChanged(nameof(TongBan));
        OnPropertyChanged(nameof(TongBanTruTraHang));
        OnPropertyChanged(nameof(NoHienTaiText));
        OnPropertyChanged(nameof(TongBanText));
        OnPropertyChanged(nameof(TongBanTruTraHangText));
    }

    private static string HienThi(string? value)
        => string.IsNullOrWhiteSpace(value) ? ChuaCo : value.Trim();

    /// <summary>Định dạng tiền kiểu Việt Nam, ví dụ 1.442.000 đ.</summary>
    private static string FormatTien(decimal value)
        => MoneyInput.Format(value) + " đ";
}
