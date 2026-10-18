using Avalonia.Controls;
using Avalonia.Input;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class ThemPhieuXuatHangView : UserControl
{
    public ThemPhieuXuatHangView()
    {
        InitializeComponent();
    }

    private void OnPhieuXuatProductSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        _ = viewModel.TimKiemHangHoaXuatAsync(textBox.Text);
    }

    private void OnPhieuXuatNgayXuatBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.MoLichPhieuXuatCommand.Execute(null);
    }
}
