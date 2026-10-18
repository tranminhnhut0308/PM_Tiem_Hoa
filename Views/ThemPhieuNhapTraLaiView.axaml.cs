using Avalonia.Controls;
using Avalonia.Input;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class ThemPhieuNhapTraLaiView : UserControl
{
    public ThemPhieuNhapTraLaiView()
    {
        InitializeComponent();
    }

    private void OnPhieuTraProductSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        _ = viewModel.TimKiemHangHoaPhieuTraAsync(textBox.Text);
    }

    private void OnPhieuTraCustomerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        _ = viewModel.TimKiemKhachHangPhieuTraAsync(textBox.Text);
    }

    private void OnPhieuTraHoaDonSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        _ = viewModel.TimKiemHoaDonPhieuTraAsync(textBox.Text);
    }

    private void OnPhieuTraNgayTraBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.MoLichPhieuTra();
    }
}
