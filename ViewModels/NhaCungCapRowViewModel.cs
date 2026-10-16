using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Một dòng trong bảng danh sách nhà cung cấp.
///
/// Mỗi dòng tự quản lý trạng thái "đang mở rộng" của mình nên vùng chi tiết
/// nằm ngay dưới chính dòng đó, giống màn hình Khách hàng.
/// Chỉ một dòng được mở tại một thời điểm (do MainWindowViewModel điều phối).
/// </summary>
public sealed partial class NhaCungCapRowViewModel : ObservableObject
{
    /// <summary>Giá trị hiển thị khi một trường dữ liệu chưa có.</summary>
    public const string ChuaCo = "Chưa có";

    private static readonly IBrush NenCanhBao = new SolidColorBrush(Color.Parse("#FDF1E7"));
    private static readonly IBrush ChuCanhBao = new SolidColorBrush(Color.Parse("#B45309"));
    private static readonly IBrush ChuKhongConNo = new SolidColorBrush(Color.Parse("#52645E"));

    public NhaCungCap Supplier { get; private set; }

    [ObservableProperty] private bool isExpanded;

    /// <summary>Ô tick chọn nhà cung cấp này, độc lập với việc mở xem chi tiết.</summary>
    [ObservableProperty] private bool isSelected;

    /// <summary>Vùng chi tiết của dòng (null khi dòng đang đóng).</summary>
    [ObservableProperty]
    private NhaCungCapDetailViewModel? detail;

    /// <summary>
    /// ViewModel chính, dùng để chuyển thao tác "Sửa" / "Xóa" của dòng
    /// về đúng chỗ xử lý nghiệp vụ.
    /// </summary>
    private readonly MainWindowViewModel? _chuSo;

    public NhaCungCapRowViewModel(
        NhaCungCap supplier,
        MainWindowViewModel? chuSo = null)
    {
        Supplier = supplier;
        _chuSo = chuSo;
    }

    /// <summary>Mở form sửa nhà cung cấp của dòng này.</summary>
    [RelayCommand]
    private void Sua()
        => _chuSo?.SuaNhaCungCapCommand.Execute(Supplier);

    /// <summary>Xóa nhà cung cấp của dòng này (có xác nhận trước).</summary>
    [RelayCommand]
    private void Xoa()
        => _chuSo?.XoaNhaCungCapCommand.Execute(Supplier);

    public uint SupplierId => Supplier.Id;

    public string MaNhaCungCap => HienThi(Supplier.MaNhaCungCap);

    public string TenNhaCungCap => HienThi(Supplier.TenNhaCungCap);

    public string SoDienThoai => HienThi(Supplier.SoDienThoai);

    public string Email => HienThi(Supplier.Email);

    public string DiaChi => HienThi(Supplier.DiaChi);

    public string NgayTaoText => Supplier.NgayTao?.ToString("dd/MM/yyyy") ?? ChuaCo;

    /// <summary>Công nợ hiện tại, lấy từ sổ cái công nợ.</summary>
    public decimal ConNo => Supplier.ConNo;

    /// <summary>Công nợ đã định dạng kèm đơn vị "đ".</summary>
    public string ConNoText => MoneyInput.Format(Supplier.ConNo) + " đ";

    /// <summary>Tổng tiền đã mua của nhà cung cấp.</summary>
    public decimal TongMua => Supplier.TongMua;

    /// <summary>Tổng tiền đã mua trừ trả hàng của nhà cung cấp.</summary>
    public decimal TongMuaTruTraHang => Supplier.TongMuaTruTraHang;

    /// <summary>Tổng tiền đã mua đã định dạng kèm đơn vị "đ".</summary>
    public string TongMuaText => MoneyInput.Format(Supplier.TongMua) + " đ";

    /// <summary>Tổng tiền đã mua trừ trả hàng đã định dạng kèm đơn vị "đ".</summary>
    public string TongMuaTruTraHangText => MoneyInput.Format(Supplier.TongMuaTruTraHang) + " đ";

    /// <summary>Nhà cung cấp đang còn nợ hay không (dùng để tô màu cảnh báo).</summary>
    public bool CoConNo => Supplier.ConNo > 0;

    /// <summary>Màu nền của ô công nợ: chỉ tô nhạt khi còn nợ.</summary>
    public IBrush ConNoBackground =>
        CoConNo ? NenCanhBao : Brushes.Transparent;

    /// <summary>Màu chữ của ô công nợ: 0 đ hiển thị bình thường, có nợ thì nổi bật.</summary>
    public IBrush ConNoForeground =>
        CoConNo ? ChuCanhBao : ChuKhongConNo;

    /// <summary>Chữ cái đầu để dựng vòng tròn nhỏ ở cột tên.</summary>
    public string ChuCaiDau
    {
        get
        {
            var ten = (Supplier.TenNhaCungCap ?? "").Trim();

            if (ten.Length == 0)
                return "?";

            return ten[..1].ToUpperInvariant();
        }
    }

    /// <summary>Ký hiệu mũi tên báo trạng thái mở/đóng chi tiết.</summary>
    public string KyHieuMoRong => IsExpanded ? "▲" : "▼";

    partial void OnIsExpandedChanged(bool value)
        => OnPropertyChanged(nameof(KyHieuMoRong));

    private static string HienThi(string? value)
        => string.IsNullOrWhiteSpace(value) ? ChuaCo : value.Trim();
}
