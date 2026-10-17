using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class ThietLapGiaKhachHangWindow : Window
{
    public ThietLapGiaKhachHangWindow()
    {
        InitializeComponent();
    }

    public ThietLapGiaKhachHangWindow(KhachHang customer) : this()
    {
        var vm = new ThietLapGiaKhachHangViewModel(customer)
        {
            LayCuaSoChinh = () => this
        };

        DataContext = vm;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
