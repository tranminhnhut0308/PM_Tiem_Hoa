using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Thông tin cửa hàng dùng chung cho mọi mẫu chứng từ (hóa đơn bán hàng,
/// phiếu nhập, báo cáo...).
/// <para>
/// Bảng <c>thong_tin_tiem</c> chỉ chứa MỘT bản ghi (id = 1). Người dùng sửa
/// thì UPDATE đúng bản ghi đó, không tạo bản ghi mới, nhờ vậy hóa đơn luôn đọc
/// được thông tin mới nhất mà không phải dò tìm "bản ghi tiệm nào là mới nhất".
/// </para>
/// <para>
/// Tên tiệm dùng để hiển thị; các dòng liên hệ gộp sẵn để hóa đơn chỉ cần bind
/// một biến, không tự ghép chuỗi ở tầng UI (dễ lệch chỗ hiển thị).
/// </para>
/// </summary>
public sealed partial class ThongTinTiem : ObservableObject
{
    /// <summary>Khoá của bản ghi thông tin tiệm. Luôn = 1 trong thiết kế này.</summary>
    public const uint IdMacDinh = 1;

    public string TenTiem { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string Email { get; set; } = "";
    public string MaSoThue { get; set; } = "";
    public string Website { get; set; } = "";
    public string DuongDanLogo { get; set; } = "";
    public string LoiCamOn { get; set; } = "";

    /// <summary>
    /// Logo đã nạp để hiển thị (không lưu xuống database). Null khi người dùng
    /// chưa chọn logo hoặc file đã bị mất.
    /// </summary>
    [ObservableProperty]
    private Bitmap? logoBitmap;

    /// <summary>File logo có dùng được để hiển thị không.</summary>
    public bool CoLogo => LogoBitmap is not null;

    /// <summary>
    /// Lời cảm ơn cuối hóa đơn. Người dùng xoá hết thì lấy mặc định thay vì
    /// để hóa đơn trống mất dòng kết.
    /// </summary>
    public string LoiCamOnHienThi =>
        string.IsNullOrWhiteSpace(LoiCamOn) ? "Cảm ơn quý khách!" : LoiCamOn.Trim();

    /// <summary>Số điện thoại kèm tiền tố "ĐT:" đúng như cách in ra hóa đơn.</summary>
    public string SoDienThoaiHienThi =>
        string.IsNullOrWhiteSpace(SoDienThoai) ? "" : $"ĐT: {SoDienThoai.Trim()}";

    /// <summary>Mã số thuế kèm nhãn, rỗng nếu người dùng chưa nhập.</summary>
    public string MaSoThueHienThi =>
        string.IsNullOrWhiteSpace(MaSoThue) ? "" : $"MST: {MaSoThue.Trim()}";

    /// <summary>Website kèm nhãn, rỗng nếu người dùng chưa nhập.</summary>
    public string WebsiteHienThi =>
        string.IsNullOrWhiteSpace(Website) ? "" : Website.Trim();

    /// <summary>Có ít nhất một dòng thông tin liên hệ để in ra hóa đơn không.</summary>
    public bool CoThongTinLienHe =>
        !string.IsNullOrWhiteSpace(DiaChi)
        || !string.IsNullOrWhiteSpace(SoDienThoai)
        || !string.IsNullOrWhiteSpace(Email)
        || !string.IsNullOrWhiteSpace(Website)
        || !string.IsNullOrWhiteSpace(MaSoThue);

    partial void OnLogoBitmapChanged(Bitmap? value) => OnPropertyChanged(nameof(CoLogo));
}