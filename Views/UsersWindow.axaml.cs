using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class UsersWindow : Window
{
    public UsersWindow()
    {
        InitializeComponent();
        DataContext = new UsersViewModel();
    }

    public UsersWindow(UsersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
