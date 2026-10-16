using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private readonly LoginCredentialStore _store = new();

    public LoginViewModel()
    {
        _ = KiemTraKetNoiAsync();
    }

    // Không có tài khoản nào gắn cứng trong source.
    // Tên đăng nhập chỉ được điền lại khi người dùng đã bật "Ghi nhớ đăng nhập".
    [ObservableProperty]
    private string tenDangNhap = "";

    [ObservableProperty]
    private string matKhau = "";

    [ObservableProperty]
    private bool dangXuLy;

    [ObservableProperty]
    private string thongBao = "";

    [ObservableProperty]
    private bool ghiNhoDangNhap;

    [ObservableProperty]
    private string trangThaiKetNoi = "Đang kiểm tra kết nối...";

    /// <summary>
    /// Hiện nút "Xóa thông tin đã ghi nhớ" khi máy đang có credential lưu.
    /// </summary>
    [ObservableProperty]
    private bool coThongTinDaGhiNho;

    // =========================================================
    // KIỂM TRA DATABASE
    // =========================================================

    public async Task KiemTraKetNoiAsync()
    {
        try
        {
            await _db.WarmUpAsync();

            TrangThaiKetNoi = "Đã kết nối MariaDB";
        }
        catch (Exception ex)
        {
            TrangThaiKetNoi = "Chưa kết nối MariaDB";

            SafeLog.WriteError("DATABASE CONNECTION ERROR", ex);
        }
    }

    // =========================================================
    // GHI NHỚ ĐĂNG NHẬP
    // =========================================================

    /// <summary>
    /// Được LoginWindow gọi khi cửa sổ vừa mở.
    /// Điền lại tên đăng nhập đã lưu và nếu có credential hợp lệ
    /// thì khôi phục phiên đăng nhập luôn.
    /// Trả về true nghĩa là đã vào thẳng MainWindow.
    /// </summary>
    public async Task<bool> PhucHoiPhienDaGhiNhoAsync(Window? loginWindow)
    {
        var remembered = _store.Load();

        CoThongTinDaGhiNho = remembered is not null;

        if (remembered is null)
        {
            TrangThaiKetNoi = "Đang chờ đăng nhập...";
            return false;
        }

        // Điền lại tên đăng nhập dù có khôi phục được phiên hay không.
        TenDangNhap = remembered.TenDangNhap;

        // Máy đang có credential đã lưu -> tick lại checkbox cho khớp.
        GhiNhoDangNhap = true;

        var secret = SecretProtector.Unprotect(remembered.MatKhauDaMaHoa);

        if (string.IsNullOrEmpty(secret))
        {
            // Dữ liệu hỏng hoặc không thuộc về máy này -> dọn dẹp.
            _store.Clear();

            CoThongTinDaGhiNho = false;

            TrangThaiKetNoi = "Thông tin ghi nhớ không hợp lệ. Vui lòng đăng nhập lại.";
            return false;
        }

        DangXuLy = true;
        ThongBao = "";
        TrangThaiKetNoi = "Đang khôi phục phiên đăng nhập...";

        try
        {
            // Vẫn phải xác thực lại với database, tuyệt đối không tin file đã lưu.
            var loginRes = await _db.DangNhapAsync(remembered.TenDangNhap, secret);

            if (!loginRes.IsValid)
            {
                // Mật khẩu đã đổi / tài khoản bị khoá -> xoá credential,
                // tránh thử lại sai mỗi lần mở app.
                _store.Clear();

                CoThongTinDaGhiNho = false;

                GhiNhoDangNhap = false;

                ThongBao =
                    "Thông tin đăng nhập đã lưu không còn đúng. " +
                    "Vui lòng đăng nhập lại.";

                TrangThaiKetNoi = "Đăng nhập lại bằng tài khoản của bạn.";

                return false;
            }

            var user = loginRes.User!;
            await _db.EnsurePermissionCatalogAsync();
            var perms = await _db.GetUserPermissionKeysAsync(user.Id);
            Session.SetCurrentUser(user, perms);
            _ = _db.CapNhatLanDangNhapCuoiAsync(user.Id);

            if (!Quyen.IsAdmin(user))
            {
                _ = _db.LogSubAccountActivityAsync(
                    "Đăng nhập",
                    "HE_THONG",
                    user.Id,
                    user.TenDangNhap,
                    $"Tài khoản {user.TenDangNhap} ({user.HoTen}) đăng nhập hệ thống");
            }

            ThongBao = "";
            TrangThaiKetNoi = "Đăng nhập thành công.";

            return MoGiaoDienChinh(loginWindow);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("DATABASE AUTO LOGIN ERROR", ex);

            ThongBao =
                "Không thể kết nối database.\n" +
                SafeLog.Redact(ex.Message);

            TrangThaiKetNoi = "Lỗi kết nối MariaDB.";

            return false;
        }
        finally
        {
            DangXuLy = false;
        }
    }

    /// <summary>
    /// Lưu hoặc xoá credential theo trạng thái checkbox hiện tại.
    /// </summary>
    private void CapNhatGhiNhoDangNhap(
        string tenDangNhap,
        string matKhau)
    {
        if (!GhiNhoDangNhap)
        {
            _store.Clear();

            CoThongTinDaGhiNho = false;
            return;
        }

        var saved = _store.Save(tenDangNhap, matKhau);

        CoThongTinDaGhiNho = saved;
    }

    // =========================================================
    // ĐĂNG NHẬP
    // =========================================================

    [RelayCommand]
    private async Task DangNhapAsync(Window? loginWindow)
    {
        if (DangXuLy)
            return;

        var tenDangNhap = TenDangNhap?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(tenDangNhap))
        {
            ThongBao = "Vui lòng nhập tên đăng nhập.";
            TenDangNhap = "";
            return;
        }

        if (string.IsNullOrEmpty(MatKhau))
        {
            ThongBao = "Vui lòng nhập mật khẩu.";
            MatKhau = "";
            return;
        }

        DangXuLy = true;
        ThongBao = "";
        TrangThaiKetNoi = "Đang kết nối MariaDB...";

        try
        {
            // =====================================================
            // XÁC THỰC TRỰC TIẾP TỪ DATABASE (BCrypt)
            // Không có tài khoản gắn cứng, không có đường tắt.
            // =====================================================

            var loginRes = await _db.DangNhapAsync(tenDangNhap, MatKhau);

            if (!loginRes.IsValid)
            {
                ThongBao = loginRes.IsLocked ? (loginRes.ErrorMessage ?? "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.") : "Tên đăng nhập hoặc mật khẩu không đúng.";
                TrangThaiKetNoi = "Kết nối database thành công.";
                MatKhau = "";
                return;
            }

            var user = loginRes.User!;
            await _db.EnsurePermissionCatalogAsync();
            var perms = await _db.GetUserPermissionKeysAsync(user.Id);
            Session.SetCurrentUser(user, perms);
            _ = _db.CapNhatLanDangNhapCuoiAsync(user.Id);

            if (!Quyen.IsAdmin(user))
            {
                _ = _db.LogSubAccountActivityAsync(
                    "Đăng nhập",
                    "HE_THONG",
                    user.Id,
                    user.TenDangNhap,
                    $"Tài khoản {user.TenDangNhap} ({user.HoTen}) đăng nhập hệ thống");
            }

            // Ghi nhớ hoặc xoá thông tin đăng nhập
            CapNhatGhiNhoDangNhap(tenDangNhap, MatKhau);

            // Không giữ mật khẩu dạng rõ trong bộ nhớ ViewModel
            MatKhau = "";

            TrangThaiKetNoi = "Đăng nhập thành công.";
            ThongBao = "";

            // =====================================================
            // MỞ MAIN WINDOW
            // =====================================================

            if (!MoGiaoDienChinh(loginWindow))
            {
                ThongBao =
                    "Đăng nhập thành công nhưng không thể mở giao diện chính.";

                TrangThaiKetNoi = "Lỗi khi mở MainWindow.";
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("DATABASE LOGIN ERROR", ex);

            ThongBao =
                "Không thể kết nối database.\n" +
                SafeLog.Redact(ex.Message);

            TrangThaiKetNoi = "Lỗi kết nối MariaDB.";
        }
        finally
        {
            DangXuLy = false;
        }
    }

    /// <summary>
    /// Mở MainWindow và đóng cửa sổ đăng nhập.
    /// Trả về false nếu không mở được.
    /// </summary>
    private bool MoGiaoDienChinh(Window? loginWindow)
    {
        try
        {
            var mainWindow = new MainWindow();

            // Sau đăng nhập, cửa sổ chính thật là MainWindow chứ không còn là
            // LoginWindow. Phải cập nhật MainWindow của lifetime, nếu không mọi
            // nơi lấy cửa sổ qua ApplicationLifetime.MainWindow (hộp thoại chọn
            // file xuất Excel, hộp thoại con...) vẫn dùng LoginWindow đã đóng nên
            // thao tác không mở được gì.
            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = mainWindow;
            }

            mainWindow.Show();

            loginWindow?.Close();

            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("MAIN WINDOW ERROR", ex);

            return false;
        }
    }

    // =========================================================
    // XÓA THÔNG TIN ĐĂNG NHẬP ĐÃ GHI NHỚ
    // =========================================================

    [RelayCommand]
    private void XoaThongTinDaGhiNho()
    {
        _store.Clear();

        CoThongTinDaGhiNho = false;
        GhiNhoDangNhap = false;
        MatKhau = "";

        ThongBao = "";
        TrangThaiKetNoi = "Đã xoá thông tin đăng nhập đã ghi nhớ.";
    }

    // =========================================================
    // THOÁT
    // =========================================================

    [RelayCommand]
    private void Thoat(Window? loginWindow)
    {
        loginWindow?.Close();
    }

    // =========================================================
    // QUÊN MẬT KHẨU
    // =========================================================

    [RelayCommand]
    private void QuenMatKhau()
    {
        ThongBao =
            "Vui lòng liên hệ quản trị viên để đặt lại mật khẩu.";
    }

    // =========================================================
    // TÀI KHOẢN KHÁC
    // =========================================================

    [RelayCommand]
    private void TaiKhoanKhac()
    {
        TenDangNhap = "";
        MatKhau = "";
        ThongBao = "";

        TrangThaiKetNoi = "Đang chờ đăng nhập...";
    }
}
