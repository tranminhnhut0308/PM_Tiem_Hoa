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
    private readonly ObservableCollection<SoQuyDauKy> _items = new();
    private readonly List<SoQuyLuaChon> _quyOptions = new();
    private uint _editingId;
    private bool _dangLuu;

    public SoQuyDauKyWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        NgayDauKyPicker.SelectedDate = DateTime.Today;

        await TaiQuyOptionsAsync();
        await TaiDanhSachAsync();

        DanhSachGrid.ItemsSource = _items;
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
                // Chưa có phiếu thu/chi nào nên bảng phieu_chi chưa có phương thức.
                // Bổ sung danh mục quỹ mặc định để người dùng khai báo được ngay.
                _quyOptions.Add(new SoQuyLuaChon("TIEN_MAT", "Tiền mặt"));
                _quyOptions.Add(new SoQuyLuaChon("NGAN_HANG", "Ngân hàng"));
                _quyOptions.Add(new SoQuyLuaChon("VI_DIEN_TU", "Ví điện tử"));
            }

            QuyComboBox.ItemsSource = _quyOptions;
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
            var search = (SearchTextBox.Text ?? "").Trim();
            var list = await _db.GetDanhSachSoQuyDauKyAsync(string.IsNullOrWhiteSpace(search) ? null : search);

            _items.Clear();
            foreach (var item in list)
                _items.Add(item);

            TongCongText.Text = $"{_items.Sum(x => x.SoTien):N0} đ";
        }
        catch (Exception ex)
        {
            HienThongBao("Không tải được danh sách số dư đầu kỳ: " + ex.Message, isError: true);
        }
    }

    private async void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        await TaiDanhSachAsync();
    }

    private void SuaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SoQuyDauKy item)
            return;

        AnThongBao();

        _editingId = item.Id;
        FormTitle.Text = "CHỈNH SỬA SỐ DƯ ĐẦU KỲ";
        NgayDauKyPicker.SelectedDate = item.NgayDauKy;
        SoTienTextBox.Text = item.SoTien.ToString("0.##", CultureInfo.InvariantCulture);
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
    }

    private async void XoaRow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SoQuyDauKy item)
            return;

        var dialog = new ConfirmationWindow(
            "Xóa số dư đầu kỳ",
            $"Bạn có chắc muốn xóa số dư {item.SoTienHienThi} của quỹ {item.TenQuy} ngày {item.NgayDauKyHienThi}?",
            "Xóa",
            "Hủy");

        if (!await dialog.ShowDialog<bool>(this))
            return;

        try
        {
            await _db.XoaSoQuyDauKyAsync(item.Id);

            if (_editingId == item.Id)
                DatLaiForm();

            await TaiDanhSachAsync();
            HienThongBao("Đã xóa số dư đầu kỳ.", isError: false);
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

        if (!NgayDauKyPicker.SelectedDate.HasValue)
        {
            HienThongBao("Vui lòng chọn ngày đầu kỳ.", isError: true);
            NgayDauKyPicker.Focus();
            return;
        }

        if (QuyComboBox.SelectedItem is not SoQuyLuaChon quy)
        {
            HienThongBao("Vui lòng chọn quỹ cho số dư đầu kỳ.", isError: true);
            QuyComboBox.Focus();
            return;
        }

        var soTien = DocTien(SoTienTextBox.Text);
        if (soTien < 0)
        {
            HienThongBao("Số tiền đầu kỳ không được âm.", isError: true);
            SoTienTextBox.Focus();
            return;
        }

        var item = new SoQuyDauKy
        {
            Id = _editingId,
            NgayDauKy = NgayDauKyPicker.SelectedDate.Value.Date,
            PhuongThuc = quy.GiaTri,
            SoTien = soTien,
            NoiDung = (NoiDungTextBox.Text ?? "").Trim()
        };

        try
        {
            _dangLuu = true;
            SaveButton.IsEnabled = false;
            SaveButton.Content = "⏳ Đang lưu...";

            await _db.LuuSoQuyDauKyAsync(item);

            DatLaiForm();
            await TaiDanhSachAsync();
            HienThongBao("Đã lưu số dư quỹ đầu kỳ thành công.", isError: false);
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
            SaveButton.Content = "💾  Lưu số dư";
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
        SoTienTextBox.Text = "";
        NoiDungTextBox.Text = "";
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

        var cleaned = raw.Replace(".", "").Replace(",", "").Replace(" ", "").Replace("đ", "").Replace("₫", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private void HienThongBao(string message, bool isError)
    {
        NotificationBorder.IsVisible = true;
        NotificationTextBlock.Text = message;

        if (isError)
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#FEE2E2"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FCA5A5"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#991B1B"));
        }
        else
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#ECFDF5"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#A7F3D0"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#065F46"));
        }
    }

    private void AnThongBao()
    {
        NotificationBorder.IsVisible = false;
        NotificationTextBlock.Text = "";
    }
}
