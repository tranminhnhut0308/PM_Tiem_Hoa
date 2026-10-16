using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ShopHoa.Models;

namespace ShopHoa.Views;

/// <summary>
/// Hộp thoại "Phát hiện sản phẩm trùng": hiển thị từng sản phẩm trùng kèm dữ liệu
/// cũ trong database và dữ liệu mới trong file Excel để người dùng đối chiếu,
/// rồi chọn giữ cũ / thay thế / hủy.
/// </summary>
public partial class SanPhamTrungWindow : Window
{
    private SanPhamTrungWindow(IReadOnlyList<SanPhamTrung> danhSachTrung)
    {
        InitializeComponent();

        Title = "Phát hiện sản phẩm trùng";
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        DuLieuItems.ItemsSource = danhSachTrung;
        DuLieuItems.MaxHeight = 420;
        SoDongTrungTextBlock.Text =
            $"Tổng cộng {danhSachTrung.Count:N0} sản phẩm trùng với dữ liệu đang có.";
    }

    /// <summary>
    /// Mở hộp thoại xử lý sản phẩm trùng. Trả về null khi không có sản phẩm trùng
    /// thì không cần hỏi, người gọi tự hiểu là thêm mới toàn bộ.
    /// </summary>
    public static Task<XuLySanPhamTrung?> ShowAsync(
        Window owner,
        IReadOnlyList<SanPhamTrung> danhSachTrung)
    {
        if (danhSachTrung.Count == 0)
            return Task.FromResult<XuLySanPhamTrung?>(null);

        var dialog = new SanPhamTrungWindow(danhSachTrung);
        return dialog.ShowDialog<XuLySanPhamTrung?>(owner);
    }

    private void GiuSanPhamCuButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuLySanPhamTrung.GiuSanPhamCu);

    private void ThayTheButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuLySanPhamTrung.ThayTheBangExcel);

    private void HuyButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuLySanPhamTrung.Huy);
}