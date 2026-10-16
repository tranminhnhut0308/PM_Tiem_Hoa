using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ShopHoa.Models;

namespace ShopHoa.Views;

/// <summary>
/// Báo cáo kết quả nhập hàng hóa: tổng số dòng Excel, số thêm mới, số thay thế,
/// số giữ cũ, số phát hiện trùng, số dòng lỗi và chi tiết từng dòng lỗi.
/// </summary>
public partial class KetQuaImportWindow : Window
{
    private KetQuaImportWindow(KetQuaImportSanPham ketQua)
    {
        InitializeComponent();

        Title = "Kết quả nhập hàng hóa";
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        TenFileTextBlock.Text = $"File: {ketQua.TenFile}";
        TomTatTextBlock.Text = ketQua.ThongBaoTatCa;

        CanhBaoItems.ItemsSource = ketQua.CanhBao;
        CanhBaoBorder.IsVisible = ketQua.CanhBao.Count > 0;

        LoiItems.ItemsSource = ketQua.LoiDong;
        LoiBorder.IsVisible = ketQua.LoiDong.Count > 0;

        LoiTieudeTextBlock.Text = $"Chi tiết dòng lỗi ({ketQua.LoiDong.Count:N0} dòng):";
    }

    public static Task ShowAsync(Window owner, KetQuaImportSanPham ketQua)
    {
        var dialog = new KetQuaImportWindow(ketQua);
        return dialog.ShowDialog(owner);
    }

    private void DongButton_OnClick(object? sender, RoutedEventArgs e)
        => Close();
}