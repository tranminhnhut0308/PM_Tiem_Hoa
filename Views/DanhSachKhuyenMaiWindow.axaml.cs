using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly List<ChuongTrinhKhuyenMai> _allRawItems = new();
    private readonly List<ChuongTrinhKhuyenMai> _filteredItems = new();
    private readonly ObservableCollection<ChuongTrinhKhuyenMai> _pagedItems = new();

    private bool _daKhoiTao;
    private bool _isLoading;
    private int _currentPage = 1;
    private int _pageSize = 15;
    private int _totalPages = 1;

    public DanhSachKhuyenMaiWindow()
    {
        InitializeComponent();
        DanhSachGrid.ItemsSource = _pagedItems;

        Loaded += async (_, _) =>
        {
            _daKhoiTao = true;
            KiemTraPhanQuyen();
            await TaiDanhSachTuDatabaseAsync();
        };
    }

    private void KiemTraPhanQuyen()
    {
        var canCreate = Session.CurrentUser == null ||
                        Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsCreate) ||
                        Quyen.IsAdmin(Session.CurrentUser);
        ThemButton.IsVisible = canCreate;
    }

    /// <summary>
    /// Tải toàn bộ dữ liệu từ CSDL và tính toán thống kê thẻ trên cùng.
    /// </summary>
    private async Task TaiDanhSachTuDatabaseAsync()
    {
        if (!_daKhoiTao || _isLoading)
            return;

        try
        {
            _isLoading = true;
            LoadingBorder.IsVisible = true;

            var list = await _db.GetDanhSachKhuyenMaiAsync();

            _allRawItems.Clear();
            _allRawItems.AddRange(list);

            CapNhatThongKeThe();
            ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách khuyến mãi", ex);
            await NotificationWindow.ShowErrorAsync("Không tải được danh sách khuyến mãi: " + ex.Message, owner: this);
        }
        finally
        {
            _isLoading = false;
            LoadingBorder.IsVisible = false;
        }
    }

    /// <summary>
    /// Cập nhật số lượng trên các thẻ thống kê tổng quan.
    /// </summary>
    private void CapNhatThongKeThe()
    {
        var tongSo = _allRawItems.Count;
        var dangDienRa = _allRawItems.Count(x => x.TrangThaiTinhToan == "DANG_HOAT_DONG");
        var sapApDung = _allRawItems.Count(x => x.TrangThaiTinhToan == "CHUA_BAT_DAU");
        var ketThucNgung = _allRawItems.Count(x => x.TrangThaiTinhToan is "DA_KET_THUC" or "TAM_NGUNG");

        TongSoTopText.Text = $"{tongSo:N0} chương trình";
        DangDienRaCountText.Text = dangDienRa.ToString("N0");
        SapApDungCountText.Text = sapApDung.ToString("N0");
        KetThucNgungCountText.Text = ketThucNgung.ToString("N0");
    }

    /// <summary>
    /// Lọc dữ liệu trong bộ nhớ theo từ khóa, trạng thái và khoảng ngày.
    /// </summary>
    private void ApDungBoLocVaPhanTrang(bool datLaiTrangDau = false)
    {
        if (!_daKhoiTao)
            return;

        if (datLaiTrangDau)
            _currentPage = 1;

        var keyword = (SearchTextBox.Text ?? "").Trim();
        var trangThai = LayTrangThaiDangChon();
        var tuNgay = TuNgayPicker.SelectedDate?.Date;
        var denNgay = DenNgayPicker.SelectedDate?.Date;

        var query = _allRawItems.AsEnumerable();

        // 1. Tìm theo từ khóa (Mã hoặc Tên)
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
            query = query.Where(x => string.Equals(x.TrangThaiTinhToan, trangThai, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Lọc theo thời gian áp dụng
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

        KetQuaLocText.Text = $"{_filteredItems.Count:N0} chương trình";

        // 4. Tính toán phân trang
        _totalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredItems.Count / _pageSize));
        if (_currentPage > _totalPages)
            _currentPage = _totalPages;

        CapNhatDuLieuTrangHienTai();
    }

    private void CapNhatDuLieuTrangHienTai()
    {
        _pagedItems.Clear();

        var skip = (_currentPage - 1) * _pageSize;
        var itemsForPage = _filteredItems.Skip(skip).Take(_pageSize);

        foreach (var item in itemsForPage)
        {
            _pagedItems.Add(item);
        }

        // Cập nhật giao diện thanh phân trang
        PagingInfoText.Text = $"Trang {_currentPage} / {_totalPages} (Tổng {_filteredItems.Count:N0} chương trình)";

        FirstPageButton.IsEnabled = _currentPage > 1;
        PrevPageButton.IsEnabled = _currentPage > 1;
        NextPageButton.IsEnabled = _currentPage < _totalPages;
        LastPageButton.IsEnabled = _currentPage < _totalPages;

        // Trạng thái trống
        EmptyStateBorder.IsVisible = _filteredItems.Count == 0 && !_isLoading;
        DanhSachGrid.IsVisible = _filteredItems.Count > 0;
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

    // ==========================================
    // SỰ KIỆN TÌM KIẾM & BỘ LỌC
    // ==========================================

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
    }

    private void FilterComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
    }

    private void DateFilter_OnDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
    }

    private void XoaBoLocButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        TrangThaiComboBox.SelectedIndex = 0;
        TuNgayPicker.SelectedDate = null;
        DenNgayPicker.SelectedDate = null;
        ApDungBoLocVaPhanTrang(datLaiTrangDau: true);
    }

    private async void LamMoiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await TaiDanhSachTuDatabaseAsync();
    }

    // ==========================================
    // THAO TÁC DỮ LIỆU (THÊM / SỬA / XÓA)
    // ==========================================

    private async void ThemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var window = new ThemChuongTrinhKhuyenMaiWindow();
        await window.ShowDialog<bool>(this);
        await TaiDanhSachTuDatabaseAsync();
    }

    private async void SuaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ChuongTrinhKhuyenMai item)
            return;

        var window = new ThemChuongTrinhKhuyenMaiWindow(item);
        await window.ShowDialog<bool>(this);
        await TaiDanhSachTuDatabaseAsync();
    }

    private async void XoaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ChuongTrinhKhuyenMai item)
            return;

        if (!item.CanDelete)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền xóa chương trình khuyến mãi này.", owner: this);
            return;
        }

        var dialog = new ConfirmationWindow(
            "Xóa chương trình khuyến mãi",
            $"Bạn có chắc chắn muốn xóa chương trình khuyến mãi [{item.MaChuongTrinh}] \"{item.TenChuongTrinh}\"?\n\nToàn bộ danh mục {item.SoLuongSanPham} sản phẩm áp dụng kèm theo cũng sẽ được gỡ bỏ.",
            "Xóa chương trình",
            "Hủy bỏ");

        if (!await dialog.ShowDialog<bool>(this))
            return;

        try
        {
            await _db.XoaChuongTrinhKhuyenMaiAsync(item.Id);
            await TaiDanhSachTuDatabaseAsync();
            await NotificationWindow.ShowSuccessAsync($"Đã xóa thành công chương trình [{item.MaChuongTrinh}].", owner: this);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xóa chương trình khuyến mãi", ex);
            await NotificationWindow.ShowErrorAsync("Không thể xóa chương trình khuyến mãi: " + ex.Message, owner: this);
        }
    }

    // ==========================================
    // SỰ KIỆN PHÂN TRANG
    // ==========================================

    private void PageSizeComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PageSizeComboBox.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out var size))
        {
            _pageSize = size > 0 ? size : 15;
            _totalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredItems.Count / _pageSize));
            _currentPage = 1;
            CapNhatDuLieuTrangHienTai();
        }
    }

    private void FirstPageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage = 1;
            CapNhatDuLieuTrangHienTai();
        }
    }

    private void PrevPageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            CapNhatDuLieuTrangHienTai();
        }
    }

    private void NextPageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            CapNhatDuLieuTrangHienTai();
        }
    }

    private void LastPageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage = _totalPages;
            CapNhatDuLieuTrangHienTai();
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
