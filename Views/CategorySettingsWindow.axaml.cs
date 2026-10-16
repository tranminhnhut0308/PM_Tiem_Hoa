using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class CategorySettingsWindow : Window
{
    private readonly DatabaseService _dbService;

    public ObservableCollection<ProductOption> Groups { get; } = new();
    public ObservableCollection<ProductOption> Units { get; } = new();

    public CategorySettingsWindow()
    {
        InitializeComponent();
        _dbService = new DatabaseService();

        GroupsListBox.ItemsSource = Groups;
        UnitsListBox.ItemsSource = Units;

        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var groups = await _dbService.GetNhomSanPhamAsync();
            Groups.Clear();
            foreach (var item in groups)
            {
                Groups.Add(item);
            }

            var units = await _dbService.GetDonViTinhAsync();
            Units.Clear();
            foreach (var item in units)
            {
                Units.Add(item);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh mục cài đặt", ex);
        }
    }

    private async void AddGroup_OnClick(object? sender, RoutedEventArgs e)
    {
        var currentText = NewGroupNameTextBox.Text?.Trim();
        var tenNhom = await QuickOptionEditorWindow.ShowAsync(
            this,
            "Thêm nhóm hàng mới",
            "Tên nhóm hàng *",
            "Ví dụ: Hoa tươi, Hoa sáp, Hoa lụa, Phụ kiện...",
            initialValue: string.IsNullOrWhiteSpace(currentText) ? null : currentText);

        if (string.IsNullOrWhiteSpace(tenNhom))
            return;

        await ThemNhomMoiAsync(tenNhom);
    }

    private async void NewGroupNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var name = NewGroupNameTextBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                await ThemNhomMoiAsync(name);
            }
        }
    }

    private async Task ThemNhomMoiAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        try
        {
            // Kiểm tra xem nhóm đã có trong danh sách hiển thị chưa
            var existingInList = Groups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existingInList != null)
            {
                GroupsListBox.SelectedItem = existingInList;
                NewGroupNameTextBox.Text = string.Empty;
                await NotificationWindow.ShowWarningAsync($"Nhóm hàng \"{name}\" đã có trong danh mục.", owner: this);
                return;
            }

            ProductOption newGroup = await _dbService.ThemNhomSanPhamAsync(name);

            if (!Groups.Any(g => g.Id == newGroup.Id))
            {
                Groups.Add(newGroup);
            }

            GroupsListBox.SelectedItem = newGroup;
            NewGroupNameTextBox.Text = string.Empty;
            await NotificationWindow.ShowSuccessAsync($"Đã thêm nhóm hàng \"{name}\" thành công.", owner: this);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi thêm nhóm hàng", ex);
            await NotificationWindow.ShowErrorAsync("Không thể thêm nhóm hàng: " + ex.Message, owner: this);
        }
    }

    private async void DeleteGroup_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProductOption item)
        {
            try
            {
                await _dbService.XoaNhomSanPhamAsync(item.Id);
                Groups.Remove(item);
                await NotificationWindow.ShowSuccessAsync($"Đã xóa nhóm \"{item.Name}\".", owner: this);
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi xóa nhóm hàng", ex);
                await NotificationWindow.ShowErrorAsync("Không thể xóa nhóm hàng: " + ex.Message, owner: this);
            }
        }
    }

    private async void AddUnit_OnClick(object? sender, RoutedEventArgs e)
    {
        var currentText = NewUnitNameTextBox.Text?.Trim();
        var tenDvt = await QuickOptionEditorWindow.ShowAsync(
            this,
            "Thêm đơn vị tính mới",
            "Tên đơn vị tính *",
            "Ví dụ: Bó, Cành, Chậu, Giỏ, Lẵng, Bình...",
            initialValue: string.IsNullOrWhiteSpace(currentText) ? null : currentText);

        if (string.IsNullOrWhiteSpace(tenDvt))
            return;

        await ThemDonViTinhMoiAsync(tenDvt);
    }

    private async void NewUnitNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var name = NewUnitNameTextBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                await ThemDonViTinhMoiAsync(name);
            }
        }
    }

    private async Task ThemDonViTinhMoiAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        try
        {
            // Kiểm tra xem ĐVT đã có trong danh sách hiển thị chưa
            var existingInList = Units.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existingInList != null)
            {
                UnitsListBox.SelectedItem = existingInList;
                NewUnitNameTextBox.Text = string.Empty;
                await NotificationWindow.ShowWarningAsync($"Đơn vị tính \"{name}\" đã có trong danh mục.", owner: this);
                return;
            }

            var newOption = await _dbService.ThemDonViTinhAsync(name);

            if (!Units.Any(u => u.Id == newOption.Id))
            {
                Units.Add(newOption);
            }

            UnitsListBox.SelectedItem = newOption;
            NewUnitNameTextBox.Text = string.Empty;
            await NotificationWindow.ShowSuccessAsync($"Đã thêm đơn vị tính \"{name}\" thành công.", owner: this);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi thêm đơn vị tính", ex);
            await NotificationWindow.ShowErrorAsync("Không thể thêm đơn vị tính: " + ex.Message, owner: this);
        }
    }

    private async void DeleteUnit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProductOption item)
        {
            try
            {
                await _dbService.XoaDonViTinhAsync(item.Id);
                Units.Remove(item);
                await NotificationWindow.ShowSuccessAsync($"Đã xóa đơn vị tính \"{item.Name}\".", owner: this);
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi xóa đơn vị tính", ex);
                await NotificationWindow.ShowErrorAsync("Không thể xóa đơn vị tính: " + ex.Message, owner: this);
            }
        }
    }
}