using Avalonia.Controls;

namespace ShopHoa.Views;

/// <summary>
/// Code-behind của màn "Báo cáo hàng hóa". DataContext được gán từ
/// <c>MainWindowViewModel</c> (<c>DataContext="{Binding BaoCaoHangHoa}"</c>).
/// </summary>
public partial class BaoCaoHangHoaView : UserControl
{
    public BaoCaoHangHoaView()
    {
        InitializeComponent();
    }
}
