using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class DatabaseSettingsWindow : Window
{
    public DatabaseSettingsWindow() : this(null)
    {
    }

    public DatabaseSettingsWindow(Window? owner)
    {
        InitializeComponent();

        DataContext = new DatabaseSettingsViewModel();

        if (owner != null)
        {
            Owner = owner;
        }
    }
}