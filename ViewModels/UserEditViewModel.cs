using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class UserEditViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly bool _isEdit;
    private bool _canEditPermissions = true;
    private bool _dangTaiDuLieu;

    [ObservableProperty] private string title = "Thêm tài khoản";
    [ObservableProperty] private string saveButtonText = "Tạo tài khoản mới";
    [ObservableProperty] private string passwordWatermark = "Nhập mật khẩu *";
    [ObservableProperty] private uint userId;
    [ObservableProperty] private string tenDangNhap = string.Empty;
    [ObservableProperty] private string hoTen = string.Empty;
    [ObservableProperty] private string matKhau = string.Empty;
    [ObservableProperty] private string nhapLaiMatKhau = string.Empty;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private ObservableCollection<Role> roles = new();
    [ObservableProperty] private ObservableCollection<PermissionGroup> permissionGroups = new();
    [ObservableProperty] private bool canChangeUsername = true;
    [ObservableProperty] private Role? selectedRole;
    [ObservableProperty] private bool isAdminAccount;
    [ObservableProperty] private string permissionHint = string.Empty;

    public Action? OnSaved { get; set; }

    public bool CanEditPermissions
    {
        get => _canEditPermissions;
        private set
        {
            if (_canEditPermissions == value)
            {
                return;
            }

            _canEditPermissions = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditBasicInfo));
            RefreshCommands();
        }
    }

    public bool CanEditBasicInfo => !IsAdminAccount || Session.HasPermission(PermissionKeys.UsersEdit);

    public int TotalSelectedCount => PermissionGroups.Sum(g => g.SelectedCount);

    public int TotalCount => PermissionGroups.Sum(g => g.TotalCount);

    public string SelectedSummary => $"Đã chọn {TotalSelectedCount}/{TotalCount} quyền";

    public UserEditViewModel(DatabaseService db, bool isEdit, uint userId)
    {
        _db = db;
        _isEdit = isEdit;
        this.userId = userId;
        if (_isEdit)
        {
            Title = "Sửa tài khoản";
            SaveButtonText = "Lưu tài khoản";
            PasswordWatermark = "Để trống nếu không đổi";
            canChangeUsername = false;
        }
        else
        {
            Title = "Thêm tài khoản";
            SaveButtonText = "Tạo tài khoản mới";
            PasswordWatermark = "Nhập mật khẩu *";
            canChangeUsername = true;
        }

        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            await _db.EnsurePermissionCatalogAsync();

            var rolesList = await _db.GetRolesAsync();
            Roles.Clear();
            foreach (var r in rolesList)
            {
                Roles.Add(r);
            }

            var perms = await _db.GetPermissionsAsync();
            var checkedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            uint[] currentRoleIds = Array.Empty<uint>();

            if (_isEdit && UserId > 0)
            {
                var uwr = await _db.GetUserWithRolesAsync(UserId);
                if (uwr != null)
                {
                    TenDangNhap = uwr.TenDangNhap;
                    HoTen = uwr.HoTen;
                    IsActive = uwr.TrangThai;
                    currentRoleIds = uwr.RoleIds.ToArray();
                }

                foreach (var key in await _db.GetUserPermissionKeysAsync(UserId))
                {
                    checkedKeys.Add(key);
                }
            }

            _dangTaiDuLieu = true;
            ApplyRoleSelection(currentRoleIds);
            BuildPermissionGroups(perms, checkedKeys);
            _dangTaiDuLieu = false;

            // Tạo tài khoản mới: tick sẵn nhóm quyền mặc định của vai trò đang chọn.
            if (!_isEdit)
            {
                ApplyRoleDefaults(SelectedRole);
            }

            ApplyPermissionGuard();
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("UserEditViewModel.LoadAsync", ex);
        }
    }

    private void ApplyRoleSelection(uint[] currentRoleIds)
    {
        SelectedRole = currentRoleIds.Length > 0
            ? Roles.FirstOrDefault(r => r.Id == currentRoleIds[0]) ?? Roles.FirstOrDefault()
            : Roles.FirstOrDefault(r => string.Equals(r.MaVaiTro, Quyen.ThuNgan, StringComparison.OrdinalIgnoreCase))
              ?? Roles.FirstOrDefault();

        IsAdminAccount = SelectedRole is not null
                         && string.Equals(SelectedRole.MaVaiTro, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(IsAdminAccount));
        OnPropertyChanged(nameof(CanEditBasicInfo));
    }

    private void BuildPermissionGroups(IEnumerable<Permission> perms, HashSet<string> checkedKeys)
    {
        foreach (var existing in PermissionGroups)
        {
            existing.PropertyChanged -= OnGroupPropertyChanged;
            existing.Detach();
        }

        PermissionGroups.Clear();

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
                    IsChecked = IsAdminAccount || checkedKeys.Contains(key)
                });
            }

            group.Attach();
            group.PropertyChanged += OnGroupPropertyChanged;
            PermissionGroups.Add(group);
        }

        // Quyền có trong DB nhưng chưa có trong danh mục: gom vào nhóm tương ứng để không bị mất.
        foreach (var orphan in dbByKey.Values.Where(p => !PermissionCatalog.AllKeys.Contains(p.MaQuyen)))
        {
            var group = PermissionGroups.FirstOrDefault(g =>
                string.Equals(g.Nhom, orphan.Nhom, StringComparison.OrdinalIgnoreCase));
            if (group is null)
            {
                group = new PermissionGroup
                {
                    Nhom = orphan.Nhom,
                    TenNhom = PermissionCatalog.GetTenNhom(orphan.Nhom)
                };
                group.Attach();
                group.PropertyChanged += OnGroupPropertyChanged;
                PermissionGroups.Add(group);
            }

            group.Permissions.Add(new PermissionItem
            {
                Id = orphan.Id,
                MaQuyen = orphan.MaQuyen,
                TenQuyen = orphan.TenQuyen,
                Nhom = orphan.Nhom,
                Loai = orphan.Loai,
                IsChecked = IsAdminAccount || checkedKeys.Contains(orphan.MaQuyen)
            });
        }

        OnPropertyChanged(nameof(TotalSelectedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SelectedSummary));
    }

    /// <summary>Chặn sửa quyền khi bản thân không có quyền phân quyền, hoặc đang sửa tài khoản Admin.</summary>
    private void ApplyPermissionGuard()
    {
        var canAssign = Session.HasPermission(PermissionKeys.UsersAssignPermission);
        var isSelf = _isEdit && UserId > 0 && Session.CurrentUser?.Id == UserId;

        if (IsAdminAccount)
        {
            CanEditPermissions = false;
            PermissionHint = "Tài khoản QUAN_TRI luôn có toàn bộ quyền, không thể bỏ quyền.";
        }
        else if (!canAssign)
        {
            CanEditPermissions = false;
            PermissionHint = "Bạn không có quyền Phân quyền nên không thể thay đổi quyền của tài khoản này.";
        }
        else if (isSelf)
        {
            CanEditPermissions = false;
            PermissionHint = "Bạn không thể tự thay đổi quyền của chính mình.";
        }
        else
        {
            CanEditPermissions = true;
            PermissionHint = "Chỉ những quyền được tick ở đây mới được cấp cho tài khoản.";
        }

        foreach (var g in PermissionGroups)
        {
            g.CanEditPermissions = CanEditPermissions;
        }

        OnPropertyChanged(nameof(PermissionHint));
    }

    partial void OnSelectedRoleChanged(Role? value)
    {
        // Khi nạp dữ liệu, LoadAsync đã tự dựng cây quyền nên bỏ qua để không ghi đè.
        if (_dangTaiDuLieu)
        {
            return;
        }

        ApplyRoleDefaults(value);
        ApplyPermissionGuard();
        RefreshCommands();
    }

    /// <summary>
    /// Tick sẵn các quyền mặc định của vai trò đang chọn (người dùng vẫn sửa lại được).
    /// QUAN_TRI luôn được tick toàn bộ.
    /// </summary>
    private void ApplyRoleDefaults(Role? role)
    {
        var isAdmin = role is not null
                      && string.Equals(role.MaVaiTro, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase);

        IsAdminAccount = isAdmin;
        OnPropertyChanged(nameof(IsAdminAccount));
        OnPropertyChanged(nameof(CanEditBasicInfo));

        if (isAdmin)
        {
            SetAllPermissions(true);
            return;
        }

        var defaults = role is not null
                       && PermissionCatalog.DefaultRolePermissions.TryGetValue(role.MaVaiTro, out var keys)
            ? new HashSet<string>(keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        foreach (var g in PermissionGroups)
        {
            foreach (var it in g.Permissions)
            {
                it.IsChecked = defaults.Contains(it.MaQuyen);
            }
        }

        RaiseSelectedSummary();
    }

    private void SetAllPermissions(bool value)
    {
        foreach (var g in PermissionGroups)
        {
            g.IsChecked = value;
        }

        RaiseSelectedSummary();
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
        OnPropertyChanged(nameof(TotalSelectedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SelectedSummary));
    }

    private void RefreshCommands()
    {
        ChonTatCaCommand.NotifyCanExecuteChanged();
        BoChonTatCaCommand.NotifyCanExecuteChanged();
        MoTatCaNhomCommand.NotifyCanExecuteChanged();
        DongTatCaNhomCommand.NotifyCanExecuteChanged();
    }

    private bool CanChonTatCa => CanEditPermissions;
    private bool CanBoChonTatCa => CanEditPermissions;
    private bool CanMoTatCaNhom => PermissionGroups.Count > 0;
    private bool CanDongTatCaNhom => PermissionGroups.Count > 0;

    [RelayCommand(CanExecute = nameof(CanChonTatCa))]
    private void ChonTatCa() => SetAllPermissions(true);

    [RelayCommand(CanExecute = nameof(CanBoChonTatCa))]
    private void BoChonTatCa() => SetAllPermissions(false);

    [RelayCommand(CanExecute = nameof(CanMoTatCaNhom))]
    private void MoTatCaNhom()
    {
        foreach (var g in PermissionGroups)
        {
            g.IsExpanded = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDongTatCaNhom))]
    private void DongTatCaNhom()
    {
        foreach (var g in PermissionGroups)
        {
            g.IsExpanded = false;
        }
    }

    private List<uint> BuildRoleIds()
    {
        if (SelectedRole is not null)
        {
            return new List<uint> { SelectedRole.Id };
        }

        var fallback = Roles.FirstOrDefault(r => string.Equals(r.MaVaiTro, Quyen.ThuNgan, StringComparison.OrdinalIgnoreCase))
                       ?? Roles.FirstOrDefault();
        return fallback is null ? new List<uint>() : new List<uint> { fallback.Id };
    }

    private List<uint> BuildPermissionIds()
    {
        var ids = new List<uint>();
        foreach (var g in PermissionGroups)
        {
            foreach (var it in g.Permissions)
            {
                if (it.IsChecked && it.Id > 0)
                {
                    ids.Add(it.Id);
                }
            }
        }

        return ids;
    }

    [RelayCommand]
    public async Task SaveAsync(Avalonia.Controls.Window? window)
    {
        if (string.IsNullOrWhiteSpace(TenDangNhap))
        {
            await ShowMessage("Vui lòng nhập tên đăng nhập.");
            return;
        }

        if (string.IsNullOrWhiteSpace(HoTen))
        {
            await ShowMessage("Vui lòng nhập họ và tên.");
            return;
        }

        var username = TenDangNhap.Trim();

        if (!_isEdit)
        {
            if (!Session.HasPermission(PermissionKeys.UsersCreate))
            {
                await ShowMessage("Bạn không có quyền tạo tài khoản.");
                return;
            }

            if (string.IsNullOrWhiteSpace(MatKhau))
            {
                await ShowMessage("Vui lòng nhập mật khẩu.");
                return;
            }

            if (MatKhau != NhapLaiMatKhau)
            {
                await ShowMessage("Mật khẩu xác nhận không khớp. Vui lòng nhập lại.");
                return;
            }

            var users = await _db.GetUsersAsync();
            if (users.Any(u => u.TenDangNhap.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                await ShowMessage("Tên đăng nhập đã tồn tại trong hệ thống. Vui lòng chọn tên đăng nhập khác.");
                return;
            }
        }
        else
        {
            if (!Session.HasPermission(PermissionKeys.UsersEdit))
            {
                await ShowMessage("Bạn không có quyền sửa tài khoản.");
                return;
            }

            if (IsAdminAccount && !string.IsNullOrWhiteSpace(SelectedRole?.MaVaiTro)
                                && !string.Equals(SelectedRole.MaVaiTro, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase)
                                && !Session.HasPermission(PermissionKeys.UsersAssignPermission))
            {
                await ShowMessage("Không thể bỏ vai trò QUAN_TRI của tài khoản này.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(MatKhau) && MatKhau != NhapLaiMatKhau)
            {
                await ShowMessage("Mật khẩu xác nhận không khớp. Vui lòng nhập lại.");
                return;
            }
        }

        TenDangNhap = username;
        HoTen = HoTen.Trim();

        try
        {
            var roleIds = BuildRoleIds();
            var permissionIds = BuildPermissionIds();
            var actorId = Session.CurrentUser?.Id;

            if (_isEdit)
            {
                var dto = new UserEditDto
                {
                    Id = UserId,
                    TenDangNhap = TenDangNhap,
                    HoTen = HoTen,
                    MatKhau = string.IsNullOrWhiteSpace(MatKhau) ? null : MatKhau,
                    TrangThai = IsActive,
                    RoleIds = roleIds,
                    PermissionIds = permissionIds
                };
                await _db.UpdateUserAsync(dto, actorId);

                // Chỉ gán lại quyền chi tiết khi người thao tác thực sự có quyền phân quyền.
                if (CanEditPermissions)
                {
                    await _db.SaveUserPermissionsAsync(UserId, permissionIds, actorId);
                }
                
                await ShowMessage("Cập nhật tài khoản thành công!");
            }
            else
            {
                var dto = new UserCreateDto
                {
                    TenDangNhap = TenDangNhap,
                    HoTen = HoTen,
                    MatKhau = MatKhau,
                    TrangThai = IsActive,
                    RoleIds = roleIds,
                    PermissionIds = permissionIds
                };
                await _db.CreateUserAsync(dto, actorId);
            }

            OnSaved?.Invoke();

            var target = window ?? GetCurrentWindow();
            target?.Close(true);

            await Views.NotificationWindow.ShowSuccessAsync(
                _isEdit ? "Cập nhật tài khoản thành công!" : "Tạo tài khoản mới thành công!");
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            await Views.NotificationWindow.ShowWarningAsync(
                "Tên đăng nhập đã tồn tại trong hệ thống. Vui lòng chọn tên đăng nhập khác.",
                owner: GetCurrentWindow());
        }
        catch (UnauthorizedAccessException ex)
        {
            await Views.NotificationWindow.ShowWarningAsync(ex.Message, owner: GetCurrentWindow());
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("UserEditViewModel.SaveAsync", ex);
            await Views.NotificationWindow.ShowErrorAsync(
                "Không thể " + (_isEdit ? "cập nhật" : "tạo") + " tài khoản: " + ex.Message,
                owner: GetCurrentWindow());
        }
    }

    [RelayCommand]
    public void Cancel(Avalonia.Controls.Window? window)
    {
        var target = window ?? GetCurrentWindow();
        target?.Close(false);
    }

    private static Avalonia.Controls.Window? GetCurrentWindow()
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt)
        {
            return lt.Windows.OfType<Views.UserEditWindow>().FirstOrDefault(w => w.IsVisible && w.IsActive)
                ?? lt.Windows.OfType<Views.UserEditWindow>().FirstOrDefault(w => w.IsVisible)
                ?? lt.Windows.FirstOrDefault(w => w.IsActive);
        }
        return null;
    }

    private Task ShowMessage(string msg)
    {
        return Views.NotificationWindow.ShowWarningAsync(msg, owner: GetCurrentWindow());
    }
}
