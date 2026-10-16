using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho màn hình "Thông tin tiệm".
/// <para>
/// Màn này chỉ làm một việc: đọc và ghi bản ghi thông tin tiệm duy nhất qua
/// <see cref="StoreInfoService"/>. Không sao chép dữ liệu ra biến tĩnh, không tự
/// gọi database riêng — nhờ vậy hóa đơn và mọi mẫu in sau đó đọc đúng thông tin
/// vừa lưu, không cần làm gì thêm.
/// </para>
/// </summary>
public sealed partial class ThongTinTiemViewModel : ObservableObject
{
    private readonly StoreInfoService _service;

    public ThongTinTiemViewModel(StoreInfoService service)
    {
        _service = service;
    }

    /// <summary>Lấy cửa sổ chính để mở hộp thoại chọn logo (gán từ MainWindowViewModel).</summary>
    public Func<Window?> LayCuaSoChinh { get; set; } = static () => null;

    /// <summary>Cập nhật dòng trạng thái chung của cửa sổ chính.</summary>
    public Action<string>? CapNhatTrangThai { get; set; }

    /// <summary>Hiện hộp thoại thông báo (tiêu đề, nội dung) cho người dùng.</summary>
    public Func<string, string, Task>? HienThiHopNhat { get; set; }

    /// <summary>
    /// Người dùng có quyền sửa thông tin tiệm không (gán từ MainWindowViewModel).
    /// Khi false thì các ô nhập và nút lưu bị khoá, nhưng vẫn xem được.
    /// </summary>
    public Func<bool>? CoTheChinhSua { get; set; }

    private bool DuocPhepChinhSua => CoTheChinhSua?.Invoke() ?? true;

    // ============================================================
    // TRẠNG THÁI MÀN HÌNH
    // ============================================================

    /// <summary>Đang tải thông tin đã lưu từ database.</summary>
    [ObservableProperty]
    private bool dangTai = true;

    /// <summary>Đang ghi xuống database, khoá nút "Lưu" để tránh bấm hai lần.</summary>
    [ObservableProperty]
    private bool dangLuu;

    /// <summary>Thông báo lỗi hiển thị ngay trên màn hình (rỗng = không lỗi).</summary>
    [ObservableProperty]
    private string thongBaoLoi = "";

    /// <summary>Logo đã nạp để xem trước.</summary>
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? logoXemTruoc;

    /// <summary>Đường dẫn file logo sẽ lưu cùng bản ghi thông tin tiệm.</summary>
    [ObservableProperty]
    private string duongDanLogo = "";

    /// <summary>
    /// Người dùng hiện tại có quyền sửa không. Cập nhật mỗi lần mở màn nên
    /// khoá/ mở khoá các ô nhập và nút lưu theo quyền.
    /// </summary>
    [ObservableProperty]
    private bool canChinhSua = true;

    /// <summary>Có logo để hiển thị hay không (dùng cho IsVisible trong XAML).</summary>
    public bool CoLogoXemTruoc => LogoXemTruoc is not null;

    /// <summary>Tên file logo để người dùng biết đang dùng ảnh nào.</summary>
    public string TenLogoHienThi =>
        string.IsNullOrWhiteSpace(DuongDanLogo) ? "Chưa chọn logo" : Path.GetFileName(DuongDanLogo);

    // ============================================================
    // CÁC TRƯỜNG NHẬP
    // ============================================================

    [ObservableProperty] private string tenTiem = "";
    [ObservableProperty] private string diaChi = "";
    [ObservableProperty] private string soDienThoai = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string maSoThue = "";
    [ObservableProperty] private string website = "";
    [ObservableProperty] private string loiCamOn = "";

    // ============================================================
    // TẢI / LƯU
    // ============================================================

    /// <summary>
    /// Nạp thông tin tiệm đang lưu. Gọi mỗi lần người dùng mở lại màn hình
    /// nên thấy đúng dữ liệu mới nhất, kể cả sau khi đã lưu.
    /// </summary>
    [RelayCommand]
    private async Task TaiLaiAsync()
    {
        DangTai = true;
        ThongBaoLoi = "";

        try
        {
            var info = await _service.LayThongTinKemLogoAsync();

            TenTiem = info.TenTiem;
            DiaChi = info.DiaChi;
            SoDienThoai = info.SoDienThoai;
            Email = info.Email;
            MaSoThue = info.MaSoThue;
            Website = info.Website;
            LoiCamOn = info.LoiCamOn;

            DuongDanLogo = info.DuongDanLogo;
            LogoXemTruoc = info.LogoBitmap;

            CanChinhSua = DuocPhepChinhSua;

            CapNhatTrangThai?.Invoke(
                string.IsNullOrWhiteSpace(info.TenTiem)
                    ? "Chưa có thông tin tiệm, hãy nhập và bấm Lưu."
                    : $"Đã tải thông tin tiệm: {info.TenTiem}");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không tải được thông tin tiệm: " + ex.Message;
            SafeLog.WriteError("Loi tai thong tin tiem", ex);
        }
        finally
        {
            DangTai = false;
        }
    }

    /// <summary>
    /// Ghi thông tin tiệm xuống database. Không có bản ghi thì INSERT bản ghi
    /// đầu tiên, đã có thì UPDATE đúng bản ghi đó.
    /// </summary>
    [RelayCommand]
    private async Task LuuAsync()
    {
        if (DangLuu)
            return;

        if (!DuocPhepChinhSua)
        {
            ThongBaoLoi = "Bạn không có quyền sửa thông tin tiệm.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TenTiem))
        {
            ThongBaoLoi = "Tên tiệm không được để trống.";
            await HienThiHopNhatThongBaoAsync(
                "Thiếu tên tiệm",
                "Vui lòng nhập tên tiệm trước khi lưu.");
            return;
        }

        DangLuu = true;
        ThongBaoLoi = "";

        try
        {
            // Logo được chép vào thư mục dữ liệu ứng dụng trước khi ghi, để sau này
            // người dùng di chuyển/xoá file gốc thì hóa đơn vẫn có logo.
            var duongDanLogoDaLuu = string.IsNullOrWhiteSpace(DuongDanLogo)
                ? ""
                : await _service.LuuLogoAsync(DuongDanLogo);

            var info = new ThongTinTiem
            {
                TenTiem = TenTiem,
                DiaChi = DiaChi,
                SoDienThoai = SoDienThoai,
                Email = Email,
                MaSoThue = MaSoThue,
                Website = Website,
                LoiCamOn = LoiCamOn,
                DuongDanLogo = duongDanLogoDaLuu
            };

            await _service.LuuThongTinAsync(info);

            // Đọc lại để chắc chắn màn hình và hóa đơn cùng thấy một dữ liệu,
            // đồng thời nạp lại logo vừa lưu.
            var daLuu = await _service.LayThongTinKemLogoAsync();

            TenTiem = daLuu.TenTiem;
            DiaChi = daLuu.DiaChi;
            SoDienThoai = daLuu.SoDienThoai;
            Email = daLuu.Email;
            MaSoThue = daLuu.MaSoThue;
            Website = daLuu.Website;
            LoiCamOn = daLuu.LoiCamOn;
            DuongDanLogo = daLuu.DuongDanLogo;
            LogoXemTruoc = daLuu.LogoBitmap;

            CanChinhSua = DuocPhepChinhSua;

            const string noiDung = "Đã lưu thông tin tiệm";

            CapNhatTrangThai?.Invoke(noiDung);

            await HienThiHopNhatThongBaoAsync(
                "Lưu thành công",
                noiDung + ".\n\nHóa đơn từ lần này trở đi sẽ dùng tên, địa chỉ, "
                + "số điện thoại và logo vừa cập nhật.");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không lưu được thông tin tiệm: " + ex.Message;
            CapNhatTrangThai?.Invoke("❌ " + ThongBaoLoi);
            SafeLog.WriteError("Loi luu thong tin tiem", ex);

            await HienThiHopNhatThongBaoAsync(
                "Không lưu được thông tin tiệm",
                ex.Message);
        }
        finally
        {
            DangLuu = false;
        }
    }

    /// <summary>
    /// Chọn logo từ máy tính và xem trước ngay. File được chép vào thư mục dữ
    /// liệu ứng dụng ở bước Lưu, nên việc chọn trước không ghi gì xuống database.
    /// </summary>
    [RelayCommand]
    private async Task ChonLogoAsync()
    {
        if (!DuocPhepChinhSua)
            return;

        var owner = LayCuaSoChinh();

        if (owner is null)
            return;

        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Chọn logo cho tiệm",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Ảnh")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                    }
                ]
            });

        var file = files.FirstOrDefault();

        if (file is null)
        {
            CapNhatTrangThai?.Invoke("Đã hủy chọn logo.");
            return;
        }

        var duongDan = file.Path.LocalPath;

        if (string.IsNullOrWhiteSpace(duongDan) || !File.Exists(duongDan))
        {
            ThongBaoLoi = "Không đọc được file logo đã chọn.";
            return;
        }

        try
        {
            LogoXemTruoc = new Avalonia.Media.Imaging.Bitmap(duongDan);
            DuongDanLogo = duongDan;
            ThongBaoLoi = "";
            CapNhatTrangThai?.Invoke(
                $"Đã chọn logo {Path.GetFileName(duongDan)}. Bấm Lưu thông tin để áp dụng.");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "File logo không hợp lệ: " + ex.Message;
            SafeLog.WriteError("Loi xem truoc logo tiem", ex);
        }
    }

    /// <summary>Bỏ logo khỏi bản ghi (hóa đơn sẽ in không có logo).</summary>
    [RelayCommand]
    private void BoLogo()
    {
        LogoXemTruoc = null;
        DuongDanLogo = "";
        CapNhatTrangThai?.Invoke("Đã bỏ logo. Bấm Lưu thông tin để áp dụng.");
    }

    private async Task HienThiHopNhatThongBaoAsync(string tieuDe, string noiDung)
    {
        if (HienThiHopNhat is null)
            return;

        await HienThiHopNhat(tieuDe, noiDung);
    }

    partial void OnLogoXemTruocChanged(Avalonia.Media.Imaging.Bitmap? value)
    {
        OnPropertyChanged(nameof(CoLogoXemTruoc));
    }

    partial void OnDuongDanLogoChanged(string value)
    {
        OnPropertyChanged(nameof(TenLogoHienThi));
    }
}