using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class SoQuyDauKyWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<SoQuyDauKy> _allRawItems = new();
    private readonly ObservableCollection<SoQuyDauKy> _displayedItems = new();
    private readonly List<SoQuyLuaChon> _quyOptions = new();
    private uint _editingId;
    private bool _dangLuu;

    public SoQuyDauKyWindow()
    {
        InitializeComponent();
        DanhSachGrid.ItemsSource = _displayedItems;

        Loaded += async (_, _) => await KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        NgayDauKyPicker.SelectedDate = DateTime.Today;

        KiemTraPhanQuyen();
        await TaiQuyOptionsAsync();
        await TaiDanhSachAsync();
    }

    private void KiemTraPhanQuyen()
    {
        var coQuyen = Session.CurrentUser == null ||
                      Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookOpeningBalance) ||
                      Quyen.IsAdmin(Session.CurrentUser);

        SaveButton.IsEnabled = coQuyen;
        if (!coQuyen)
        {
            HienThongBao("Tài khoản của bạn chỉ có quyền xem. Cần quyền 'Sổ quỹ đầu kỳ' để lưu hoặc xóa dữ liệu.", isError: false);
        }
    }

    private async Task TaiQuyOptionsAsync()
    {
        try
        {
            var options = await _db.GetSoQuyQuyOptionsAsync();
            _quyOptions.Clear();
            _quyOptions.AddRange(options);

            if (_quyOptions.Count == 0)
            {
                // Mặc định các loại quỹ thông dụng
                _quyOptions.Add(new SoQuyLuaChon("TIEN_MAT", "💵 Tiền mặt"));
                _quyOptions.Add(new SoQuyLuaChon("NGAN_HANG", "💳 Tài khoản ngân hàng"));
                _quyOptions.Add(new SoQuyLuaChon("VI_DIEN_TU", "📱 Ví điện tử"));
            }
            else
            {
                // Thêm icon thân thiện cho các lựa chọn nếu chưa có
                for (int i = 0; i < _quyOptions.Count; i++)
                {
                    var opt = _quyOptions[i];
                    var upper = (opt.GiaTri ?? "").Trim().ToUpperInvariant();
                    if (!opt.HienThi.Contains("💵") && !opt.HienThi.Contains("💳") && !opt.HienThi.Contains("📱"))
                    {
                        var icon = upper switch
                        {
                            "TIEN_MAT" or "" => "💵",
                            "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "💳",
                            "VI_DIEN_TU" or "VI" => "📱",
                            _ => "💰"
                        };
                        opt.HienThi = $"{icon} {opt.HienThi}";
                    }
                }
            }

            QuyComboBox.ItemsSource = null;
            QuyComboBox.ItemsSource = _quyOptions;
            if (_quyOptions.Count > 0)
                QuyComboBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            HienThongBao("Không tải được danh mục quỹ: " + ex.Message, isError: true);
        }
    }

    private async Task TaiDanhSachAsync()
    {
        try
        {
            var list = await _db.GetDanhSachSoQuyDauKyAsync();

            _allRawItems.Clear();
            _allRawItems.AddRange(list);

            CapNhatThongKeTongQuan();
            ApDungBoLocDanhSach();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi TaiDanhSachAsync SoQuyDauKy", ex);
            HienThongBao("Không tải được danh sách số dư đầu kỳ: " + ex.Message, isError: true);
        }
    }

    private void CapNhatThongKeTongQuan()
    {
        decimal tongTienMat = 0m;
        decimal tongNganHang = 0m;
        decimal tongCong = 0m;

        foreach (var item in _allRawItems)
        {
            tongCong += item.SoTien;
            var maQuy = (item.PhuongThuc ?? "").Trim().ToUpperInvariant();
            if (maQuy is "TIEN_MAT" or "")
            {
                tongTienMat += item.SoTien;
            }
            else if (maQuy is "NGAN_HANG" or "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG")
            {
                tongNganHang += item.SoTien;
            }
        }

        TongTienMatText.Text = $"{tongTienMat:N0} đ";
        TongNganHangText.Text = $"{tongNganHang:N0} đ";
        TongCongText.Text = $"{tongCong:N0} đ";
    }

    private void ApDungBoLocDanhSach()
    {
        var search = (SearchTextBox.Text ?? "").Trim();
        var query = _allRawItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                (x.TenQuy?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.NoiDung?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.PhuongThuc?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var resultList = query.ToList();

        _displayedItems.Clear();
        for (int i = 0; i < resultList.Count; i++)
        {
            resultList[i].Stt = i + 1;
            _displayedItems.Add(resultList[i]);
        }

        SoBanGhiText.Text = $"{_displayedItems.Count:N0} bản ghi";
        EmptyStateBorder.IsVisible = _displayedItems.Count == 0;
        DanhSachGrid.IsVisible = _displayedItems.Count > 0;
    }

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApDungBoLocDanhSach();
    }

    private async void LamMoiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await TaiDanhSachAsync();
    }

    private void SoTienTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var tien = DocTien(SoTienTextBox.Text);
        SoTienPreviewText.Text = $"{tien:N0} đ";
    }

    private void SuaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SoQuyDauKy item)
            return;

        AnThongBao();

        _editingId = item.Id;
        FormTitle.Text = "CHỈNH SỬA SỐ DƯ ĐẦU KỲ";
        NgayDauKyPicker.SelectedDate = item.NgayDauKy;
        SoTienTextBox.Text = item.SoTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        NoiDungTextBox.Text = item.NoiDung ?? "";

        var maQuy = (item.PhuongThuc ?? "").Trim().ToUpperInvariant();
        var option = _quyOptions.FirstOrDefault(o => string.Equals(o.GiaTri, maQuy, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            option = new SoQuyLuaChon(maQuy, SoQuyDauKy.TenHienThiQuy(maQuy));
            _quyOptions.Add(option);
            QuyComboBox.ItemsSource = null;
            QuyComboBox.ItemsSource = _quyOptions;
        }
        QuyComboBox.SelectedItem = option;
        SaveButton.Content = "💾  Cập nhật số dư";
    }

    private async void XoaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SoQuyDauKy item)
            return;

        if (!item.CanDelete)
        {
            await NotificationWindow.ShowWarningAsync("Bạn không có quyền xóa số dư quỹ đầu kỳ.", owner: this);
            return;
        }

        var dialog = new ConfirmationWindow(
            "Xóa số dư đầu kỳ",
            $"Bạn có chắc chắn muốn xóa bản ghi số dư quỹ đầu kỳ:\n• Quỹ: {item.TenQuy}\n• Ngày: {item.NgayDauKyHienThi}\n• Số tiền: {item.SoTienHienThi}\n\nThao tác này sẽ cập nhật lại số dư ban đầu khi kết chuyển vào sổ quỹ.",
            "Xóa số dư",
            "Hủy bỏ");

        if (!await dialog.ShowDialog<bool>(this))
            return;

        try
        {
            await _db.XoaSoQuyDauKyAsync(item.Id);

            if (_editingId == item.Id)
                DatLaiForm();

            await TaiDanhSachAsync();
            HienThongBao($"Đã xóa số dư {item.SoTienHienThi} của quỹ {item.TenQuy}.", isError: false);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xóa số dư đầu kỳ", ex);
            HienThongBao("Xóa thất bại: " + ex.Message, isError: true);
        }
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangLuu)
            return;

        AnThongBao();

        // 1. Kiểm tra ngày
        if (!NgayDauKyPicker.SelectedDate.HasValue)
        {
            HienThongBao("Vui lòng chọn ngày chốt số dư đầu kỳ.", isError: true);
            NgayDauKyPicker.Focus();
            return;
        }

        // 2. Kiểm tra quỹ
        if (QuyComboBox.SelectedItem is not SoQuyLuaChon quy)
        {
            HienThongBao("Vui lòng chọn quỹ nhận số dư đầu kỳ.", isError: true);
            QuyComboBox.Focus();
            return;
        }

        // 3. Kiểm tra số tiền
        var rawTien = SoTienTextBox.Text;
        if (string.IsNullOrWhiteSpace(rawTien))
        {
            HienThongBao("Vui lòng nhập số tiền đầu kỳ.", isError: true);
            SoTienTextBox.Focus();
            return;
        }

        var soTien = DocTien(rawTien);
        if (soTien < 0)
        {
            HienThongBao("Số tiền đầu kỳ không được là số âm.", isError: true);
            SoTienTextBox.Focus();
            return;
        }

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;

        var item = new SoQuyDauKy
        {
            Id = _editingId,
            NgayDauKy = NgayDauKyPicker.SelectedDate.Value.Date,
            PhuongThuc = quy.GiaTri,
            SoTien = soTien,
            NoiDung = (NoiDungTextBox.Text ?? "").Trim(),
            ChiNhanhId = branchId
        };

        try
        {
            _dangLuu = true;
            SaveButton.IsEnabled = false;
            SaveButton.Content = "⏳ Đang lưu...";

            await _db.LuuSoQuyDauKyAsync(item);

            DatLaiForm();
            await TaiDanhSachAsync();
            HienThongBao($"Đã lưu thành công số dư {soTien:N0} đ cho quỹ {quy.HienThi}.", isError: false);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi lưu số dư đầu kỳ", ex);
            HienThongBao("Lưu thất bại: " + ex.Message, isError: true);
        }
        finally
        {
            _dangLuu = false;
            SaveButton.IsEnabled = true;
            SaveButton.Content = _editingId > 0 ? "💾  Cập nhật số dư" : "💾  Lưu số dư đầu kỳ";
        }
    }

    private void NewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AnThongBao();
        DatLaiForm();
    }

    private void DatLaiForm()
    {
        _editingId = 0;
        FormTitle.Text = "THÊM SỐ DƯ ĐẦU KỲ";
        NgayDauKyPicker.SelectedDate = DateTime.Today;
        SoTienTextBox.Text = string.Empty;
        SoTienPreviewText.Text = "0 đ";
        NoiDungTextBox.Text = string.Empty;
        SaveButton.Content = "💾  Lưu số dư đầu kỳ";
        if (_quyOptions.Count > 0)
            QuyComboBox.SelectedIndex = 0;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private static decimal DocTien(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0m;

        var cleaned = raw.Replace(".", "")
                         .Replace(",", "")
                         .Replace(" ", "")
                         .Replace("đ", "")
                         .Replace("₫", "")
                         .Replace("VNĐ", "", StringComparison.OrdinalIgnoreCase)
                         .Trim();

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private void HienThongBao(string message, bool isError)
    {
        NotificationBorder.IsVisible = true;
        NotificationTextBlock.Text = message;

        if (isError)
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#FEF2F2"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FECACA"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#991B1B"));
        }
        else
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#F0FDF4"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#BBF7D0"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#166534"));
        }
    }

    private void AnThongBao()
    {
        NotificationBorder.IsVisible = false;
        NotificationTextBlock.Text = "";
    }
}
