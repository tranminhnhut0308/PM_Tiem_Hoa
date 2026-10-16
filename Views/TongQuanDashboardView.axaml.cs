using Avalonia.Controls;

namespace ShopHoa.Views;

/// <summary>
/// Toàn bộ giao diện màn "Tổng quan". DataContext là <c>DashboardViewModel</c>
/// nên mọi binding bên trong không cần tiền tố Dashboard.
/// </summary>
public partial class TongQuanDashboardView : UserControl
{
    public TongQuanDashboardView()
    {
        InitializeComponent();
    }
}
