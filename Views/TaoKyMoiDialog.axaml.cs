using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class TaoKyMoiDialog : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<KyKeToan> _danhSachKyCu = new();

    public KyKeToan? KetQuaKyMoi { get; private set; }

    public TaoKyMoiDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) => await KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        try
        {
            // Nạp danh sách tháng 1..12
            var thangs = Enumerable.Range(1, 12).Select(m => $"Tháng {m:D2}").ToList();
            ThangComboBox.ItemsSource = thangs;

            // Nạp danh sách năm (năm hiện tại - 1 đến năm hiện tại + 3)
            var currentYear = DateTime.Today.Year;
            var nams = Enumerable.Range(currentYear - 1, 5).ToList();
            NamComboBox.ItemsSource = nams;

            // Mặc định chọn tháng kế tiếp hoặc tháng hiện tại
            var nextMonth = DateTime.Today.AddMonths(1);
            ThangComboBox.SelectedIndex = nextMonth.Month - 1;
            NamComboBox.SelectedItem = nextMonth.Year;

            CapNhatTheoThangNam();

            // Tải danh sách kỳ cũ để kế thừa
            var kyList = await _db.GetDanhSachKyKeToanAsync();
            _danhSachKyCu.Clear();
            _danhSachKyCu.AddRange(kyList);

            KyTruocComboBox.ItemsSource = _danhSachKyCu;
            if (_danhSachKyCu.Count > 0)
            {
                KyTruocComboBox.SelectedIndex = 0; // Kỳ gần nhất
            }
            else
            {
                KeThuaTonCheckBox.IsChecked = false;
                KeThuaTonCheckBox.IsEnabled = false;
                KyTruocPanel.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khởi tạo TaoKyMoiDialog", ex);
        }
    }

    private void CapNhatTheoThangNam()
    {
        if (ThangComboBox.SelectedIndex < 0 || NamComboBox.SelectedItem is not int nam)
            return;

        int thang = ThangComboBox.SelectedIndex + 1;
        var dauThang = new DateTime(nam, thang, 1);
        var cuoiThang = dauThang.AddMonths(1).AddDays(-1);

        NgayBatDauPicker.SelectedDate = dauThang;
        NgayKetThucPicker.SelectedDate = cuoiThang;
        TenKyTextBox.Text = $"Tháng {thang:D2} Năm {nam}";
        MaKyTextBox.Text = $"KY_{thang:D2}_{nam}";
    }

    private void ThangNam_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TheoThangRadio.IsChecked == true)
        {
            CapNhatTheoThangNam();
        }
    }

    private void LoaiKyRadio_OnChecked(object? sender, RoutedEventArgs e)
    {
        bool isTheoThang = TheoThangRadio.IsChecked == true;
        if (ThangPanel != null)
        {
            ThangPanel.IsVisible = isTheoThang;
        }

        if (isTheoThang)
        {
            CapNhatTheoThangNam();
        }
        else
        {
            if (NgayBatDauPicker != null && !NgayBatDauPicker.SelectedDate.HasValue)
                NgayBatDauPicker.SelectedDate = DateTime.Today;

            if (NgayKetThucPicker != null && !NgayKetThucPicker.SelectedDate.HasValue)
                NgayKetThucPicker.SelectedDate = DateTime.Today.AddDays(7);

            if (TenKyTextBox != null && string.IsNullOrWhiteSpace(TenKyTextBox.Text))
                TenKyTextBox.Text = $"Đợt bán hàng {DateTime.Today:dd/MM/yyyy}";

            if (MaKyTextBox != null)
                MaKyTextBox.Text = $"KY_{DateTime.Today:yyyyMMdd}";
        }
    }

    private void KeThuaTonCheckBox_OnCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (KyTruocPanel != null)
        {
            KyTruocPanel.IsVisible = KeThuaTonCheckBox.IsChecked == true;
        }
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ErrorTextBlock.IsVisible = false;

        var tenKy = (TenKyTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tenKy))
        {
            HienLoi("Vui lòng nhập tên kỳ kinh doanh.");
            TenKyTextBox.Focus();
            return;
        }

        if (!NgayBatDauPicker.SelectedDate.HasValue)
        {
            HienLoi("Vui lòng chọn ngày bắt đầu kỳ.");
            NgayBatDauPicker.Focus();
            return;
        }

        if (!NgayKetThucPicker.SelectedDate.HasValue)
        {
            HienLoi("Vui lòng chọn ngày kết thúc kỳ.");
            NgayKetThucPicker.Focus();
            return;
        }

        var ngayBatDau = NgayBatDauPicker.SelectedDate.Value.Date;
        var ngayKetThuc = NgayKetThucPicker.SelectedDate.Value.Date;

        if (ngayKetThuc < ngayBatDau)
        {
            HienLoi("Ngày kết thúc không được trước ngày bắt đầu.");
            NgayKetThucPicker.Focus();
            return;
        }

        var maKy = (MaKyTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(maKy))
        {
            maKy = $"KY_{ngayBatDau:yyyyMMdd}";
        }

        bool keThua = KeThuaTonCheckBox.IsChecked == true;
        uint? kyTruocId = null;
        if (keThua && KyTruocComboBox.SelectedItem is KyKeToan selectedOldKy)
        {
            kyTruocId = selectedOldKy.Id;
        }

        var ghiChu = (GhiChuTextBox.Text ?? "").Trim();

        try
        {
            SaveButton.IsEnabled = false;
            SaveButton.Content = "⏳ Đang tạo kỳ...";

            KetQuaKyMoi = await _db.TaoKyMoiAsync(
                maKy,
                tenKy,
                ngayBatDau,
                ngayKetThuc,
                keThua,
                kyTruocId,
                ghiChu);

            Close(true);
        }
        catch (Exception ex)
        {
            HienLoi("Không thể tạo kỳ mới: " + ex.Message);
            SaveButton.IsEnabled = true;
            SaveButton.Content = "✓ Tạo & Mở kỳ mới";
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void HienLoi(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }
}
