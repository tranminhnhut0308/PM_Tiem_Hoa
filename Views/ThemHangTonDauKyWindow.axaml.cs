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

public partial class ThemHangTonDauKyWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<ProductOption> _danhSachKho = new();
    private readonly List<SanPham> _danhSachSanPham = new();
    private SanPham? _sanPhamDaChon;
    private bool _dangLuu;
    private bool _dangFormatTien;
    private bool _dangFormatSoLuong;

    /// <summary>
    /// Đối tượng hàng tồn đầu kỳ vừa được lưu thành công (nếu có).
    /// </summary>
    public ChiTietHangHoaDauKy? KetQuaLuu { get; private set; }

    public ThemHangTonDauKyWindow() : this(null, null)
    {
    }

    public ThemHangTonDauKyWindow(SanPham? sanPhamMacDinh = null, uint? khoIdMacDinh = null)
    {
        InitializeComponent();

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync(sanPhamMacDinh, khoIdMacDinh);
    }

    private async Task KhoiTaoDuLieuAsync(SanPham? sanPhamMacDinh, uint? khoIdMacDinh)
    {
        try
        {
            // 1. Mặc định ngày đầu kỳ là ngày hôm nay
            NgayDauKyPicker.SelectedDate = DateTime.Today;

            // 2. Tải danh sách kho và sản phẩm song song để tối ưu tốc độ
            var khoTask = _db.GetDanhSachKhoAsync();
            var spTask = _db.GetSanPhamAsync();

            await Task.WhenAll(khoTask, spTask);

            // Gán danh sách kho
            _danhSachKho.Clear();
            _danhSachKho.AddRange(await khoTask);
            KhoComboBox.ItemsSource = _danhSachKho;

            if (_danhSachKho.Count > 0)
            {
                if (khoIdMacDinh.HasValue && khoIdMacDinh.Value > 0)
                {
                    KhoComboBox.SelectedItem = _danhSachKho.FirstOrDefault(k => k.Id == khoIdMacDinh.Value) 
                                               ?? _danhSachKho[0];
                }
                else
                {
                    KhoComboBox.SelectedIndex = 0;
                }
            }

            // Gán danh sách hàng hóa
            _danhSachSanPham.Clear();
            _danhSachSanPham.AddRange(await spTask);

            // Cấu hình tìm kiếm gợi ý linh hoạt (theo mã hoặc theo tên)
            MaHangAutoCompleteBox.ItemsSource = _danhSachSanPham;
            MaHangAutoCompleteBox.ItemFilter = (search, item) =>
            {
                if (string.IsNullOrWhiteSpace(search))
                    return true;

                if (item is SanPham sp)
                {
                    var ma = sp.MaSanPham ?? "";
                    var ten = sp.TenSanPham ?? "";
                    return ma.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           ten.Contains(search, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            };

            // Nếu có sản phẩm mặc định truyền vào từ màn hình chính
            if (sanPhamMacDinh is not null)
            {
                ChonSanPham(sanPhamMacDinh);
            }

            // Tính thành tiền ban đầu
            CapNhatThanhTienRealtime();
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể tải danh mục: " + ex.Message, isError: true);
        }
    }

    private void ChonSanPham(SanPham sp)
    {
        _sanPhamDaChon = sp;
        MaHangAutoCompleteBox.Text = sp.MaSanPham;
        TenHangTextBox.Text = sp.TenSanPham;

        // Nếu ô giá vốn đang là 0 thì tự gợi ý theo giá vốn hiện có của sản phẩm
        var giaHienTai = DocTien(GiaVonTextBox.Text);
        if (giaHienTai == 0 && sp.GiaVon > 0)
        {
            _dangFormatTien = true;
            GiaVonTextBox.Text = MoneyInput.Format(sp.GiaVon);
            _dangFormatTien = false;
        }

        AnThongBao();
        CapNhatThanhTienRealtime();
    }

    private void MaHangAutoCompleteBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MaHangAutoCompleteBox.SelectedItem is SanPham sp)
        {
            ChonSanPham(sp);
        }
    }

    private void MaHangAutoCompleteBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var text = (MaHangAutoCompleteBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _sanPhamDaChon = null;
            TenHangTextBox.Text = "";
            CapNhatThanhTienRealtime();
            return;
        }

        // Nếu đã chọn đúng sản phẩm có mã trùng với text đang nhập thì giữ nguyên
        if (_sanPhamDaChon != null && string.Equals(_sanPhamDaChon.MaSanPham, text, StringComparison.OrdinalIgnoreCase))
        {
            AnThongBao();
            return;
        }

        // Tìm kiếm chính xác theo mã hoặc theo tên
        var spTimThay = _danhSachSanPham.FirstOrDefault(s =>
            string.Equals(s.MaSanPham, text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.TenSanPham, text, StringComparison.OrdinalIgnoreCase));

        if (spTimThay is not null)
        {
            ChonSanPham(spTimThay);
        }
        else
        {
            _sanPhamDaChon = null;
            TenHangTextBox.Text = "";
            CapNhatThanhTienRealtime();
            HienThongBao($"Hàng hóa '{text}' không tồn tại trong danh mục. Vui lòng chọn một mặt hàng hợp lệ!", isError: true);
        }
    }

    private void SoLuongTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dangFormatSoLuong) return;
        CapNhatThanhTienRealtime();
    }

    private void SoLuongTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var sl = DocSoLuong(SoLuongTextBox.Text);
        if (sl < 0) sl = 0;

        _dangFormatSoLuong = true;
        SoLuongTextBox.Text = sl.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        _dangFormatSoLuong = false;

        CapNhatThanhTienRealtime();
    }

    private void GiaVonTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dangFormatTien) return;

        // Cho phép người dùng gõ số và định dạng nhanh
        var text = GiaVonTextBox.Text ?? "";
        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var val))
        {
            var formatted = MoneyInput.Format(val);
            if (formatted != text)
            {
                _dangFormatTien = true;
                GiaVonTextBox.Text = formatted;
                GiaVonTextBox.CaretIndex = formatted.Length;
                _dangFormatTien = false;
            }
        }

        CapNhatThanhTienRealtime();
    }

    private void GiaVonTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var val = DocTien(GiaVonTextBox.Text);
        if (val < 0) val = 0;

        _dangFormatTien = true;
        GiaVonTextBox.Text = MoneyInput.Format(val);
        _dangFormatTien = false;

        CapNhatThanhTienRealtime();
    }

    /// <summary>
    /// Công thức nghiệp vụ: Thành tiền = Số lượng × Giá vốn.
    /// Cập nhật ngay lập tức mà không cần nhấn nút tính toán.
    /// </summary>
    private void CapNhatThanhTienRealtime()
    {
        var soLuong = DocSoLuong(SoLuongTextBox.Text);
        var giaVon = DocTien(GiaVonTextBox.Text);

        if (soLuong < 0) soLuong = 0;
        if (giaVon < 0) giaVon = 0;

        var thanhTien = Math.Round(soLuong * giaVon, 2, MidpointRounding.AwayFromZero);
        ThanhTienTextBlock.Text = MoneyInput.Format(thanhTien) + " đ";
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

        var ngayDauKy = NgayDauKyPicker.SelectedDate.Value.Date;

        // 2. Kiểm tra kho hàng
        if (KhoComboBox.SelectedItem is not ProductOption selectedKho || selectedKho.Id == 0)
        {
            HienThongBao("Vui lòng chọn kho hàng lưu trữ.", isError: true);
            KhoComboBox.Focus();
            return;
        }

        // 3. Kiểm tra hàng hóa
        if (_sanPhamDaChon is null)
        {
            var maNhap = (MaHangAutoCompleteBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(maNhap))
            {
                HienThongBao("Vui lòng nhập hoặc chọn mã hàng hóa.", isError: true);
                MaHangAutoCompleteBox.Focus();
                return;
            }

            // Thử tra lại một lần nữa
            var sp = _danhSachSanPham.FirstOrDefault(s =>
                string.Equals(s.MaSanPham, maNhap, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.TenSanPham, maNhap, StringComparison.OrdinalIgnoreCase));

            if (sp is not null)
            {
                ChonSanPham(sp);
            }
            else
            {
                HienThongBao($"Hàng hóa '{maNhap}' không tồn tại. Vui lòng chọn hàng hợp lệ từ danh mục!", isError: true);
                MaHangAutoCompleteBox.Focus();
                return;
            }
        }

        // 4. Kiểm tra số lượng
        var soLuong = DocSoLuong(SoLuongTextBox.Text);
        if (soLuong < 0)
        {
            HienThongBao("Số lượng tồn kho không được âm.", isError: true);
            SoLuongTextBox.Focus();
            return;
        }

        // 5. Kiểm tra giá vốn
        var giaVon = DocTien(GiaVonTextBox.Text);
        if (giaVon < 0)
        {
            HienThongBao("Giá vốn không được âm.", isError: true);
            GiaVonTextBox.Focus();
            return;
        }

        var noiDung = (NoiDungTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(noiDung))
        {
            noiDung = "Tồn đầu kỳ";
        }

        // 6. Thực hiện lưu vào CSDL
        try
        {
            _dangLuu = true;
            SaveButton.IsEnabled = false;
            SaveButton.Content = "⏳  Đang lưu...";

            await _db.LuuHangTonDauKyAsync(
                selectedKho.Id,
                _sanPhamDaChon!.Id,
                soLuong,
                giaVon,
                ngayDauKy,
                noiDung);

            // Lưu kết quả để caller sử dụng nếu cần
            KetQuaLuu = new ChiTietHangHoaDauKy
            {
                KhoHangId = selectedKho.Id,
                HangHoaId = _sanPhamDaChon.Id,
                SoLuong = soLuong,
                GiaVon = giaVon,
                ThanhTien = Math.Round(soLuong * giaVon, 2, MidpointRounding.AwayFromZero),
                NoiDung = noiDung,
                NgayDauKy = ngayDauKy,
                MaKhoHang = selectedKho.Name,
                TenKhoHang = selectedKho.Name,
                MaHangHoa = _sanPhamDaChon.MaSanPham,
                TenHangHoa = _sanPhamDaChon.TenSanPham
            };

            // Đóng cửa sổ và báo kết quả thành công
            Close(true);
        }
        catch (Exception ex)
        {
            // Khi lưu thất bại, giữ lại dữ liệu người dùng đã nhập và hiển thị lỗi dễ hiểu
            HienThongBao("Lưu tồn đầu kỳ thất bại: " + ex.Message, isError: true);
        }
        finally
        {
            _dangLuu = false;
            SaveButton.IsEnabled = true;
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

    private void HienThongBao(string thongBao, bool isError)
    {
        NotificationTextBlock.Text = thongBao;
        NotificationIconText.Text = isError ? "⚠️" : "ℹ️";

        if (isError)
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#FDE8E8"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#F8B4B4"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#C00000"));
        }
        else
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#EAF7EE"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#A3D9B1"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#166534"));
        }

        NotificationBorder.IsVisible = true;
    }

    private void AnThongBao()
    {
        NotificationBorder.IsVisible = false;
        NotificationTextBlock.Text = "";
    }

    private static decimal DocSoLuong(string? text)
    {
        return MoneyInput.Parse(text);
    }

    private static decimal DocTien(string? text)
    {
        return MoneyInput.Parse(text);
    }
}
