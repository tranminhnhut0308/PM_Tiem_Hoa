using System;
using System.Collections.Generic;
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

    /// <summary>Danh sách nhóm hàng đầy đủ (đã tải từ database).</summary>
    private List<ProductOption> _allGroups = new();

    /// <summary>Danh sách đơn vị tính đầy đủ (đã tải từ database).</summary>
    private List<ProductOption> _allUnits = new();

    /// <summary>Cờ chống thao tác chồng nhau (người dùng bấm nút nhiều lần).</summary>
    private bool _dangXuLy;

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

    /// <summary>
    /// Tải lại toàn bộ danh mục từ database rồi áp dụng bộ lọc tìm kiếm hiện tại.
    /// </summary>
    private async Task LoadDataAsync()
    {
        SetBusy(true);

        try
        {
            var groups = await _dbService.GetNhomSanPhamAsync();
            _allGroups = groups.ToList();

            var units = await _dbService.GetDonViTinhAsync();
            _allUnits = units.ToList();

            ApplyFilter();
            HideStatus(GroupsStatusTextBlock);
            HideStatus(UnitsStatusTextBlock);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh mục cài đặt", ex);

            ShowStatus(
                GroupsStatusTextBlock,
                "Không tải được danh mục: " + ex.Message);
            ShowStatus(
                UnitsStatusTextBlock,
                "Không tải được danh mục: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void AddGroup_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangXuLy)
            return;

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

        if (_dangXuLy) return;
        SetBusy(true);
        HideStatus(GroupsStatusTextBlock);

        try
        {
            // Kiểm tra trùng trên TOÀN BỘ danh mục (không phụ thuộc ô tìm kiếm
            // đang lọc), và cả trong database để không sinh bản ghi trùng.
            var existingInList = _allGroups.FirstOrDefault(g =>
                g.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existingInList != null)
            {
                GroupsListBox.SelectedItem = existingInList;
                NewGroupNameTextBox.Text = string.Empty;
                await NotificationWindow.ShowWarningAsync($"Nhóm hàng \"{name}\" đã có trong danh mục.", owner: this);
                return;
            }

            var saved = await _dbService.ThemNhomSanPhamAsync(name);

            NewGroupNameTextBox.Text = string.Empty;

            // Tự động tải lại danh sách từ database để dữ liệu chắc chắn đã lưu.
            await LoadDataAsync();

            var newGroup = _allGroups.FirstOrDefault(g => g.Id == saved.Id);
            if (newGroup != null)
            {
                GroupsListBox.SelectedItem = newGroup;
            }

            await NotificationWindow.ShowSuccessAsync($"Đã thêm nhóm hàng \"{name}\" thành công.", owner: this);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi thêm nhóm hàng", ex);
            ShowStatus(GroupsStatusTextBlock, "Không thể thêm nhóm hàng: " + ex.Message);
            await NotificationWindow.ShowErrorAsync("Không thể thêm nhóm hàng: " + ex.Message, owner: this);
        }
        finally
        {
            SetBusy(false);
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
                _allGroups.RemoveAll(g => g.Id == item.Id);
                CapNhatSoLuong();
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
        if (_dangXuLy)
            return;

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

        if (_dangXuLy) return;
        SetBusy(true);
        HideStatus(UnitsStatusTextBlock);

        try
        {
            // Kiểm tra trùng trên TOÀN BỘ danh mục (không phụ thuộc ô tìm kiếm
            // đang lọc), và cả trong database để không sinh bản ghi trùng.
            var existingInList = _allUnits.FirstOrDefault(u =>
                u.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existingInList != null)
            {
                UnitsListBox.SelectedItem = existingInList;
                NewUnitNameTextBox.Text = string.Empty;
                await NotificationWindow.ShowWarningAsync($"Đơn vị tính \"{name}\" đã có trong danh mục.", owner: this);
                return;
            }

            var saved = await _dbService.ThemDonViTinhAsync(name);

            NewUnitNameTextBox.Text = string.Empty;

            // Tự động tải lại danh sách từ database để dữ liệu chắc chắn đã lưu.
            await LoadDataAsync();

            var newUnit = _allUnits.FirstOrDefault(u => u.Id == saved.Id);
            if (newUnit != null)
            {
                UnitsListBox.SelectedItem = newUnit;
            }

            await NotificationWindow.ShowSuccessAsync($"Đã thêm đơn vị tính \"{name}\" thành công.", owner: this);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi thêm đơn vị tính", ex);
            ShowStatus(UnitsStatusTextBlock, "Không thể thêm đơn vị tính: " + ex.Message);
            await NotificationWindow.ShowErrorAsync("Không thể thêm đơn vị tính: " + ex.Message, owner: this);
        }
        finally
        {
            SetBusy(false);
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
                _allUnits.RemoveAll(u => u.Id == item.Id);
                CapNhatSoLuong();
                await NotificationWindow.ShowSuccessAsync($"Đã xóa đơn vị tính \"{item.Name}\".", owner: this);
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi xóa đơn vị tính", ex);
                await NotificationWindow.ShowErrorAsync("Không thể xóa đơn vị tính: " + ex.Message, owner: this);
            }
        }
    }

    private async void ReloadGroupsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async void ReloadUnitsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private void SearchGroupsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void SearchUnitsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Lọc danh sách hiển thị theo từ khóa tìm kiếm (không gọi lại database).
    /// </summary>
    private void ApplyFilter()
    {
        var tuKhoaNhom = SearchGroupsTextBox.Text?.Trim() ?? string.Empty;
        var danhSachNhom = string.IsNullOrEmpty(tuKhoaNhom)
            ? _allGroups
            : _allGroups
                .Where(g => g.Name.Contains(tuKhoaNhom, StringComparison.OrdinalIgnoreCase)
                            || g.Id.ToString().Contains(tuKhoaNhom, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Groups.Clear();
        foreach (var item in danhSachNhom)
        {
            Groups.Add(item);
        }

        var tuKhoaDvt = SearchUnitsTextBox.Text?.Trim() ?? string.Empty;
        var danhSachDvt = string.IsNullOrEmpty(tuKhoaDvt)
            ? _allUnits
            : _allUnits
                .Where(u => u.Name.Contains(tuKhoaDvt, StringComparison.OrdinalIgnoreCase)
                            || u.Id.ToString().Contains(tuKhoaDvt, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Units.Clear();
        foreach (var item in danhSachDvt)
        {
            Units.Add(item);
        }

        CapNhatSoLuong();
    }

    private void CapNhatSoLuong()
    {
        var tongNhom = _allGroups.Count;
        var hienNhom = Groups.Count;

        GroupsCountTextBlock.Text = tongNhom == hienNhom
            ? $"{tongNhom} nhóm hàng"
            : $"{hienNhom}/{tongNhom} nhóm hàng";

        var tongDvt = _allUnits.Count;
        var hienDvt = Units.Count;

        UnitsCountTextBlock.Text = tongDvt == hienDvt
            ? $"{tongDvt} đơn vị tính"
            : $"{hienDvt}/{tongDvt} đơn vị tính";
    }

    /// <summary>
    /// Khoá các nút thêm trong lúc đang xử lý để người dùng bấm nhiều lần
    /// không tạo ra bản ghi trùng.
    /// </summary>
    private void SetBusy(bool busy)
    {
        _dangXuLy = busy;

        AddGroupButton.IsEnabled = !busy;
        AddUnitButton.IsEnabled = !busy;
        ReloadGroupsButton.IsEnabled = !busy;
        ReloadUnitsButton.IsEnabled = !busy;
        NewGroupNameTextBox.IsEnabled = !busy;
        NewUnitNameTextBox.IsEnabled = !busy;
    }

    private static void ShowStatus(TextBlock target, string message)
    {
        target.Text = message;
        target.IsVisible = true;
    }

    private static void HideStatus(TextBlock target)
    {
        target.Text = string.Empty;
        target.IsVisible = false;
    }
}
