using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;
using System.Linq;

namespace ShopHoa.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<UserListItem> users = new();

    public UsersViewModel()
    {
        _ = LoadAsync();
    }

    /// <summary>Chỉ mở được cửa sổ quản lý tài khoản khi có quyền xem danh sách tài khoản hoặc tạo tài khoản.</summary>
    public static bool DuocXemTaiKhoan => Session.CurrentUser != null
                                          && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersView)
                                              || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersCreate)
                                              || Quyen.IsAdmin(Session.CurrentUser));

    public bool CanCreateUser => Session.CurrentUser != null
                                 && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersCreate)
                                     || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanEditUser => DuocXemTaiKhoan && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersEdit);
    public bool CanAssignPermission => DuocXemTaiKhoan && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission);
    public bool CanLockUser => DuocXemTaiKhoan && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersLock);
    public bool CanUnlockUser => DuocXemTaiKhoan && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersUnlock);
    public bool CanResetPassword => DuocXemTaiKhoan && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersResetPassword);
    public bool CanDeleteUser => DuocXemTaiKhoan && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersDelete);

    private bool KiemTraQuyen(string key, string message)
    {
        if (Session.CurrentUser != null && Quyen.HasPermission(Session.CurrentUser, key))
        {
            return true;
        }

        _ = ShowMessage(message);
        return false;
    }

    /// <summary>Không được tự phân quyền / khóa / xóa chính mình.</summary>
    private bool KiemTraTuThaoTac(UserListItem? item, string message)
    {
        if (item != null && Session.CurrentUser != null && item.Id == Session.CurrentUser.Id)
        {
            _ = ShowMessage(message);
            return false;
        }

        return true;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (!DuocXemTaiKhoan)
        {
            Users = new ObservableCollection<UserListItem>();
            return;
        }

        try
        {
            var data = await _db.GetUsersAsync(SearchText);
            Users = new ObservableCollection<UserListItem>(data);
        }
        catch (Exception ex)
        {
            _ = ShowMessage("Lỗi tải danh sách tài khoản: " + ex.Message);
        }
    }

    [RelayCommand]
    public async Task TimKiemAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ReloadAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    public async Task MoThemTaiKhoanAsync()
    {
        if (!KiemTraQuyen(PermissionKeys.UsersCreate, "Bạn không có quyền tạo tài khoản."))
        {
            return;
        }

        var vm = new UserEditViewModel(_db, isEdit: false, userId: 0);
        vm.OnSaved = () => _ = LoadAsync();
        var win = new Views.UserEditWindow(vm);
        Avalonia.Controls.Window? owner = null;
        try
        {
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt)
            {
                owner = lt.Windows.FirstOrDefault(w => w.IsActive) ?? lt.MainWindow;
            }
        }
        catch { owner = null; }
        if (owner == null || owner.IsVisible == false)
        {
            win.Closed += (_, _) => _ = LoadAsync();
            win.Show();
        }
        else
        {
            try
            {
                await win.ShowDialog(owner);
                await LoadAsync();
            }
            catch
            {
                win.Closed += (_, _) => _ = LoadAsync();
                win.Show();
            }
        }
    }

    [RelayCommand]
    public async Task MoSuaTaiKhoanAsync(UserListItem? item)
    {
        if (item == null) return;
        if (!KiemTraQuyen(PermissionKeys.UsersEdit, "Bạn không có quyền sửa tài khoản."))
        {
            return;
        }

        var vm = new UserEditViewModel(_db, isEdit: true, userId: item.Id);
        vm.OnSaved = () => _ = LoadAsync();
        var win = new Views.UserEditWindow(vm);
        Avalonia.Controls.Window? owner = null;
        try
        {
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt)
            {
                owner = lt.Windows.FirstOrDefault(w => w.IsActive) ?? lt.MainWindow;
            }
        }
        catch { owner = null; }
        if (owner == null || owner.IsVisible == false)
        {
            win.Closed += (_, _) => _ = LoadAsync();
            win.Show();
        }
        else
        {
            try
            {
                await win.ShowDialog(owner);
                await LoadAsync();
            }
            catch
            {
                win.Closed += (_, _) => _ = LoadAsync();
                win.Show();
            }
        }
    }

    

    [RelayCommand]
    public async Task MoPhanQuyenAsync(UserListItem? item)
    {
        if (item == null) return;
        if (!KiemTraQuyen(PermissionKeys.UsersAssignPermission, "Bạn không có quyền phân quyền."))
        {
            return;
        }

        if (!KiemTraTuThaoTac(item, "Bạn không thể tự phân quyền cho chính mình."))
        {
            return;
        }

        var vm = new UserPermissionsViewModel(_db, item.Id);
        var win = new Views.UserPermissionsWindow(vm);
        await win.ShowDialog(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt ? lt.MainWindow : null);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task KhoaTaiKhoanAsync(UserListItem? item)
    {
        if (item == null) return;
        if (!KiemTraQuyen(PermissionKeys.UsersLock, "Bạn không có quyền khóa tài khoản."))
        {
            return;
        }

        if (!KiemTraTuThaoTac(item, "Bạn không thể tự khóa tài khoản của chính mình."))
        {
            return;
        }

        try
        {
            await _db.LockUserAsync(item.Id, Session.CurrentUser?.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ShowMessage(ex.Message);
        }
    }

    [RelayCommand]
    public async Task MoKhoaTaiKhoanAsync(UserListItem? item)
    {
        if (item == null) return;
        if (!KiemTraQuyen(PermissionKeys.UsersUnlock, "Bạn không có quyền mở khóa tài khoản."))
        {
            return;
        }

        try
        {
            await _db.UnlockUserAsync(item.Id, Session.CurrentUser?.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ShowMessage(ex.Message);
        }
    }

    [RelayCommand]
    public async Task DatLaiMatKhauAsync(UserListItem? item)
    {
        if (item == null) return;
        if (!KiemTraQuyen(PermissionKeys.UsersResetPassword, "Bạn không có quyền đặt lại mật khẩu."))
        {
            return;
        }

        try
        {
            var newPwd = "123456";
            await _db.ResetPasswordAsync(item.Id, newPwd, Session.CurrentUser?.Id);
            await ShowMessage($"Đặt lại mật khẩu thành công. Mật khẩu mới: {newPwd}");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ShowMessage(ex.Message);
        }
    }

    [RelayCommand]
    public async Task XoaTaiKhoanAsync(UserListItem? item)
    {
        if (item == null) return;
        if (!KiemTraQuyen(PermissionKeys.UsersDelete, "Bạn không có quyền xóa tài khoản."))
        {
            return;
        }

        if (!KiemTraTuThaoTac(item, "Bạn không thể tự xóa tài khoản của chính mình."))
        {
            return;
        }

        if (item.Id == 1) { await ShowMessage("Không thể xóa tài khoản Admin mặc định."); return; }
        try
        {
            await _db.DeleteUserAsync(item.Id, Session.CurrentUser?.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ShowMessage(ex.Message);
        }
    }

    private Task ShowMessage(string msg)
    {
        return Views.NotificationWindow.ShowWarningAsync(msg);
    }
}
