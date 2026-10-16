using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// Cửa sổ phân quyền độc lập: cũng dùng cây nhóm + quyền chi tiết như màn hình Tạo/Sửa tài khoản.
/// </summary>
public partial class UserPermissionsViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly uint _userId;

    [ObservableProperty] private string title = "Phân quyền tài khoản";
    [ObservableProperty] private ObservableCollection<PermissionGroup> groups = new();
    [ObservableProperty] private string hint = string.Empty;
    [ObservableProperty] private string selectedSummary = string.Empty;

    public bool CanEditPermissions => Session.CurrentUser != null
                                      && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission)
                                      && Session.CurrentUser.Id != _userId;

    public bool CanSave => CanEditPermissions;

    public UserPermissionsViewModel(DatabaseService db, uint userId)
    {
        _db = db;
        _userId = userId;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            await _db.EnsurePermissionCatalogAsync();
            var perms = await _db.GetPermissionsAsync();

            var userPerms = await _db.GetUserPermissionsDetailAsync(_userId);
            var checkedKeys = userPerms
                .Select(x => x.MaQuyen)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var isQuanTri = await _db.IsQuanTriAsync(_userId);

            BuildGroups(perms, checkedKeys, isQuanTri);

            if (isQuanTri)
            {
                Hint = "Tài khoản QUAN_TRI luôn có toàn bộ quyền, không thể bỏ quyền.";
            }
            else if (Session.CurrentUser?.Id == _userId)
            {
                Hint = "Bạn không thể tự thay đổi quyền của chính mình.";
            }
            else if (!CanEditPermissions)
            {
                Hint = "Bạn không có quyền Phân quyền nên không thể thay đổi quyền của tài khoản này.";
            }
            else
            {
                Hint = "Chỉ những quyền được tick ở đây mới được cấp cho tài khoản.";
            }

            OnPropertyChanged(nameof(CanEditPermissions));
            OnPropertyChanged(nameof(CanSave));
            LuuCommand.NotifyCanExecuteChanged();
            ChonTatCaCommand.NotifyCanExecuteChanged();
            BoChonTatCaCommand.NotifyCanExecuteChanged();
            RaiseSelectedSummary();
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("UserPermissionsViewModel.LoadAsync", ex);
        }
    }

    private void BuildGroups(IEnumerable<Permission> perms, HashSet<string> checkedKeys, bool isQuanTri)
    {
        foreach (var existing in Groups)
        {
            existing.PropertyChanged -= OnGroupPropertyChanged;
            existing.Detach();
        }

        Groups.Clear();

        var dbByKey = perms
            .Where(p => !string.IsNullOrWhiteSpace(p.MaQuyen))
            .GroupBy(p => p.MaQuyen, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var def in PermissionCatalog.Groups)
        {
            var group = new PermissionGroup
            {
                Nhom = def.Nhom,
                TenNhom = def.TenNhom,
                IsExpanded = def.Nhom is "Products" or "Sales"
            };

            foreach (var key in def.Keys)
            {
                dbByKey.TryGetValue(key, out var row);
                group.Permissions.Add(new PermissionItem
                {
                    Id = row?.Id ?? 0,
                    MaQuyen = key,
                    TenQuyen = row?.TenQuyen is { Length: > 0 } ten ? ten : PermissionCatalog.GetTenQuyen(key),
                    Nhom = def.Nhom,
                    Loai = row?.Loai,
                    IsChecked = isQuanTri || checkedKeys.Contains(key)
                });
            }

            group.Attach();
            group.CanEditPermissions = CanEditPermissions && !isQuanTri;
            group.PropertyChanged += OnGroupPropertyChanged;
            Groups.Add(group);
        }

        // Quyền có trong DB nhưng chưa có trong danh mục.
        foreach (var orphan in dbByKey.Values.Where(p => !PermissionCatalog.AllKeys.Contains(p.MaQuyen)))
        {
            var group = Groups.FirstOrDefault(g =>
                string.Equals(g.Nhom, orphan.Nhom, StringComparison.OrdinalIgnoreCase));
            if (group is null)
            {
                group = new PermissionGroup
                {
                    Nhom = orphan.Nhom,
                    TenNhom = PermissionCatalog.GetTenNhom(orphan.Nhom),
                    CanEditPermissions = CanEditPermissions
                };
                group.Attach();
                group.PropertyChanged += OnGroupPropertyChanged;
                Groups.Add(group);
            }

            group.Permissions.Add(new PermissionItem
            {
                Id = orphan.Id,
                MaQuyen = orphan.MaQuyen,
                TenQuyen = orphan.TenQuyen,
                Nhom = orphan.Nhom,
                Loai = orphan.Loai,
                IsChecked = isQuanTri || checkedKeys.Contains(orphan.MaQuyen)
            });
        }
    }

    private void OnGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PermissionGroup.SelectedCount) or nameof(PermissionGroup.IsChecked))
        {
            RaiseSelectedSummary();
        }
    }

    private void RaiseSelectedSummary()
    {
        var total = Groups.Sum(g => g.TotalCount);
        var selected = Groups.Sum(g => g.SelectedCount);
        SelectedSummary = $"Đã chọn {selected}/{total} quyền";
    }

    private void SetAllPermissions(bool value)
    {
        foreach (var g in Groups)
        {
            g.IsChecked = value;
        }

        RaiseSelectedSummary();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task LuuAsync(Avalonia.Controls.Window? window)
    {
        if (!CanEditPermissions)
        {
            await ShowMessage("Bạn không có quyền thay đổi phân quyền của tài khoản này.");
            return;
        }

        var ids = new List<uint>();
        foreach (var g in Groups)
        {
            foreach (var it in g.Permissions)
            {
                if (it.IsChecked && it.Id > 0)
                {
                    ids.Add(it.Id);
                }
            }
        }

        try
        {
            await _db.SaveUserPermissionsAsync(_userId, ids, Session.CurrentUser?.Id);
            await ShowMessage("Đã lưu phân quyền.");
            var target = window ?? GetCurrentWindow();
            target?.Close();
        }
        catch (UnauthorizedAccessException ex)
        {
            await ShowMessage(ex.Message);
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("UserPermissionsViewModel.LuuAsync", ex);
            await ShowMessage(ex.Message);
        }
    }

    [RelayCommand]
    public void Huy(Avalonia.Controls.Window? window)
    {
        var target = window ?? GetCurrentWindow();
        target?.Close();
    }

    private static Avalonia.Controls.Window? GetCurrentWindow()
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt)
        {
            return lt.Windows.OfType<Views.UserPermissionsWindow>().FirstOrDefault(w => w.IsVisible && w.IsActive)
                ?? lt.Windows.OfType<Views.UserPermissionsWindow>().FirstOrDefault(w => w.IsVisible)
                ?? lt.Windows.FirstOrDefault(w => w.IsActive);
        }
        return null;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public void ChonTatCa() => SetAllPermissions(true);

    [RelayCommand(CanExecute = nameof(CanSave))]
    public void BoChonTatCa() => SetAllPermissions(false);

    private Task ShowMessage(string msg)
    {
        return Views.NotificationWindow.ShowAsync(msg);
    }
}
