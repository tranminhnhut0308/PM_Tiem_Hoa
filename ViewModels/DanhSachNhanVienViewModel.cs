using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Helpers;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public partial class DanhSachNhanVienViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private string searchKeyword = string.Empty;
    [ObservableProperty] private string selectedTrangThai = "Tất cả trạng thái";
    [ObservableProperty] private Role? selectedRole;
    [ObservableProperty] private ObservableCollection<Role> roleOptions = new();
    [ObservableProperty] private ObservableCollection<string> trangThaiOptions = new()
    {
        "Tất cả trạng thái",
        "Đang làm việc",
        "Đã khóa"
    };

    [ObservableProperty] private ObservableCollection<NhanVienItem> nhanVienList = new();
    [ObservableProperty] private NhanVienItem? selectedNhanVien;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int pageSize = 20;
    [ObservableProperty] private ObservableCollection<int> pageSizeOptions = new() { 10, 20, 50 };

    public Window? OwnerWindow { get; set; }

    public int TotalPages => TotalRecords <= 0 ? 1 : (int)Math.Ceiling((double)TotalRecords / Math.Max(1, PageSize));

    public bool CanGoPrevious => CurrentPage > 1 && !IsLoading;
    public bool CanGoNext => CurrentPage < TotalPages && !IsLoading;
    public bool IsEmpty => !IsLoading && NhanVienList.Count == 0;
    public bool HasRecords => !IsLoading && NhanVienList.Count > 0;

    public string PagingSummary => TotalRecords == 0
        ? "0 tài khoản"
        : $"Hiển thị {(CurrentPage - 1) * PageSize + 1} - {Math.Min(CurrentPage * PageSize, TotalRecords)} trong tổng số {TotalRecords} tài khoản";

    public string PagingPagesText => $"Trang {CurrentPage} / {TotalPages}";

    // Phân quyền
    public bool CanView => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersView) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanCreate => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersCreate) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersEdit) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersDelete) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanLock => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersLock) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanUnlock => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersUnlock) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanAssignPermission => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission) || Quyen.IsAdmin(Session.CurrentUser));

    public DanhSachNhanVienViewModel()
    {
        _ = InitAsync();
    }

    public async Task InitAsync()
    {
        await LoadRolesAsync();
        await LoadDataAsync();
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            var roles = await _db.GetRolesAsync();
            RoleOptions.Clear();
            RoleOptions.Add(new Role { Id = 0, TenVaiTro = "Tất cả chức vụ", MaVaiTro = "" });
            foreach (var r in roles)
            {
                RoleOptions.Add(r);
            }
            SelectedRole = RoleOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("LoadRolesAsync", ex);
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (!CanView)
        {
            NhanVienList.Clear();
            TotalRecords = 0;
            UpdatePagingProperties();
            return;
        }

        try
        {
            IsLoading = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasRecords));

            string? statusFilter = null;
            if (SelectedTrangThai == "Đang làm việc") statusFilter = "DANG_LAM_VIEC";
            else if (SelectedTrangThai == "Đã khóa") statusFilter = "KHOA";

            uint? roleFilter = (SelectedRole != null && SelectedRole.Id > 0) ? SelectedRole.Id : null;

            var (items, count) = await _db.GetNhanVienPagedAsync(
                SearchKeyword,
                CurrentPage,
                PageSize,
                statusFilter,
                roleFilter,
                null,
                chiCoTaiKhoan: true);

            NhanVienList = new ObservableCollection<NhanVienItem>(items);
            TotalRecords = count;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("DanhSachNhanVien.LoadDataAsync", ex);
            await NotificationWindow.ShowErrorAsync($"Lỗi tải danh sách tài khoản: {ex.Message}", owner: GetActiveWindow());
        }
        finally
        {
            IsLoading = false;
            UpdatePagingProperties();
        }
    }

    private void UpdatePagingProperties()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PagingPagesText));
        OnPropertyChanged(nameof(PagingSummary));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasRecords));
    }

    partial void OnSearchKeywordChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    CurrentPage = 1;
                    await LoadDataAsync();
                });
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    partial void OnSelectedTrangThaiChanged(string value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnSelectedRoleChanged(Role? value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public void XoaBoLoc()
    {
        SearchKeyword = string.Empty;
        SelectedTrangThai = "Tất cả trạng thái";
        SelectedRole = RoleOptions.FirstOrDefault();
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task ThemNhanVienAsync()
    {
        if (!CanCreate)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền thêm mới tài khoản.", owner: GetActiveWindow());
            return;
        }

        var vm = new UserEditViewModel(_db, isEdit: false, userId: 0);
        vm.OnSaved = () => _ = LoadDataAsync();
        var win = new UserEditWindow(vm);
        var owner = GetActiveWindow();
        if (owner != null && owner.IsVisible)
        {
            await win.ShowDialog(owner);
        }
        else
        {
            win.Closed += (_, _) => _ = LoadDataAsync();
            win.Show();
        }
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task SuaNhanVienAsync(NhanVienItem? item)
    {
        if (item == null) return;
        if (!CanEdit)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền sửa thông tin nhân viên.", owner: GetActiveWindow());
            return;
        }

        var vm = new UserEditViewModel(_db, isEdit: true, userId: item.Id);
        vm.OnSaved = () => _ = LoadDataAsync();
        var win = new UserEditWindow(vm);
        var owner = GetActiveWindow();
        if (owner != null && owner.IsVisible)
        {
            await win.ShowDialog(owner);
        }
        else
        {
            win.Closed += (_, _) => _ = LoadDataAsync();
            win.Show();
        }
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task XemChiTietAsync(NhanVienItem? item)
    {
        if (item == null) return;
        await SuaNhanVienAsync(item);
    }

    [RelayCommand]
    public async Task PhanQuyenAsync(NhanVienItem? item)
    {
        if (item == null) return;
        if (!CanAssignPermission)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền phân quyền truy cập.", owner: GetActiveWindow());
            return;
        }

        if (Session.CurrentUser != null && item.Id == Session.CurrentUser.Id)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không thể tự phân quyền cho chính mình.", owner: GetActiveWindow());
            return;
        }

        var vm = new UserPermissionsViewModel(_db, item.Id);
        var win = new UserPermissionsWindow(vm);
        var owner = GetActiveWindow();
        if (owner != null && owner.IsVisible)
        {
            await win.ShowDialog(owner);
        }
        else
        {
            win.Show();
        }
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task KhoaNhanVienAsync(NhanVienItem? item)
    {
        if (item == null) return;
        if (!CanLock)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền khóa nhân viên.", owner: GetActiveWindow());
            return;
        }

        if (Session.CurrentUser != null && item.Id == Session.CurrentUser.Id)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không thể tự khóa tài khoản của chính mình đang đăng nhập.", owner: GetActiveWindow());
            return;
        }

        if (item.IsAdmin)
        {
            await NotificationWindow.ShowWarningAsync("Không thể khóa tài khoản có vai trò Quản trị viên.", owner: GetActiveWindow());
            return;
        }

        var confirm = await ShowConfirmationDialogAsync("Xác nhận khóa nhân viên", $"Bạn có chắc chắn muốn khóa nhân viên \"{item.HoTen}\" ({item.MaNhanVien}) không? Nhân viên này sẽ không thể đăng nhập vào hệ thống.");
        if (!confirm) return;

        try
        {
            await _db.LockUserAsync(item.Id, Session.CurrentUser?.Id);
            await NotificationWindow.ShowSuccessAsync($"Đã khóa nhân viên \"{item.HoTen}\".", owner: GetActiveWindow());
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync($"Không thể khóa nhân viên: {ex.Message}", owner: GetActiveWindow());
        }
    }

    [RelayCommand]
    public async Task MoKhoaNhanVienAsync(NhanVienItem? item)
    {
        if (item == null) return;
        if (!CanUnlock)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền mở khóa nhân viên.", owner: GetActiveWindow());
            return;
        }

        var confirm = await ShowConfirmationDialogAsync("Xác nhận mở khóa", $"Bạn có muốn mở khóa cho nhân viên \"{item.HoTen}\" ({item.MaNhanVien}) hoạt động trở lại không?");
        if (!confirm) return;

        try
        {
            await _db.UnlockUserAsync(item.Id, Session.CurrentUser?.Id);
            await NotificationWindow.ShowSuccessAsync($"Đã mở khóa thành công cho nhân viên \"{item.HoTen}\".", owner: GetActiveWindow());
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync($"Không thể mở khóa nhân viên: {ex.Message}", owner: GetActiveWindow());
        }
    }

    [RelayCommand]
    public async Task XoaNhanVienAsync(NhanVienItem? item)
    {
        if (item == null) return;
        if (!CanDelete)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền xóa nhân viên.", owner: GetActiveWindow());
            return;
        }

        // 1. Kiểm tra an toàn trước
        var (canDelete, reason) = await _db.KiemTraXoaNhanVienAsync(item.Id);
        if (!canDelete)
        {
            await NotificationWindow.ShowWarningAsync(reason ?? "Không thể xóa nhân viên này.", title: "Không thể xóa", owner: GetActiveWindow());
            return;
        }

        // 2. Xác nhận xóa
        var confirm = await ShowConfirmationDialogAsync("Xác nhận xóa nhân viên", $"Bạn có chắc chắn muốn xóa vĩnh viễn nhân viên \"{item.HoTen}\" ({item.MaNhanVien}) khỏi hệ thống không? Thao tác này không thể hoàn tác.");
        if (!confirm) return;

        try
        {
            await _db.XoaNhanVienAnToanAsync(item.Id);
            await NotificationWindow.ShowSuccessAsync($"Đã xóa nhân viên \"{item.HoTen}\" thành công.", owner: GetActiveWindow());
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync($"Không thể xóa nhân viên: {ex.Message}", owner: GetActiveWindow());
        }
    }

    [RelayCommand]
    public void TrangDau()
    {
        if (CurrentPage != 1)
        {
            CurrentPage = 1;
            _ = LoadDataAsync();
        }
    }

    [RelayCommand]
    public void TrangTruoc()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            _ = LoadDataAsync();
        }
    }

    [RelayCommand]
    public void TrangSau()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            _ = LoadDataAsync();
        }
    }

    [RelayCommand]
    public void TrangCuoi()
    {
        if (CurrentPage != TotalPages)
        {
            CurrentPage = TotalPages;
            _ = LoadDataAsync();
        }
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var owner = GetActiveWindow();
        var confirmWin = new Window
        {
            Title = title,
            Width = 460,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.White
        };

        var result = false;
        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(new TextBlock { Text = "⚠️", FontSize = 22, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = Avalonia.Media.Brush.Parse("#0F172A"), VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brush.Parse("#475569"),
            FontSize = 13.5,
            LineHeight = 20
        });

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        var btnCancel = new Button
        {
            Content = "Hủy",
            Padding = new Avalonia.Thickness(18, 8),
            Background = Avalonia.Media.Brush.Parse("#F1F5F9"),
            Foreground = Avalonia.Media.Brush.Parse("#334155"),
            CornerRadius = new Avalonia.CornerRadius(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        btnCancel.Click += (_, _) => { result = false; confirmWin.Close(); };

        var btnOk = new Button
        {
            Content = "Đồng ý",
            Padding = new Avalonia.Thickness(18, 8),
            Background = Avalonia.Media.Brush.Parse("#0B4B82"),
            Foreground = Avalonia.Media.Brushes.White,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            CornerRadius = new Avalonia.CornerRadius(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        btnOk.Click += (_, _) => { result = true; confirmWin.Close(); };

        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnOk);
        panel.Children.Add(btnRow);

        confirmWin.Content = panel;

        if (owner != null && owner.IsVisible)
        {
            try
            {
                await confirmWin.ShowDialog(owner);
                return result;
            }
            catch { }
        }

        var tcs = new TaskCompletionSource<bool>();
        confirmWin.Closed += (_, _) => tcs.TrySetResult(result);
        confirmWin.Show();
        return await tcs.Task;
    }

    private Window? GetActiveWindow()
    {
        if (OwnerWindow != null && OwnerWindow.IsVisible)
            return OwnerWindow;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
        {
            return lt.Windows.FirstOrDefault(w => w.IsActive) ?? lt.MainWindow;
        }

        return null;
    }
}
