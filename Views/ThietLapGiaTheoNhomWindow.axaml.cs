using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class ThietLapGiaTheoNhomWindow : Window
{
    public ThietLapGiaTheoNhomWindow() : this(null)
    {
    }

    public ThietLapGiaTheoNhomWindow(IEnumerable<KhachHang>? initialCustomers)
    {
        InitializeComponent();

        var vm = new ThietLapGiaTheoNhomViewModel(initialCustomers)
        {
            LayCuaSoChinh = () => this,
            YeuCauDong = () => Close(true)
        };

        DataContext = vm;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
