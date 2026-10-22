using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

/// <summary>
/// Hộp thoại chọn sản phẩm khi một mã vạch trỏ tới nhiều mặt hàng
/// (cùng mã vạch nhưng khác quy cách / đơn vị tính).
/// </summary>
public partial class ChonSanPhamBarcodeWindow : Window
{
    public ChonSanPhamBarcodeWindow(IReadOnlyList<SanPham> sanPhams)
    {
        InitializeComponent();

        DanhSachListBox.ItemsSource = sanPhams;

        if (sanPhams.Count > 0)
            DanhSachListBox.SelectedIndex = 0;
    }

    /// <summary>Tạo hộp thoại và hiển thị mã vạch đang quét trên tiêu đề.</summary>
    public static Task<SanPham?> ShowAsync(Window owner, IReadOnlyList<SanPham> sanPhams, string maVach)
    {
        var dialog = new ChonSanPhamBarcodeWindow(sanPhams);
        dialog.MaVachText.Text = $"Mã vạch: {maVach} — tìm thấy {sanPhams.Count} sản phẩm, vui lòng chọn:";
        return dialog.ShowDialog<SanPham?>(owner);
    }

    private void ChonClicked(object? sender, RoutedEventArgs e)
    {
        Close(DanhSachListBox.SelectedItem as SanPham);
    }

    private void HuyClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void DanhSachListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DanhSachListBox.SelectedItem is SanPham sanPham)
            Close(sanPham);
    }
}
