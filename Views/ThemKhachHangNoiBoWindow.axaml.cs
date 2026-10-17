using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ thêm mới khách hàng nội bộ / đối tác nội bộ (Giao diện 17).
/// Ánh xạ vào bảng khach_hang với loai_khach_hang = 'NOI_BO'.
/// </summary>
public partial class ThemKhachHangNoiBoWindow : Window
{
    private readonly DatabaseService _db = new();
    private bool _dangLuu;

    /// <summary>
    /// Đối tượng khách hàng nội bộ vừa được lưu thành công (dùng cho cửa sổ gọi nhận kết quả).
    /// </summary>
    public KhachHang? KetQuaLuu { get; private set; }

    public ThemKhachHangNoiBoWindow()
    {
        InitializeComponent();

        KhoiTaoDanhSachBoPhan();

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync();
    }

    private void KhoiTaoDanhSachBoPhan()
    {
        var boPhanMacDinh = new List<string>
        {
            "Ban Giám đốc",
            "Phòng Kinh doanh",
            "Phòng Kế toán & Tài chính",
            "Bộ phận Kho vận & Giao nhận",
            "Bộ phận Cắm hoa & Thiết kế",
            "Bộ phận Bán hàng & Thu ngân",
            "Chi nhánh 1 (Trung tâm)",
            "Chi nhánh 2",
            "Chi nhánh 3"
        };

        BoPhanComboBox.ItemsSource = boPhanMacDinh;
        BoPhanComboBox.SelectedIndex = 1; // Mặc định Phòng Kinh doanh
    }

    private async Task KhoiTaoDuLieuAsync()
    {
        try
        {
            CapNhatTrangThaiNut(true);

            // 1. Đảm bảo nhóm "Nội bộ" tồn tại trong database
            var nhomNoiBo = await _db.EnsureNhomKhachHangNoiBoAsync();

            // 2. Tải danh sách nhóm khách hàng và sinh mã mới
            var nhomTask = _db.GetNhomKhachHangAsync();
            var maTask = _db.TaoMaKhachHangNoiBoMoiAsync("NB");

            await Task.WhenAll(nhomTask, maTask);

            var danhSachNhom = await nhomTask;
            var maMoi = await maTask;

            MaKhachHangBox.Text = maMoi;

            NhomKhachHangComboBox.ItemsSource = danhSachNhom;
            if (nhomNoiBo != null)
            {
                var chonNhom = danhSachNhom.FirstOrDefault(n => n.Id == nhomNoiBo.Id)
                               ?? danhSachNhom.FirstOrDefault(n => n.TenNhom.Contains("Nội bộ", StringComparison.OrdinalIgnoreCase))
                               ?? danhSachNhom.FirstOrDefault();
                NhomKhachHangComboBox.SelectedItem = chonNhom;
            }

            Dispatcher.UIThread.Post(() => TenKhachHangBox.Focus());
        }
        catch (Exception ex)
        {
            HienThiLoi("Không thể khởi tạo dữ liệu: " + ex.Message);
        }
        finally
        {
            CapNhatTrangThaiNut(false);
        }
    }

    private async void OnSinhMaMoiClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var maMoi = await _db.TaoMaKhachHangNoiBoMoiAsync("NB");
            MaKhachHangBox.Text = maMoi;
        }
        catch (Exception ex)
        {
            HienThiLoi("Lỗi khi sinh mã mới: " + ex.Message);
        }
    }

    private async Task<bool> ValidateInputAsync()
    {
        AnThongBao();

        // 1. Kiểm tra mã đối tác nội bộ
        var ma = MaKhachHangBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(ma))
        {
            HienThiLoi("Vui lòng nhập hoặc sinh mã khách hàng nội bộ.");
            MaKhachHangBox.Focus();
            return false;
        }

        // 2. Kiểm tra tên khách hàng nội bộ
        var ten = TenKhachHangBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(ten))
        {
            HienThiLoi("Vui lòng nhập tên khách hàng / đối tác nội bộ.");
            TenKhachHangBox.Focus();
            return false;
        }

        // 3. Kiểm tra số điện thoại nếu có nhập
        var sdt = SoDienThoaiBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(sdt))
        {
            var cleanPhone = Regex.Replace(sdt, @"[\s\.\-\(\)]", "");
            if (cleanPhone.Length < 8 || cleanPhone.Length > 15 || !Regex.IsMatch(cleanPhone, @"^[0-9\+]+$"))
            {
                HienThiLoi("Số điện thoại không hợp lệ (vui lòng nhập từ 8 đến 15 chữ số).");
                SoDienThoaiBox.Focus();
                return false;
            }

            // Kiểm tra trùng SĐT trong database
            var daTrungSdt = await _db.KiemTraTrungSoDienThoaiAsync(cleanPhone);
            if (daTrungSdt)
            {
                HienThiLoi($"Số điện thoại '{sdt}' đã tồn tại trong danh mục khách hàng.");
                SoDienThoaiBox.Focus();
                return false;
            }
        }

        // 4. Kiểm tra Email nếu có nhập
        var email = EmailBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                HienThiLoi("Định dạng địa chỉ Email không hợp lệ (ví dụ: nhanvien@tiemhoa.com).");
                EmailBox.Focus();
                return false;
            }
        }

        return true;
    }

    private async Task<bool> LuuDoiTacAsync()
    {
        if (_dangLuu) return false;

        var isValid = await ValidateInputAsync();
        if (!isValid) return false;

        _dangLuu = true;
        CapNhatTrangThaiNut(true);

        try
        {
            var ma = MaKhachHangBox.Text!.Trim();
            var ten = TenKhachHangBox.Text!.Trim();
            var boPhan = BoPhanComboBox.SelectedItem?.ToString()?.Trim();
            var nhomChon = NhomKhachHangComboBox.SelectedItem as NhomKhachHang;
            var sdt = string.IsNullOrWhiteSpace(SoDienThoaiBox.Text) ? null : SoDienThoaiBox.Text.Trim();
            var email = string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim();
            var diaChi = string.IsNullOrWhiteSpace(DiaChiBox.Text) ? null : DiaChiBox.Text.Trim();
            var ghiChu = string.IsNullOrWhiteSpace(GhiChuBox.Text) ? null : GhiChuBox.Text.Trim();
            var trangThai = TrangThaiCheckBox.IsChecked ?? true;

            var khachHang = new KhachHang
            {
                MaKhachHang = ma,
                TenKhachHang = ten,
                LoaiKhachHang = "NOI_BO",
                TenCongTy = boPhan, // Lưu bộ phận/đơn vị trực thuộc
                NhomKhachHangId = nhomChon?.Id,
                NhomKhachHang = nhomChon?.TenNhom ?? "Nội bộ",
                SoDienThoai = sdt,
                Email = email,
                DiaChi = diaChi,
                GhiChu = ghiChu,
                TrangThai = trangThai,
                ConHopTac = trangThai,
                NgayTao = DateTime.Now,
                NgayCapNhat = DateTime.Now
            };

            var newId = await _db.ThemKhachHangNoiBoAsync(khachHang);
            khachHang.Id = newId;
            KetQuaLuu = khachHang;

            return true;
        }
        catch (InvalidOperationException ex)
        {
            HienThiLoi(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            HienThiLoi("Lỗi khi lưu khách hàng nội bộ: " + ex.Message);
            return false;
        }
        finally
        {
            _dangLuu = false;
            CapNhatTrangThaiNut(false);
        }
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var thanhCong = await LuuDoiTacAsync();
        if (thanhCong)
        {
            Close(true);
        }
    }

    private async void OnSaveAndNewClicked(object? sender, RoutedEventArgs e)
    {
        var tenVuaLuu = TenKhachHangBox.Text?.Trim() ?? "Đối tác nội bộ";
        var thanhCong = await LuuDoiTacAsync();

        if (thanhCong)
        {
            // Hiển thị thông báo thành công
            ThanhCongMessageTextBlock.Text = $"Đã lưu thành công: {tenVuaLuu}. Hãy tiếp tục nhập khách hàng nội bộ tiếp theo.";
            ThanhCongBorder.IsVisible = true;

            // Xóa dữ liệu các trường để nhập mới
            TenKhachHangBox.Text = "";
            SoDienThoaiBox.Text = "";
            EmailBox.Text = "";
            DiaChiBox.Text = "";
            GhiChuBox.Text = "";

            // Tự động sinh mã mới tiếp theo
            try
            {
                var maMoi = await _db.TaoMaKhachHangNoiBoMoiAsync("NB");
                MaKhachHangBox.Text = maMoi;
            }
            catch { }

            TenKhachHangBox.Focus();
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(KetQuaLuu != null);
    }

    private void HienThiLoi(string message)
    {
        ThanhCongBorder.IsVisible = false;
        LoiMessageTextBlock.Text = message;
        LoiBorder.IsVisible = true;
    }

    private void AnThongBao()
    {
        LoiBorder.IsVisible = false;
        ThanhCongBorder.IsVisible = false;
    }

    private void CapNhatTrangThaiNut(bool dangXuLy)
    {
        LoadingProgressBar.IsVisible = dangXuLy;
        NutLuuButton.IsEnabled = !dangXuLy;
        NutLuuVaThemMoiButton.IsEnabled = !dangXuLy;
        NutHuyButton.IsEnabled = !dangXuLy;
    }
}
