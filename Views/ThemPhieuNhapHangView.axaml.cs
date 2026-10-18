using Avalonia.Controls;
using Avalonia.Input;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class ThemPhieuNhapHangView : UserControl
{
    public ThemPhieuNhapHangView()
    {
        InitializeComponent();
    }

    private void OnImportProductSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.ImportProductSearch = textBox.Text ?? string.Empty;

        if (viewModel.TimKiemSanPhamNhapCommand.CanExecute(null))
        {
            _ = viewModel.TimKiemSanPhamNhapCommand.ExecuteAsync(textBox.Text);
        }
    }

    private void OnImportSupplierSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.ImportSupplierSearch = textBox.Text ?? string.Empty;

        if (viewModel.TimKiemNhaCungCapNhapCommand.CanExecute(null))
        {
            _ = viewModel.TimKiemNhaCungCapNhapCommand.ExecuteAsync(textBox.Text);
        }
    }

    private void OnPhieuNhapNgayNhapBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.MoLichPhieuNhapCommand.Execute(null);
    }
}
