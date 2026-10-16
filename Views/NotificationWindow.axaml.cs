using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ShopHoa.Views;

public enum NotificationType
{
    Success,
    Error,
    Warning,
    Info
}

public partial class NotificationWindow : Window
{
    public NotificationWindow()
    {
        InitializeComponent();
    }

    public NotificationWindow(string title, string message, NotificationType type = NotificationType.Info)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        ApplyType(type);
    }

    private void ApplyType(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.Success:
                IconContainer.Background = Brush.Parse("#ECFDF5");
                IconPath.Foreground = Brush.Parse("#059669");
                IconPath.Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
                break;
            case NotificationType.Error:
                IconContainer.Background = Brush.Parse("#FEF2F2");
                IconPath.Foreground = Brush.Parse("#DC2626");
                IconPath.Data = Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");
                break;
            case NotificationType.Warning:
                IconContainer.Background = Brush.Parse("#FFFBEB");
                IconPath.Foreground = Brush.Parse("#D97706");
                IconPath.Data = Geometry.Parse("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z");
                break;
            case NotificationType.Info:
            default:
                IconContainer.Background = Brush.Parse("#EFF6FF");
                IconPath.Foreground = Brush.Parse("#2563EB");
                IconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
                break;
        }
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public static async Task ShowAsync(string message, string title = "Thông báo", NotificationType type = NotificationType.Info, Window? owner = null)
    {
        var win = new NotificationWindow(title, message, type);
        var targetOwner = owner ?? FindActiveWindow();

        if (targetOwner != null && targetOwner.IsVisible)
        {
            try
            {
                await win.ShowDialog(targetOwner);
                return;
            }
            catch
            {
                // Fallback nếu không thể ShowDialog
            }
        }

        win.Show();
    }

    public static Task ShowSuccessAsync(string message, string title = "Thành công", Window? owner = null)
        => ShowAsync(message, title, NotificationType.Success, owner);

    public static Task ShowErrorAsync(string message, string title = "Lỗi", Window? owner = null)
        => ShowAsync(message, title, NotificationType.Error, owner);

    public static Task ShowWarningAsync(string message, string title = "Thông báo", Window? owner = null)
        => ShowAsync(message, title, NotificationType.Warning, owner);

    private static Window? FindActiveWindow()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
        {
            return lt.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.IsActive)
                ?? lt.Windows.OfType<Window>().LastOrDefault(w => w.IsVisible)
                ?? lt.MainWindow;
        }
        return null;
    }
}
