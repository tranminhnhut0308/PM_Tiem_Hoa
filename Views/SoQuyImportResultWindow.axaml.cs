using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Báo kết quả nhập sổ quỹ: số phiếu thêm mới, cập nhật, bỏ qua, số dòng lỗi,
/// thời gian xử lý, cảnh báo và danh sách lỗi có thể xuất ra Excel.
/// </summary>
public partial class SoQuyImportResultWindow : Window
{
    private SoQuyImportResult? _ketQua;

    private Window? _chuSo;

    public SoQuyImportResultWindow()
    {
        InitializeComponent();
    }

    /// <summary>Kết quả nhập. Gán sau khi khởi tạo để cửa sổ tự dựng nội dung.</summary>
    public SoQuyImportResult? Data
    {
        get => _ketQua;
        set
        {
            _ketQua = value;

            HienThiNoiDung();
        }
    }

    private void HienThiNoiDung()
    {
        if (_ketQua is null)
            return;

        // Lưu lại cửa sổ cha để mở hộp thoại lưu danh sách lỗi.
        _chuSo = Owner as Window;

        Title = "Kết quả nhập sổ quỹ";

        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        TieudeTextBlock.Text = _ketQua.DaRollback
            ? "Nhập sổ quỹ thất bại"
            : _ketQua.DaHuy
                ? "Đã hủy nhập sổ quỹ"
                : "Nhập sổ quỹ thành công";

        TieudeTextBlock.Foreground = Brush.Parse(
            _ketQua.DaRollback ? "#A32020" : _ketQua.DaHuy ? "#8A5A00" : "#163F38");

        TieudeFileTextBlock.Text =
            $"File: {_ketQua.TenFile} — {_ketQua.TongSoDong:N0} dòng, "
            + $"xử lý dòng trùng: {_ketQua.TenXuLyTrungHienThi}.";

        ThemMoiTextBlock.Text = _ketQua.SoThemMoi.ToString("N0");
        CapNhatTextBlock.Text = _ketQua.SoCapNhat.ToString("N0");
        BoQuaTextBlock.Text = _ketQua.SoBoQua.ToString("N0");
        LoiTextBlock.Text = _ketQua.SoLoi.ToString("N0");
        ThoiGianTextBlock.Text = $"{_ketQua.ThoiGian.TotalSeconds:N1}s";

        TomTatTextBlock.Text = TaoTomTat(_ketQua);

        CanhBaoItems.ItemsSource = _ketQua.CanhBao.Select(c => (object)c).ToList();
        CanhBaoBorder.IsVisible = _ketQua.CanhBao.Count > 0;

        LoiItems.ItemsSource = _ketQua.DanhSachLoi.Select(l => (object)$"• {l.MoTa}").ToList();
        LoiBorder.IsVisible = _ketQua.DanhSachLoi.Count > 0;
        LoiTieudeTextBlock.Text =
            $"Danh sách lỗi ({_ketQua.DanhSachLoi.Count:N0} dòng), sửa file gốc rồi nhập lại:";

        XuatLoiButton.IsVisible = _ketQua.DanhSachLoi.Count > 0;
    }

    private static string TaoTomTat(SoQuyImportResult ketQua)
    {
        if (ketQua.DaHuy)
            return "Đã hủy nhập, database không bị thay đổi.";

        if (ketQua.DaRollback)
            return "Lỗi nghiêm trọng nên đã hủy toàn bộ thay đổi trong lượt nhập này.\n"
                   + "Database được giữ nguyên như trước khi nhập.";

        var dong = new List<string>
        {
            $"Đã ghi thành công {ketQua.TongSoGhiThanhCong:N0} phiếu "
            + $"(thêm mới {ketQua.SoThemMoi:N0}, cập nhật {ketQua.SoCapNhat:N0})."
        };

        if (ketQua.SoBoQua > 0)
            dong.Add($"Bỏ qua theo lựa chọn của bạn: {ketQua.SoBoQua:N0} phiếu.");

        if (ketQua.SoLoi > 0)
            dong.Add($"Dòng không nhập được vì lỗi: {ketQua.SoLoi:N0}. "
                     + "Các dòng này không thay đổi dữ liệu cũ.");

        dong.Add($"Thời gian xử lý: {ketQua.ThoiGian.TotalSeconds:N1} giây.");

        return string.Join("\n", dong);
    }

    private async void XuatLoiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_ketQua is null)
            return;

        try
        {
            var file = await (_chuSo ?? this).StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Xuất danh sách lỗi nhập sổ quỹ",
                    SuggestedFileName = TaoTenFileLoi(DateTime.Now),
                    DefaultExtension = "xlsx",

                    // Không tự động ghi đè file đang tồn tại.
                    ShowOverwritePrompt = true,

                    FileTypeChoices =
                    [
                        new FilePickerFileType("Excel")
                        {
                            Patterns = ["*.xlsx"]
                        }
                    ]
                });

            if (file is null)
                return;

            // ClosedXML dựng file trong bộ nhớ rồi trả về byte[], nên tự mở stream để ghi.
            var noiDung = SoQuyImportService.XuatDanhSachLoi(_ketQua.TenFile, _ketQua.DanhSachLoi);

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(noiDung);

            LoiGhiChuTextBlock.Text = $"Đã xuất danh sách lỗi ra \"{file.Name}\".";
            LoiGhiChuTextBlock.Foreground = Brush.Parse("#056B5D");
        }
        catch (Exception ex)
        {
            LoiGhiChuTextBlock.Text = "Không xuất được file lỗi: " + ex.Message;
            LoiGhiChuTextBlock.Foreground = Brush.Parse("#A32020");
        }
    }

    /// <summary>Tên file mặc định, ví dụ: DanhSachLoiNhapSoQuy_20261001_161500.xlsx</summary>
    private static string TaoTenFileLoi(DateTime thoiDiem)
        => $"DanhSachLoiNhapSoQuy_{thoiDiem:yyyyMMdd_HHmmss}.xlsx";

    private void DongButton_OnClick(object? sender, RoutedEventArgs e)
        => Close();
}