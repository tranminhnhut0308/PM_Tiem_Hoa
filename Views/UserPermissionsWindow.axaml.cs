using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class UserPermissionsWindow : Window
{
    public UserPermissionsWindow()
    {
        InitializeComponent();
    }

    public UserPermissionsWindow(UserPermissionsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
