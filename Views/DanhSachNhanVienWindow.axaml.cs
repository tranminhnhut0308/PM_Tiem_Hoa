using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class DanhSachNhanVienWindow : Window
{
    public DanhSachNhanVienWindow()
    {
        InitializeComponent();
        var vm = new DanhSachNhanVienViewModel
        {
            OwnerWindow = this
        };
        DataContext = vm;
    }

    public DanhSachNhanVienWindow(DanhSachNhanVienViewModel vm)
    {
        InitializeComponent();
        vm.OwnerWindow = this;
        DataContext = vm;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DanhSachNhanVienViewModel vm && vm.SelectedNhanVien != null)
        {
            if (vm.XemChiTietCommand.CanExecute(vm.SelectedNhanVien))
            {
                vm.XemChiTietCommand.Execute(vm.SelectedNhanVien);
            }
        }
    }
}
