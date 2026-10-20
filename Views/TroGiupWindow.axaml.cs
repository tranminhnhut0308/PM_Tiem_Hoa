using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

public partial class TroGiupWindow : Window
{
    public TroGiupWindow() : this(null)
    {
    }

    public TroGiupWindow(Window? owner)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenUpdateWindowClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var updateWin = new UpdateWindow();
            updateWin.Show(this);
        }
        catch
        {
            // fallback
        }
    }
}
