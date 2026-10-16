using Avalonia.Controls;

namespace ShopHoa.Views;

/// <summary>
/// Code-behind của màn "Báo cáo khách hàng". DataContext được gán từ
/// <c>MainWindowViewModel</c> (<c>DataContext="{Binding BaoCaoKhachHang}"</c>).
/// </summary>
public partial class BaoCaoKhachHangView : UserControl
{
    public BaoCaoKhachHangView()
    {
        InitializeComponent();
    }
}
