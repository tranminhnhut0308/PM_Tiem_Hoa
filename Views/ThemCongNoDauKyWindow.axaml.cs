using System;
using System.Collections.Generic;
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

public partial class ThemCongNoDauKyWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<DoiTacOption> _danhSachDoiTac = new();
    private DoiTacOption? _doiTacDaChon;
    private bool _dangLuu;
    private bool _dangFormatTien;
    private bool _dangTaiDuLieu;

    /// <summary>
    /// Đối tượng công nợ đầu kỳ vừa được lưu thành công (nếu có).
    /// </summary>
    public ChiTietCongNoDauKy? KetQuaLuu { get; private set; }

    public ThemCongNoDauKyWindow() : this(null, null)
    {
    }

    public ThemCongNoDauKyWindow(string? loaiDoiTacMacDinh = null, uint? doiTacIdMacDinh = null)
    {
        InitializeComponent();

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync(loaiDoiTacMacDinh, doiTacIdMacDinh);
    }

    private async Task KhoiTaoDuLieuAsync(string? loaiDoiTacMacDinh, uint? doiTacIdMacDinh)
    {
        try
        {
            // 1. Mặc định ngày đầu kỳ là ngày hôm nay
            NgayDauKyPicker.SelectedDate = DateTime.Today;

            // 2. Chọn loại đối tác mặc định
            var loai = string.Equals(loaiDoiTacMacDinh, "NHA_CUNG_CAP", StringComparison.OrdinalIgnoreCase)
                ? "NHA_CUNG_CAP"
                : "KHACH_HANG";

            for (int i = 0; i < LoaiDoiTacComboBox.ItemCount; i++)
            {
                if (LoaiDoiTacComboBox.Items[i] is ComboBoxItem cbi && 
                    string.Equals(cbi.Tag?.ToString(), loai, StringComparison.OrdinalIgnoreCase))
                {
                    LoaiDoiTacComboBox.SelectedIndex = i;
                    break;
                }
            }

            // 3. Tải danh sách đối tác theo loại đã chọn
            await TaiDanhSachDoiTacAsync(loai, doiTacIdMacDinh);
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể khởi tạo dữ liệu: " + ex.Message, isError: true);
        }
    }

    private string LayLoaiDoiTacHienTai()
    {
        if (LoaiDoiTacComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            return item.Tag.ToString() ?? "KHACH_HANG";
        }
        return "KHACH_HANG";
    }

    private async Task TaiDanhSachDoiTacAsync(string loaiDoiTac, uint? doiTacIdMacDinh = null)
    {
        if (_dangTaiDuLieu)
            return;

        _dangTaiDuLieu = true;
        try
        {
            var ds = await _db.GetDanhSachDoiTacCongNoAsync(loaiDoiTac);

            _danhSachDoiTac.Clear();
            _danhSachDoiTac.AddRange(ds);

            MaDoiTacAutoCompleteBox.ItemsSource = null;
            MaDoiTacAutoCompleteBox.ItemsSource = _danhSachDoiTac;

            // Cấu hình bộ lọc tìm kiếm theo Mã đối tác, Tên đối tác hoặc Số điện thoại
            MaDoiTacAutoCompleteBox.ItemFilter = (search, item) =>
            {
                if (string.IsNullOrWhiteSpace(search))
                    return true;

                if (item is DoiTacOption dt)
                {
                    var ma = dt.MaDoiTac ?? "";
                    var ten = dt.TenDoiTac ?? "";
                    var sdt = dt.SoDienThoai ?? "";

                    return ma.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           ten.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           sdt.Contains(search, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            };

            // Nếu có đối tác mặc định cần chọn
            if (doiTacIdMacDinh.HasValue && doiTacIdMacDinh.Value > 0)
            {
                var dt = _danhSachDoiTac.FirstOrDefault(d => d.Id == doiTacIdMacDinh.Value);
                if (dt != null)
                {
                    await ChonDoiTacAsync(dt);
                }
            }
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể tải danh sách đối tác: " + ex.Message, isError: true);
        }
        finally
        {
            _dangTaiDuLieu = false;
        }
    }

    private async void LoaiDoiTacComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Khi người dùng chuyển đổi loại đối tác, xóa sạch lựa chọn đối tác cũ và tải danh sách mới
        _doiTacDaChon = null;
        if (MaDoiTacAutoCompleteBox != null)
            MaDoiTacAutoCompleteBox.Text = "";
        if (TenDoiTacTextBox != null)
            TenDoiTacTextBox.Text = "";

        AnThongBao();

        var loai = LayLoaiDoiTacHienTai();
        await TaiDanhSachDoiTacAsync(loai);
    }

    private async Task ChonDoiTacAsync(DoiTacOption dt)
    {
        _doiTacDaChon = dt;
        MaDoiTacAutoCompleteBox.Text = dt.MaDoiTac;
        TenDoiTacTextBox.Text = dt.TenDoiTac;

        AnThongBao();

        // Kiểm tra xem đối tác này đã có bản ghi nợ đầu kỳ lưu trước đó hay chưa
        try
        {
            var ngayDauKy = NgayDauKyPicker.SelectedDate ?? DateTime.Today;
            var banGhiCu = await _db.LayCongNoDauKyCuaDoiTacAsync(dt.LoaiDoiTac, dt.Id, ngayDauKy);

            if (banGhiCu != null)
            {
                // Tải lại dữ liệu đã khai báo trước đó để người dùng xem hoặc cập nhật an toàn
                _dangFormatTien = true;
                SoTienTextBox.Text = MoneyInput.Format(banGhiCu.SoTien);
                _dangFormatTien = false;

                NgayDauKyPicker.SelectedDate = banGhiCu.NgayDauKy;
                NgayHenTraPicker.SelectedDate = banGhiCu.NgayHenTra;

                if (!string.IsNullOrWhiteSpace(banGhiCu.NoiDung))
                    NoiDungTextBox.Text = banGhiCu.NoiDung;

                HienThongBao($"ℹ️ Đối tác đã có số dư nợ đầu kỳ: {MoneyInput.Format(banGhiCu.SoTien)} đ. Bạn có thể cập nhật lại số tiền tại đây mà không bị cộng dồn.", isError: false);
            }
            else
            {
                // Nếu chưa có nợ đầu kỳ, nếu số tiền đang là 0 và đối tác đang có công nợ hiện tại, có thể thông báo nhẹ
                if (dt.CongNoHienTai != 0)
                {
                    HienThongBao($"ℹ️ Đối tác hiện có công nợ ghi nhận trên hệ thống là {MoneyInput.Format(dt.CongNoHienTai)} đ.", isError: false);
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khi kiểm tra nợ đầu kỳ cũ của đối tác", ex);
        }
    }

    private async void MaDoiTacAutoCompleteBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MaDoiTacAutoCompleteBox.SelectedItem is DoiTacOption dt)
        {
            await ChonDoiTacAsync(dt);
        }
    }

    private async void MaDoiTacAutoCompleteBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var text = (MaDoiTacAutoCompleteBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _doiTacDaChon = null;
            TenDoiTacTextBox.Text = "";
            AnThongBao();
            return;
        }

        // Nếu mã đang nhập khớp với đối tác đã chọn
        if (_doiTacDaChon != null && string.Equals(_doiTacDaChon.MaDoiTac, text, StringComparison.OrdinalIgnoreCase))
        {
            AnThongBao();
            return;
        }

        // Tìm kiếm chính xác theo mã hoặc tên trong danh mục
        var khop = _danhSachDoiTac.FirstOrDefault(d =>
            string.Equals(d.MaDoiTac, text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.TenDoiTac, text, StringComparison.OrdinalIgnoreCase));

        if (khop != null)
        {
            await ChonDoiTacAsync(khop);
        }
        else
        {
            _doiTacDaChon = null;
            TenDoiTacTextBox.Text = "";
            HienThongBao("Đối tác không tồn tại hoặc không khớp loại đã chọn. Vui lòng chọn đối tác từ danh sách gợi ý.", isError: true);
        }
    }

    private void SoTienTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dangFormatTien)
            return;

        FormatTienRealtime();
    }

    private void SoTienTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        FormatTienRealtime();
    }

    private void FormatTienRealtime()
    {
        if (_dangFormatTien)
            return;

        _dangFormatTien = true;
        try
        {
            var raw = (SoTienTextBox.Text ?? "").Trim();
            var tien = DocTien(raw);

            if (tien < 0)
                tien = 0;

            SoTienTextBox.Text = MoneyInput.Format(tien);
        }
        finally
        {
            _dangFormatTien = false;
        }
    }

    private static decimal DocTien(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        if (MoneyInput.TryParse(text, batBuoc: false, out var val, out _))
            return val;

        var clean = text.Replace(".", "")
                        .Replace(",", "")
                        .Replace(" ", "")
                        .Replace("đ", "")
                        .Replace("₫", "")
                        .Trim();

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var fallbackVal))
        {
            return fallbackVal;
        }

        return 0m;
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangLuu)
            return;

        AnThongBao();

        // 1. Kiểm tra ngày đầu kỳ
        if (!NgayDauKyPicker.SelectedDate.HasValue)
        {
            HienThongBao("Vui lòng chọn ngày đầu kỳ hợp lệ.", isError: true);
            NgayDauKyPicker.Focus();
            return;
        }

        // 2. Kiểm tra đối tác
        if (_doiTacDaChon == null)
        {
            var text = (MaDoiTacAutoCompleteBox.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var khop = _danhSachDoiTac.FirstOrDefault(d =>
                    string.Equals(d.MaDoiTac, text, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.TenDoiTac, text, StringComparison.OrdinalIgnoreCase));

                if (khop != null)
                {
                    _doiTacDaChon = khop;
                    TenDoiTacTextBox.Text = khop.TenDoiTac;
                }
            }

            if (_doiTacDaChon == null)
            {
                HienThongBao("Vui lòng chọn đối tác hợp lệ từ danh sách gợi ý.", isError: true);
                MaDoiTacAutoCompleteBox.Focus();
                return;
            }
        }

        // 3. Kiểm tra số tiền
        var soTien = DocTien(SoTienTextBox.Text);
        if (soTien < 0)
        {
            HienThongBao("Số tiền công nợ không được âm.", isError: true);
            SoTienTextBox.Focus();
            return;
        }

        // 4. Kiểm tra ngày hẹn trả (nếu có thì không được nhỏ hơn ngày đầu kỳ)
        if (NgayHenTraPicker.SelectedDate.HasValue && 
            NgayHenTraPicker.SelectedDate.Value.Date < NgayDauKyPicker.SelectedDate.Value.Date)
        {
            HienThongBao("Ngày hẹn trả không thể trước ngày đầu kỳ.", isError: true);
            NgayHenTraPicker.Focus();
            return;
        }

        // 5. Chuẩn bị dữ liệu lưu
        var loaiDoiTac = LayLoaiDoiTacHienTai();
        var noiDung = string.IsNullOrWhiteSpace(NoiDungTextBox.Text) ? "Nợ đầu kỳ" : NoiDungTextBox.Text.Trim();

        var item = new ChiTietCongNoDauKy
        {
            LoaiDoiTac = loaiDoiTac,
            DoiTacId = _doiTacDaChon.Id,
            MaDoiTac = _doiTacDaChon.MaDoiTac,
            TenDoiTac = _doiTacDaChon.TenDoiTac,
            SoDienThoai = _doiTacDaChon.SoDienThoai,
            SoTien = soTien,
            NgayDauKy = NgayDauKyPicker.SelectedDate.Value,
            NgayHenTra = NgayHenTraPicker.SelectedDate,
            NoiDung = noiDung
        };

        // 6. Thực hiện lưu bằng transaction an toàn
        try
        {
            _dangLuu = true;
            SaveButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            SaveButton.Content = "⏳ Đang lưu...";

            var thanhCong = await _db.LuuCongNoDauKyAsync(item);

            if (thanhCong)
            {
                KetQuaLuu = item;
                Close(true);
            }
            else
            {
                HienThongBao("Không thể lưu công nợ đầu kỳ vào cơ sở dữ liệu. Vui lòng thử lại.", isError: true);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lưu công nợ đầu kỳ", ex);
            HienThongBao("Lỗi khi lưu công nợ đầu kỳ: " + ex.Message, isError: true);
        }
        finally
        {
            _dangLuu = false;
            SaveButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            SaveButton.Content = "💾  Lưu";
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void CloseWindowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
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
            NotificationIconText.Text = "❌";
        }
        else
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#EFF6FF"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#BFDBFE"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#1E40AF"));
            NotificationIconText.Text = "ℹ️";
        }
    }

    private void AnThongBao()
    {
        NotificationBorder.IsVisible = false;
        NotificationTextBlock.Text = "";
    }
}
