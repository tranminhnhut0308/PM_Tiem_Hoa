using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Một mục trong danh sách chọn trạng thái của bộ lọc khuyến mãi.
/// </summary>
public sealed class TrangThaiKhuyenMaiOption
{
    public string Tag { get; init; } = "TAT_CA";

    public string HienThi { get; init; } = string.Empty;

    public override string ToString() => HienThi;
}

/// <summary>
/// Một mục trong danh sách chọn số dòng hiển thị mỗi trang.
/// </summary>
public sealed class SoDongHienThiOption
{
    public int SoDong { get; init; }

    public string HienThi { get; init; } = string.Empty;

    public override string ToString() => HienThi;
}

/// <summary>
/// ViewModel cho trang "Quản lý chương trình khuyến mãi"
/// (Danh mục → Khuyến mãi → Quản lý danh mục khuyến mãi).
///
/// Trang này thay cho cửa sổ phụ cũ: toàn bộ nghiệp vụ tải dữ liệu, lọc,
/// tìm kiếm, phân trang, thêm/sửa/xóa và phân quyền được giữ nguyên, chỉ
/// chuyển điểm neo từ cửa sổ nổi sang vùng làm việc chính của ứng dụng.
/// </summary>
public partial class DanhSachKhuyenMaiViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly List<ChuongTrinhKhuyenMai> _allRawItems = new();
    private readonly List<ChuongTrinhKhuyenMai> _filteredItems = new();

    private bool _isLoading;

    /// <summary>Cửa sổ chính, dùng làm owner cho các hộp thoại thêm/sửa/xóa.</summary>
    public Window? OwnerWindow { get; set; }

    /// <summary>Dữ liệu hiển thị cho trang hiện tại.</summary>
    public ObservableCollection<ChuongTrinhKhuyenMai> Items { get; } = new();

    // ============================================================
    // BỘ LỌC
    // ============================================================

    /// <summary>Từ khóa tìm theo mã hoặc tên chương trình.</summary>
    [ObservableProperty]
    private string timKiem = string.Empty;

    /// <summary>Trạng thái đang chọn trong bộ lọc.</summary>
    [ObservableProperty]
    private TrangThaiKhuyenMaiOption? selectedTrangThai;

    /// <summary>Ngày bắt đầu áp dụng (null = không lọc).</summary>
    [ObservableProperty]
    private DateTime? tuNgay;

    /// <summary>Ngày kết thúc áp dụng (null = không lọc).</summary>
    [ObservableProperty]
    private DateTime? denNgay;

    public IReadOnlyList<TrangThaiKhuyenMaiOption> TrangThaiOptions { get; } =
    [
        new TrangThaiKhuyenMaiOption { Tag = "TAT_CA", HienThi = "-- Tất cả trạng thái --" },
        new TrangThaiKhuyenMaiOption { Tag = "DANG_HOAT_DONG", HienThi = "🟢 Đang diễn ra" },
        new TrangThaiKhuyenMaiOption { Tag = "CHUA_BAT_DAU", HienThi = "🔵 Sắp áp dụng" },
        new TrangThaiKhuyenMaiOption { Tag = "TAM_NGUNG", HienThi = "🟡 Tạm ngưng" },
        new TrangThaiKhuyenMaiOption { Tag = "DA_KET_THUC", HienThi = "⚪ Đã kết thúc" }
    ];

    public IReadOnlyList<SoDongHienThiOption> SoDongOptions { get; } =
    [
        new SoDongHienThiOption { SoDong = 15, HienThi = "15 dòng/trang" },
        new SoDongHienThiOption { SoDong = 25, HienThi = "25 dòng/trang" },
        new SoDongHienThiOption { SoDong = 50, HienThi = "50 dòng/trang" },
        new SoDongHienThiOption { SoDong = 100, HienThi = "100 dòng/trang" }
    ];

    // ============================================================
    // PHÂN TRANG
    // ============================================================

    /// <summary>Số dòng hiển thị mỗi trang.</summary>
    [ObservableProperty]
    private SoDongHienThiOption? selectedSoDong;

    /// <summary>Trang hiện tại (bắt đầu từ 1).</summary>
    [ObservableProperty]
    private int currentPage = 1;

    public int PageSize => SelectedSoDong?.SoDong ?? 15;

    public int TotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)_filteredItems.Count / PageSize));

    public string PagingInfoText =>
        $"Trang {CurrentPage} / {TotalPages}  •  Tổng {_filteredItems.Count:N0} chương trình";

    public bool CanVeTrangDau => CurrentPage > 1;

    public bool CanVeTrangTruoc => CurrentPage > 1;

    public bool CanVeTrangSau => CurrentPage < TotalPages;

    public bool CanVeTrangCuoi => CurrentPage < TotalPages;

    // ============================================================
    // TRẠNG THÁI HIỂN THỊ
    // ============================================================

    /// <summary>Đang tải dữ liệu từ CSDL.</summary>
    [ObservableProperty]
    private bool dangTai;

    public bool CoDuLieu => _filteredItems.Count > 0;

    public bool KhongCoDuLieu => !DangTai && _filteredItems.Count == 0;

    public string KetQuaLocText => $"{_filteredItems.Count:N0} chương trình";

    public int TongSoChuongTrinh => _allRawItems.Count;

    public int SoDangDienRa =>
        _allRawItems.Count(x => x.TrangThaiTinhToan == "DANG_HOAT_DONG");

    public int SoSapApDung =>
        _allRawItems.Count(x => x.TrangThaiTinhToan == "CHUA_BAT_DAU");

    public int SoKetThucNgung =>
        _allRawItems.Count(x => x.TrangThaiTinhToan is "DA_KET_THUC" or "TAM_NGUNG");

    public string TongSoHienThi => $"{TongSoChuongTrinh:N0} chương trình";

    /// <summary>Được phép thêm chương trình khuyến mãi hay không.</summary>
    public bool CanThemChuongTrinh =>
        Session.CurrentUser is null
        || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsCreate)
        || Quyen.IsAdmin(Session.CurrentUser);

    public DanhSachKhuyenMaiViewModel(DatabaseService? db = null)
    {
        _db = db ?? new DatabaseService();
        SelectedTrangThai = TrangThaiOptions[0];
        SelectedSoDong = SoDongOptions[0];
    }

    // ============================================================
    // PHẢN ỨNG THAY ĐỔI BỘ LỌC
    // ============================================================

    partial void OnTimKiemChanged(string value) => ApDungBoLocVaPhanTrang(datLaiTrangDau: true);

    partial void OnSelectedTrangThaiChanged(TrangThaiKhuyenMaiOption? value)
        => ApDungBoLocVaPhanTrang(datLaiTrangDau: true);

    partial void OnTuNgayChanged(DateTime? value) => ApDungBoLocVaPhanTrang(datLaiTrangDau: true);

    partial void OnDenNgayChanged(DateTime? value) => ApDungBoLocVaPhanTrang(datLaiTrangDau: true);

    partial void OnSelectedSoDongChanged(SoDongHienThiOption? value)
    {
        if (value is null)
            return;

        CurrentPage = 1;
        ApDungBoLocVaPhanTrang(datLaiTrangDau: false);
    }

    partial void OnCurrentPageChanged(int value) => CapNhatDuLieuTrangHienTai();

    /// <summary>Nhắc giao diện cập nhật lại các cờ phân trang.</summary>
    public void CapNhatChiTietPhanTrang()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PagingInfoText));
        OnPropertyChanged(nameof(CanVeTrangDau));
        OnPropertyChanged(nameof(CanVeTrangTruoc));
        OnPropertyChanged(nameof(CanVeTrangSau));
        OnPropertyChanged(nameof(CanVeTrangCuoi));
    }

    /// <summary>Thông báo lại trạng thái phân quyền sau khi phiên đăng nhập thay đổi.</summary>
    public void CapNhatPhanQuyen()
    {
        OnPropertyChanged(nameof(CanThemChuongTrinh));
    }

    // ============================================================
    // TẢI DỮ LIỆU & LỌC
    // ============================================================

    /// <summary>
    /// Tải toàn bộ chương trình khuyến mãi từ CSDL rồi áp dụng bộ lọc hiện tại.
    /// </summary>
    [RelayCommand]
    public async Task TaiDanhSachAsync()
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            DangTai = true;

            var list = await _db.GetDanhSachKhuyenMaiAsync();

            _allRawItems.Clear();
            _allRawItems.AddRange(list);

            CapNhatThongKe();
            ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách khuyến mãi", ex);
            await NotificationWindow.ShowErrorAsync(
                "Không tải được danh sách khuyến mãi: " + ex.Message,
                owner: OwnerWindow);
        }
        finally
        {
            _isLoading = false;
            DangTai = false;
        }
    }

    /// <summary>Cập nhật các thẻ thống kê ở đầu trang.</summary>
    private void CapNhatThongKe()
    {
        OnPropertyChanged(nameof(TongSoChuongTrinh));
        OnPropertyChanged(nameof(SoDangDienRa));
        OnPropertyChanged(nameof(SoSapApDung));
        OnPropertyChanged(nameof(SoKetThucNgung));
        OnPropertyChanged(nameof(TongSoHienThi));
    }

    /// <summary>
    /// Lọc dữ liệu trong bộ nhớ theo từ khóa, trạng thái và khoảng ngày,
    /// sau đó chia trang theo số dòng đang chọn.
    /// </summary>
    private void ApDungBoLocVaPhanTrang(bool datLaiTrangDau = false)
    {
        if (datLaiTrangDau)
            CurrentPage = 1;

        var keyword = (TimKiem ?? string.Empty).Trim();
        var trangThai = SelectedTrangThai?.Tag ?? "TAT_CA";
        var tuNgay = TuNgay?.Date;
        var denNgay = DenNgay?.Date;

        var query = _allRawItems.AsEnumerable();

        // 1. Tìm theo từ khóa (mã, tên hoặc ghi chú)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                (x.MaChuongTrinh?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.TenChuongTrinh?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.GhiChu?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // 2. Lọc theo trạng thái tính toán thực tế
        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            query = query.Where(x =>
                string.Equals(x.TrangThaiTinhToan, trangThai, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Lọc theo khoảng thời gian áp dụng
        if (tuNgay.HasValue)
        {
            query = query.Where(x => x.NgayKetThuc.Date >= tuNgay.Value);
        }

        if (denNgay.HasValue)
        {
            query = query.Where(x => x.NgayBatDau.Date <= denNgay.Value);
        }

        _filteredItems.Clear();
        _filteredItems.AddRange(query);

        // Đánh lại STT liên tục cho kết quả lọc
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            _filteredItems[i].Stt = i + 1;
        }

        // 4. Tính toán phân trang
        if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;

        if (CurrentPage < 1)
            CurrentPage = 1;

        CapNhatDuLieuTrangHienTai();
    }

    /// <summary>Đưa dữ liệu của trang hiện tại vào bảng và cập nhật thanh phân trang.</summary>
    private void CapNhatDuLieuTrangHienTai()
    {
        Items.Clear();

        var skip = (CurrentPage - 1) * PageSize;
        foreach (var item in _filteredItems.Skip(skip).Take(PageSize))
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(CoDuLieu));
        OnPropertyChanged(nameof(KhongCoDuLieu));
        OnPropertyChanged(nameof(KetQuaLocText));
        CapNhatChiTietPhanTrang();
    }

    /// <summary>
    /// Lấy cửa sổ chủ sở hộp thoại. Nếu chưa gán OwnerWindow thì dùng cửa sổ
    /// chính của ứng dụng để hộp thoại luôn mở đúng phía trên.
    /// </summary>
    private Window? ResolveOwner()
    {
        if (OwnerWindow is not null)
            return OwnerWindow;

        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt
            ? lt.MainWindow
            : null;
    }

    // ============================================================
    // LỆNH GIAO DIỆN
    // ============================================================

    /// <summary>Xóa toàn bộ bộ lọc đang áp dụng.</summary>
    [RelayCommand]
    public void XoaBoLoc()
    {
        TimKiem = string.Empty;
        SelectedTrangThai = TrangThaiOptions[0];
        TuNgay = null;
        DenNgay = null;
        ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
    }

    /// <summary>Mở cửa sổ thêm chương trình khuyến mãi mới.</summary>
    [RelayCommand]
    public async Task ThemChuongTrinhAsync()
    {
        var window = new ThemChuongTrinhKhuyenMaiWindow();
        var owner = ResolveOwner();

        if (owner is not null)
            await window.ShowDialog<bool>(owner);
        else
            window.Show();

        await TaiDanhSachAsync();
    }

    /// <summary>Mở cửa sổ chỉnh sửa chương trình khuyến mãi được chọn.</summary>
    [RelayCommand]
    public async Task SuaChuongTrinhAsync(ChuongTrinhKhuyenMai? item)
    {
        if (item is null)
            return;

        var window = new ThemChuongTrinhKhuyenMaiWindow(item);
        var owner = ResolveOwner();

        if (owner is not null)
            await window.ShowDialog<bool>(owner);
        else
            window.Show();

        await TaiDanhSachAsync();
    }

    /// <summary>Xóa chương trình khuyến mãi được chọn.</summary>
    [RelayCommand]
    public async Task XoaChuongTrinhAsync(ChuongTrinhKhuyenMai? item)
    {
        if (item is null)
            return;

        if (!item.CanDelete)
        {
            await NotificationWindow.ShowWarningAsync(
                "Bạn không có quyền xóa chương trình khuyến mãi này.",
                owner: OwnerWindow);
            return;
        }

        var owner = ResolveOwner();
        if (owner is null)
        {
            await NotificationWindow.ShowWarningAsync(
                "Không tìm thấy cửa sổ chính để xác nhận xóa chương trình khuyến mãi.",
                owner: OwnerWindow);
            return;
        }

        var dialog = new ConfirmationWindow(
            "Xóa chương trình khuyến mãi",
            $"Bạn có chắc chắn muốn xóa chương trình khuyến mãi [{item.MaChuongTrinh}] \"{item.TenChuongTrinh}\"?\n\nToàn bộ danh mục {item.SoLuongSanPham} sản phẩm áp dụng kèm theo cũng sẽ được gỡ bỏ.",
            "Xóa chương trình",
            "Hủy bỏ");

        if (!await dialog.ShowDialog<bool>(owner))
            return;

        try
        {
            await _db.XoaChuongTrinhKhuyenMaiAsync(item.Id);
            await TaiDanhSachAsync();
            await NotificationWindow.ShowSuccessAsync(
                $"Đã xóa thành công chương trình [{item.MaChuongTrinh}].",
                owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xóa chương trình khuyến mãi", ex);
            await NotificationWindow.ShowErrorAsync(
                "Không thể xóa chương trình khuyến mãi: " + ex.Message,
                owner: OwnerWindow);
        }
    }

    [RelayCommand]
    public void VeTrangDau()
    {
        if (CurrentPage > 1)
            CurrentPage = 1;
    }

    [RelayCommand]
    public void VeTrangTruoc()
    {
        if (CurrentPage > 1)
            CurrentPage--;
    }

    [RelayCommand]
    public void VeTrangSau()
    {
        if (CurrentPage < TotalPages)
            CurrentPage++;
    }

    [RelayCommand]
    public void VeTrangCuoi()
    {
        if (CurrentPage < TotalPages)
            CurrentPage = TotalPages;
    }
}
