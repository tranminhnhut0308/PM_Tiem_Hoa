using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

/// <summary>
/// Cổng xác thực trước khi mở cấu hình database.
/// Dùng mật khẩu kỹ thuật mặc định (123456) độc lập với database,
/// giúp luôn mở được cấu hình để sửa kết nối kể cả khi chưa có mạng/database.
/// </summary>
public partial class SettingsPasswordWindow : Window
{
    private const string DefaultSettingsPassword = "123456";

    public SettingsPasswordWindow() : this(null)
    {
    }

    public SettingsPasswordWindow(Window? owner)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
        }

        PasswordTextBox.Focus();
    }

    // ============================================================
    // XÁC NHẬN
    // ============================================================

    private void OnConfirmClick(
        object? sender,
        RoutedEventArgs e)
    {
        CheckPassword();
    }

    // ============================================================
    // ENTER ĐỂ XÁC NHẬN
    // ============================================================

    private void OnCredentialKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CheckPassword();
        }
    }

    // ============================================================
    // KIỂM TRA MẬT KHẨU
    // ============================================================

    private void CheckPassword()
    {
        var password = PasswordTextBox.Text ?? "";

        if (string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "⚠ Vui lòng nhập mật khẩu.";
            PasswordTextBox.Focus();
            return;
        }

        if (password == DefaultSettingsPassword)
        {
            // Đúng mật khẩu mặc định -> mở cấu hình
            Close(true);
        }
        else
        {
            ErrorText.Text = "❌ Mật khẩu không đúng. Vui lòng thử lại.";
            PasswordTextBox.SelectAll();
            PasswordTextBox.Focus();
        }
    }

    // ============================================================
    // HỦY
    // ============================================================

    private void OnCancelClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }
}
