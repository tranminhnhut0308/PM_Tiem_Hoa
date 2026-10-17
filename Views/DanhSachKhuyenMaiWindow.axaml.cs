using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class DanhSachKhuyenMaiWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly ObservableCollection<ChuongTrinhKhuyenMai> _items = new();
    private bool _daKhoiTao;

    public DanhSachKhuyenMaiWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _daKhoiTao = true;
            await TaiDanhSachAsync();
        };
    }

    private async Task TaiDanhSachAsync()
    {
        if (!_daKhoiTao)
            return;

        try
        {
            var search = (SearchTextBox.Text ?? "").Trim();
            var trangThai = LayTrangThaiDangChon();

            var list = await _db.GetDanhSachKhuyenMaiAsync(
                string.IsNullOrWhiteSpace(search) ? null : search,
                trangThai);

            _items.Clear();
            foreach (var item in list)
                _items.Add(item);

            DanhSachGrid.ItemsSource = _items;
            TongSoText.Text = _items.Count.ToString("N0");
            StatusText.Text = $"Hiển thị {_items.Count:N0} chương trình khuyến mãi.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách khuyến mãi", ex);
            StatusText.Text = "Không tải được danh sách: " + ex.Message;
        }
    }

    private string? LayTrangThaiDangChon()
    {
        if (TrangThaiComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is not null)
        {
            var tag = cbi.Tag.ToString();
            if (!string.IsNullOrWhiteSpace(tag) && tag != "TAT_CA")
                return tag;
        }

        return null;
    }

    private async void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        => await TaiDanhSachAsync();

    private async void TrangThaiComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        => await TaiDanhSachAsync();

    private async void ThemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var window = new ThemChuongTrinhKhuyenMaiWindow();
        await window.ShowDialog<bool>(this);
        await TaiDanhSachAsync();
    }

    private async void SuaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ChuongTrinhKhuyenMai item)
            return;

        var window = new ThemChuongTrinhKhuyenMaiWindow(item);
        await window.ShowDialog<bool>(this);
        await TaiDanhSachAsync();
    }

    private async void XoaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ChuongTrinhKhuyenMai item)
            return;

        var dialog = new ConfirmationWindow(
            "Xóa chương trình khuyến mãi",
            $"Bạn có chắc muốn xóa chương trình [{item.MaChuongTrinh}] {item.TenChuongTrinh}?",
            "Xóa",
            "Hủy");

        if (!await dialog.ShowDialog<bool>(this))
            return;

        try
        {
            await _db.XoaChuongTrinhKhuyenMaiAsync(item.Id);
            await TaiDanhSachAsync();
            StatusText.Text = $"Đã xóa chương trình {item.MaChuongTrinh}.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xóa chương trình khuyến mãi", ex);
            StatusText.Text = "Xóa thất bại: " + ex.Message;
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(true);
}
