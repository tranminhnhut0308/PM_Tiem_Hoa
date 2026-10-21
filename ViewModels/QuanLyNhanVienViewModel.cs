using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Lựa chọn trạng thái làm việc trong bộ lọc của màn "Quản lý nhân viên".
/// </summary>
public sealed class TrangThaiNhanVienOption
{
    public string Tag { get; init; } = "TAT_CA";

    public string HienThi { get; init; } = string.Empty;

    public override string ToString() => HienThi;
}

/// <summary>
/// Lựa chọn "tài khoản" trong bộ lọc của màn "Quản lý nhân viên".
/// </summary>
public sealed class TaiKhoanNhanVienOption
{
    public string Tag { get; init; } = "TAT_CA";

    public string HienThi { get; init; } = string.Empty;

    public override string ToString() => HienThi;
}

/// <summary>
/// ViewModel cho trang "Quản lý nhân viên" (mục Quản trị trên thanh menu).
///
/// Trang này quản lý HỒ SƠ NHÂN VIÊN. Việc quản lý TÀI KHOẢN ĐĂNG NHẬP
/// (tên đăng nhập, mật khẩu, vai trò, quyền) nằm ở màn "Quản lý tài khoản".
/// Một hồ sơ nhân viên có thể chưa có tài khoản đăng nhập.
/// </summary>
public partial class QuanLyNhanVienViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private CancellationTokenSource? _searchCts;
    private bool _loaded;

    [ObservableProperty] private string searchKeyword = string.Empty;
    [ObservableProperty] private TrangThaiNhanVienOption? selectedTrangThai;
    [ObservableProperty] private TaiKhoanNhanVienOption? selectedTaiKhoan;
    [ObservableProperty] private ProductOption? selectedBoPhan;
    [ObservableProperty] private ObservableCollection<ProductOption> boPhanOptions = new();
    [ObservableProperty] private ObservableCollection<NhanVienItem> nhanVienList = new();
    [ObservableProperty] private NhanVienItem? selectedNhanVien;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int pageSize = 20;
    [ObservableProperty] private ObservableCollection<int> pageSizeOptions = new() { 10, 20, 50, 100 };
    [ObservableProperty] private string statusMessage = string.Empty;

    public IReadOnlyList<TrangThaiNhanVienOption> TrangThaiOptions { get; } =
    [
        new TrangThaiNhanVienOption { Tag = "TAT_CA", HienThi = "Tất cả trạng thái" },
        new TrangThaiNhanVienOption { Tag = "DANG_LAM_VIEC", HienThi = "Đang làm việc" },
        new TrangThaiNhanVienOption { Tag = "NGUNG_HOAT_DONG", HienThi = "Ngừng hoạt động" }
    ];

    public IReadOnlyList<TaiKhoanNhanVienOption> TaiKhoanOptions { get; } =
    [
        new TaiKhoanNhanVienOption { Tag = "TAT_CA", HienThi = "Tất cả tài khoản" },
        new TaiKhoanNhanVienOption { Tag = "CO", HienThi = "Đã có tài khoản" },
        new TaiKhoanNhanVienOption { Tag = "CHUA_CO", HienThi = "Chưa có tài khoản" }
    ];

    /// <summary>Cửa sổ chủ sở dùng cho hộp thoại và thông báo.</summary>
    public Window? OwnerWindow { get; set; }

    public int TotalPages => TotalRecords <= 0 ? 1 : (int)Math.Ceiling((double)TotalRecords / Math.Max(1, PageSize));

    public bool CanGoPrevious => CurrentPage > 1 && !IsLoading;
    public bool CanGoNext => CurrentPage < TotalPages && !IsLoading;
    public bool IsEmpty => !IsLoading && NhanVienList.Count == 0;
    public bool HasRecords => !IsLoading && NhanVienList.Count > 0;

    public string PagingSummary => TotalRecords == 0
        ? "0 hồ sơ nhân viên"
        : $"Hiển thị {(CurrentPage - 1) * PageSize + 1} - {Math.Min(CurrentPage * PageSize, TotalRecords)} trong tổng số {TotalRecords} hồ sơ";

    public string PagingPagesText => $"Trang {CurrentPage} / {TotalPages}";

    // ---------- Phân quyền ----------
    public bool CanView => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesView) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanCreate => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesCreate) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesEdit) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesDelete) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanExport => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesExport) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanManageAccount => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesManageAccount) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanManageDepartment => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.EmployeesManageDepartment) || Quyen.IsAdmin(Session.CurrentUser));

    public QuanLyNhanVienViewModel(DatabaseService db)
    {
        _db = db;
        SelectedTrangThai = TrangThaiOptions[0];
        SelectedTaiKhoan = TaiKhoanOptions[0];
    }

    /// <summary>Nạp dữ liệu lần đầu khi màn được mở.</summary>
    public async Task TaiDanhSachAsync()
    {
        if (_loaded)
        {
            await LoadDataAsync();
            return;
        }

        _loaded = true;
        await LoadBoPhanAsync();
        await LoadDataAsync();
    }

    private async Task LoadBoPhanAsync()
    {
        try
        {
            var list = await _db.GetChiNhanhOptionsAsync(chiTrangThai: false);
            BoPhanOptions.Clear();
            BoPhanOptions.Add(new ProductOption { Id = 0, Name = "-- Tất cả bộ phận --" });
            foreach (var item in list)
            {
                BoPhanOptions.Add(item);
            }

            SelectedBoPhan = BoPhanOptions[0];
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("QuanLyNhanVien.LoadBoPhanAsync", ex);
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

            var statusFilter = SelectedTrangThai?.Tag;
            uint? boPhanId = SelectedBoPhan is not null && SelectedBoPhan.Id > 0 ? SelectedBoPhan.Id : null;

            bool? chiCoTaiKhoan = SelectedTaiKhoan?.Tag switch
            {
                "CO" => true,
                "CHUA_CO" => false,
                _ => null
            };

            var (items, count) = await _db.GetNhanVienPagedAsync(
                SearchKeyword,
                CurrentPage,
                PageSize,
                statusFilter,
                null,
                boPhanId,
                chiCoTaiKhoan);

            NhanVienList = new ObservableCollection<NhanVienItem>(items);
            TotalRecords = count;
            StatusMessage = $"Đã tải {items.Count} hồ sơ nhân viên.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("QuanLyNhanVien.LoadDataAsync", ex);
            await NotificationWindow.ShowErrorAsync($"Lỗi tải danh sách nhân viên: {ex.Message}", owner: GetActiveWindow());
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
        _searchCts?.Dispose();
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

    partial void OnSelectedTrangThaiChanged(TrangThaiNhanVienOption? value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnSelectedTaiKhoanChanged(TaiKhoanNhanVienOption? value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnSelectedBoPhanChanged(ProductOption? value)
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
        OnPropertyChanged(nameof(SearchKeyword));

        SelectedTrangThai = TrangThaiOptions[0];
        SelectedTaiKhoan = TaiKhoanOptions[0];
        SelectedBoPhan = BoPhanOptions.FirstOrDefault();
        CurrentPage = 1;

        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task ThemNhanVienAsync()
    {
        if (!CanCreate)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền thêm hồ sơ nhân viên.", owner: GetActiveWindow());
            return;
        }

        var vm = new NhanVienEditorViewModel(_db, CheDoNhanVienEditor.ThemMoi);
        vm.OwnerWindow = GetActiveWindow();
        vm.OnSaved = () => _ = LoadDataAsync();

        var win = new NhanVienEditorWindow(vm);
        await ShowEditorAsync(win);
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task SuaNhanVienAsync(NhanVienItem? item)
    {
        if (item is null) return;
        if (!CanEdit)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền sửa hồ sơ nhân viên.", owner: GetActiveWindow());
            return;
        }

        var vm = new NhanVienEditorViewModel(_db, CheDoNhanVienEditor.SuaHoSo, item.Id);
        vm.OwnerWindow = GetActiveWindow();
        vm.OnSaved = () => _ = LoadDataAsync();

        var win = new NhanVienEditorWindow(vm);
        await ShowEditorAsync(win);
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task CapTaiKhoanAsync(NhanVienItem? item)
    {
        if (item is null) return;
        if (!CanManageAccount)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền cấp tài khoản cho nhân viên.", owner: GetActiveWindow());
            return;
        }

        if (item.CoTaiKhoan)
        {
            await NotificationWindow.ShowWarningAsync(
                $"Nhân viên \"{item.HoTen}\" đã có tài khoản đăng nhập. Hãy quản lý tài khoản tại màn \"Quản lý tài khoản\".",
                owner: GetActiveWindow());
            return;
        }

        var vm = new NhanVienEditorViewModel(_db, CheDoNhanVienEditor.CapTaiKhoan, item.Id);
        vm.OwnerWindow = GetActiveWindow();
        vm.OnSaved = () => _ = LoadDataAsync();

        var win = new NhanVienEditorWindow(vm);
        await ShowEditorAsync(win);
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task DoiTrangThaiAsync(NhanVienItem? item)
    {
        if (item is null) return;
        if (!CanEdit)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền thay đổi trạng thái nhân viên.", owner: GetActiveWindow());
            return;
        }

        if (item.Id == Session.CurrentUser?.Id)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không thể thay đổi trạng thái làm việc của chính mình.", owner: GetActiveWindow());
            return;
        }

        var moiLamViec = !item.TrangThai;
        var xacNhan = await ShowConfirmationDialogAsync(
            moiLamViec ? "Xác nhận cho nhân viên đi làm lại" : "Xác nhận ngừng hoạt động",
            $"Bạn có chắc chắn muốn đặt \"{item.HoTen}\" ({item.MaNhanVien}) là {(moiLamViec ? "đang làm việc" : "ngừng hoạt động")} không?");

        if (!xacNhan) return;

        try
        {
            var capNhat = new NhanVienItem
            {
                Id = item.Id,
                HoTen = item.HoTen,
                ChucVu = item.ChucVu,
                SoDienThoai = item.SoDienThoai,
                Email = item.Email,
                NgayVaoLam = item.NgayVaoLam,
                ChiNhanhId = item.ChiNhanhId,
                TrangThai = moiLamViec
            };

            await _db.CapNhatNhanVienAsync(capNhat);
            await NotificationWindow.ShowSuccessAsync($"Đã cập nhật trạng thái của \"{item.HoTen}\".", owner: GetActiveWindow());
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync($"Không thể cập nhật trạng thái: {ex.Message}", owner: GetActiveWindow());
        }
    }

    [RelayCommand]
    public async Task XoaNhanVienAsync(NhanVienItem? item)
    {
        if (item is null) return;
        if (!CanDelete)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền xóa hồ sơ nhân viên.", owner: GetActiveWindow());
            return;
        }

        var (canDelete, reason) = await _db.KiemTraXoaNhanVienAsync(item.Id);
        if (!canDelete)
        {
            await NotificationWindow.ShowWarningAsync(
                reason ?? "Không thể xóa hồ sơ nhân viên này.",
                title: "Không thể xóa",
                owner: GetActiveWindow());
            return;
        }

        var xacNhan = await ShowConfirmationDialogAsync(
            "Xác nhận xóa hồ sơ nhân viên",
            $"Bạn có chắc chắn muốn xóa hồ sơ nhân viên \"{item.HoTen}\" ({item.MaNhanVien}) không? Thao tác này không thể hoàn tác.");

        if (!xacNhan) return;

        try
        {
            await _db.XoaNhanVienAnToanAsync(item.Id);
            await NotificationWindow.ShowSuccessAsync($"Đã xóa hồ sơ nhân viên \"{item.HoTen}\".", owner: GetActiveWindow());
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync($"Không thể xóa hồ sơ nhân viên: {ex.Message}", owner: GetActiveWindow());
        }
    }

    [RelayCommand]
    public async Task XuatExcelAsync()
    {
        if (!CanExport)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền xuất danh sách nhân viên.", owner: GetActiveWindow());
            return;
        }

        try
        {
            var soDong = TotalRecords < 1 ? 1 : TotalRecords;

            var statusFilter = SelectedTrangThai?.Tag;
            uint? boPhanId = SelectedBoPhan is not null && SelectedBoPhan.Id > 0 ? SelectedBoPhan.Id : null;

            bool? chiCoTaiKhoan = SelectedTaiKhoan?.Tag switch
            {
                "CO" => true,
                "CHUA_CO" => false,
                _ => null
            };

            var (items, _) = await _db.GetNhanVienPagedAsync(
                SearchKeyword, 1, soDong, statusFilter, null, boPhanId, chiCoTaiKhoan);

            if (items.Count == 0)
            {
                await NotificationWindow.ShowWarningAsync("Không có dữ liệu để xuất.", owner: GetActiveWindow());
                return;
            }

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Nhan vien");

            string[] headers = ["STT", "Mã nhân viên", "Họ và tên", "Chức vụ", "Bộ phận", "Số điện thoại", "Email", "Ngày vào làm", "Tài khoản", "Vai trò", "Trạng thái"];
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F4F0");
            }

            int row = 2;
            foreach (var it in items)
            {
                ws.Cell(row, 1).Value = it.Stt;
                ws.Cell(row, 2).Value = it.MaNhanVien;
                ws.Cell(row, 3).Value = it.HoTen;
                ws.Cell(row, 4).Value = it.ChucVu ?? "";
                ws.Cell(row, 5).Value = it.TenChiNhanh ?? "";
                ws.Cell(row, 6).Value = it.SoDienThoai ?? "";
                ws.Cell(row, 7).Value = it.Email ?? "";
                ws.Cell(row, 8).Value = it.NgayVaoLam?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 9).Value = it.CoTaiKhoan ? it.TenDangNhap : "(chưa có tài khoản)";
                ws.Cell(row, 10).Value = it.VaiTro ?? "";
                ws.Cell(row, 11).Value = it.TrangThai ? "Đang làm việc" : "Ngừng hoạt động";
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Xuất danh sách nhân viên",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                },
                SuggestedFileName = $"Danh_sach_nhan_vien_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            });

            if (file is not null)
            {
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);
                StatusMessage = "Đã xuất danh sách nhân viên ra file Excel.";
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("QuanLyNhanVien.XuatExcelAsync", ex);
            await NotificationWindow.ShowErrorAsync("Không xuất được file Excel: " + ex.Message, owner: GetActiveWindow());
        }
    }

    [RelayCommand]
    public void TrangDau()
    {
        if (CurrentPage == 1) return;
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public void TrangTruoc()
    {
        if (CurrentPage <= 1) return;
        CurrentPage--;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public void TrangSau()
    {
        if (CurrentPage >= TotalPages) return;
        CurrentPage++;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public void TrangCuoi()
    {
        if (CurrentPage == TotalPages) return;
        CurrentPage = TotalPages;
        _ = LoadDataAsync();
    }

    private async Task ShowEditorAsync(NhanVienEditorWindow win)
    {
        var owner = GetActiveWindow();
        await win.LoadDataAsync();

        if (owner != null && owner.IsVisible)
        {
            await win.ShowDialog(owner);
        }
        else
        {
            win.Show();
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

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
        {
            return lt.Windows.FirstOrDefault(w => w.IsActive) ?? lt.MainWindow;
        }

        return null;
    }
}
