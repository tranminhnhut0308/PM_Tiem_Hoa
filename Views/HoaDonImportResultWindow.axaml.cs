using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Báo cáo kết quả nhập hóa đơn: số thêm mới, số cập nhật, số bỏ qua, số khách
/// hàng tạo mới, thời gian chạy, cảnh báo và danh sách lỗi có thể xuất ra Excel.
/// </summary>
public partial class HoaDonImportResultWindow : Window
{
    private readonly HoaDonImportResult _ketQua;
    private readonly Window _owner;

    private HoaDonImportResultWindow(Window owner, HoaDonImportResult ketQua)
    {
        InitializeComponent();

        _ketQua = ketQua;
        _owner = owner;

        Title = "Kết quả nhập hóa đơn";
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        TenFileTextBlock.Text =
            $"File: {ketQua.TenFile} — {ketQua.TongSoHoaDon:N0} hóa đơn, "
            + $"{ketQua.TongSoDongChiTiet:N0} dòng chi tiết.";

        TomTatTextBlock.Text = TaoTomTat(ketQua);

        CanhBaoItems.ItemsSource = ketQua.CanhBao;
        CanhBaoBorder.IsVisible = ketQua.CanhBao.Count > 0;

        LoiItems.ItemsSource = ketQua.DanhSachLoi;
        LoiBorder.IsVisible = ketQua.DanhSachLoi.Count > 0;
        LoiTieudeTextBlock.Text =
            $"Danh sách lỗi ({ketQua.DanhSachLoi.Count:N0} dòng), sửa file gốc rồi nhập lại:";

        XuatLoiButton.IsVisible = ketQua.DanhSachLoi.Count > 0;
    }

    public static Task ShowAsync(Window owner, HoaDonImportResult ketQua)
    {
        var dialog = new HoaDonImportResultWindow(owner, ketQua);
        return dialog.ShowDialog(owner);
    }

    private static string TaoTomTat(HoaDonImportResult ketQua)
    {
        var dong = new List<string>();

        if (ketQua.DaHuy)
            return "Đã hủy nhập, database không bị thay đổi.";

        if (ketQua.DaRollback)
        {
            dong.Add("Lỗi nghiêm trọng nên đã hủy toàn bộ thay đổi trong lượt nhập này.");
            dong.Add("Database được giữ nguyên như trước khi nhập.");

            return string.Join("\n", dong);
        }

        dong.Add($"Thêm mới: {ketQua.SoThemMoi:N0} hóa đơn");
        dong.Add($"Cập nhật hóa đơn trùng mã: {ketQua.SoCapNhat:N0} hóa đơn");

        if (ketQua.SoBoQua > 0)
            dong.Add($"Bỏ qua theo lựa chọn của bạn: {ketQua.SoBoQua:N0} hóa đơn");

        if (ketQua.SoKhachHangTaoMoi > 0)
            dong.Add($"Khách hàng tạo mới: {ketQua.SoKhachHangTaoMoi:N0}");

        if (ketQua.SoLoi > 0)
            dong.Add($"Hóa đơn không nhập được vì lỗi: {ketQua.SoLoi:N0}");

        dong.Add($"Thời gian xử lý: {ketQua.ThoiGian.TotalSeconds:N1} giây");

        return string.Join("\n", dong);
    }

    private async void XuatLoiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await _owner.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Xuất danh sách lỗi nhập hóa đơn",
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

            // ClosedXML tự tạo và ghi đè file, không cần mở sẵn stream.
            HoaDonImportService.XuatDanhSachLoi(
                file.Path.LocalPath,
                _ketQua.TenFile,
                _ketQua.DanhSachLoi);

            LoiGhiChuTextBlock.Text = $"✅ Đã xuất danh sách lỗi ra \"{file.Name}\".";
            LoiGhiChuTextBlock.Foreground = Avalonia.Media.Brush.Parse("#056B5D");
        }
        catch (Exception ex)
        {
            LoiGhiChuTextBlock.Text = "❌ Không xuất được file lỗi: " + ex.Message;
            LoiGhiChuTextBlock.Foreground = Avalonia.Media.Brush.Parse("#A32020");
        }
    }

    /// <summary>Tên file mặc định, ví dụ DanhSachLoiNhapHoaDon_20261001_161500.xlsx</summary>
    public static string TaoTenFileLoi(DateTime thoiDiem)
        => $"DanhSachLoiNhapHoaDon_{thoiDiem:yyyyMMdd_HHmmss}.xlsx";

    private void DongButton_OnClick(object? sender, RoutedEventArgs e)
        => Close();
}
