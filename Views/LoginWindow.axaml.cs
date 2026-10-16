using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        DataContext = new LoginViewModel();
    }

    /// <summary>
    /// Khi cửa sổ vừa mở: đọc trạng thái "Ghi nhớ đăng nhập".
    /// Nếu có credential hợp lệ thì khôi phục phiên và vào thẳng MainWindow.
    /// </summary>
    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is not LoginViewModel viewModel)
            return;

        await viewModel.PhucHoiPhienDaGhiNhoAsync(this);
    }

    private async void OnDatabaseSettingsClick(
        object? sender,
        RoutedEventArgs e)
    {
        // Yêu cầu nhập mật khẩu xác thực mặc định (123456)
        var passwordWindow = new SettingsPasswordWindow(this);
        var result = await passwordWindow.ShowDialog<bool>(this);

        // Nếu nhập sai hoặc bấm Hủy -> dừng lại
        if (!result)
            return;

        // Đúng mật khẩu 123456 -> Mở cài đặt Database để điền thông số
        var settingsWindow = new DatabaseSettingsWindow(this);
        await settingsWindow.ShowDialog(this);

        // Sau khi đóng cửa sổ cài đặt, tự động kiểm tra lại kết nối
        if (DataContext is LoginViewModel vm)
        {
            await vm.KiemTraKetNoiAsync();
        }
    }
}
