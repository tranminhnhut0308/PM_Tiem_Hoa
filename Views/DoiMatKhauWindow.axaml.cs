using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Dapper;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class DoiMatKhauWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly NguoiDung? _user;

    public DoiMatKhauWindow() : this(null)
    {
    }

    public DoiMatKhauWindow(Window? owner)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
        }

        _user = Session.CurrentUser;
        InitUserInfo();
    }

    private void InitUserInfo()
    {
        if (_user == null)
        {
            TxtFullName.Text = "Chưa đăng nhập";
            TxtUsername.Text = "none";
            TxtRole.Text = "Khách";
            TxtInitials.Text = "?";
            ShowAlert("Không tìm thấy thông tin tài khoản đang đăng nhập.", false);
            BtnSave.IsEnabled = false;
            return;
        }

        TxtFullName.Text = string.IsNullOrWhiteSpace(_user.HoTen) ? _user.TenDangNhap : _user.HoTen;
        TxtUsername.Text = $"@{_user.TenDangNhap}";
        TxtRole.Text = string.IsNullOrWhiteSpace(_user.VaiTro) ? "Nhân viên" : _user.VaiTro;

        var name = _user.HoTen?.Trim() ?? _user.TenDangNhap?.Trim() ?? "U";
        TxtInitials.Text = name.Length > 0 ? name[0].ToString().ToUpperInvariant() : "U";
    }

    private void ShowAlert(string message, bool isSuccess)
    {
        AlertBox.IsVisible = true;
        AlertBox.Background = isSuccess ? new SolidColorBrush(Color.Parse("#ECFDF3")) : new SolidColorBrush(Color.Parse("#FEF3F2"));
        AlertBox.BorderBrush = isSuccess ? new SolidColorBrush(Color.Parse("#A6F4C5")) : new SolidColorBrush(Color.Parse("#FECDCA"));
        AlertBox.BorderThickness = new Avalonia.Thickness(1);

        TxtAlertMessage.Text = message;
        TxtAlertMessage.Foreground = isSuccess ? new SolidColorBrush(Color.Parse("#027A48")) : new SolidColorBrush(Color.Parse("#B42318"));
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_user == null)
        {
            ShowAlert("Không tìm thấy thông tin phiên làm việc.", false);
            return;
        }

        var currentPassword = TxtCurrentPassword.Text?.Trim() ?? string.Empty;
        var newPassword = TxtNewPassword.Text?.Trim() ?? string.Empty;
        var confirmPassword = TxtConfirmPassword.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(currentPassword))
        {
            ShowAlert("Vui lòng nhập mật khẩu hiện tại.", false);
            TxtCurrentPassword.Focus();
            return;
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            ShowAlert("Vui lòng nhập mật khẩu mới.", false);
            TxtNewPassword.Focus();
            return;
        }

        if (newPassword.Length < 6)
        {
            ShowAlert("Mật khẩu mới phải có tối thiểu 6 ký tự.", false);
            TxtNewPassword.Focus();
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowAlert("Xác nhận mật khẩu mới không khớp.", false);
            TxtConfirmPassword.Focus();
            return;
        }

        if (newPassword == currentPassword)
        {
            ShowAlert("Mật khẩu mới không được trùng với mật khẩu hiện tại.", false);
            TxtNewPassword.Focus();
            return;
        }

        BtnSave.IsEnabled = false;

        try
        {
            await using var connection = await _db.TaoKetNoiAsync();

            var currentHash = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT mat_khau FROM nguoi_dung WHERE id = @Id LIMIT 1;",
                new { Id = _user.Id });

            bool passwordMatch = false;
            if (!string.IsNullOrEmpty(currentHash))
            {
                try
                {
                    passwordMatch = BCrypt.Net.BCrypt.Verify(currentPassword, currentHash);
                }
                catch
                {
                    passwordMatch = false;
                }

                // Fallback nếu database lưu plaintext từ trước
                if (!passwordMatch && string.Equals(currentPassword, currentHash, StringComparison.Ordinal))
                {
                    passwordMatch = true;
                }
            }

            if (!passwordMatch)
            {
                ShowAlert("Mật khẩu hiện tại không chính xác. Vui lòng thử lại.", false);
                BtnSave.IsEnabled = true;
                TxtCurrentPassword.Focus();
                return;
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var rows = await connection.ExecuteAsync(
                "UPDATE nguoi_dung SET mat_khau = @NewHash WHERE id = @Id;",
                new { NewHash = newHash, Id = _user.Id });

            if (rows > 0)
            {
                _user.MatKhau = newHash;
                ShowAlert("Đổi mật khẩu thành công! Cửa sổ sẽ đóng sau giây lát...", true);
                await Task.Delay(1200);
                Close(true);
            }
            else
            {
                ShowAlert("Không thể cập nhật mật khẩu. Vui lòng thử lại.", false);
                BtnSave.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ShowAlert($"Lỗi hệ thống: {ex.Message}", false);
            SafeLog.WriteError("Lỗi đổi mật khẩu", ex);
            BtnSave.IsEnabled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
