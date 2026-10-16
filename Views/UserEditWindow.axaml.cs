using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class UserEditWindow : Window
{
    public UserEditWindow()
    {
        InitializeComponent();
    }

    public UserEditWindow(UserEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
