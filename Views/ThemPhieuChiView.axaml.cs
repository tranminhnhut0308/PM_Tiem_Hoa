using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShopHoa.Views;

public partial class ThemPhieuChiView : UserControl
{
    public ThemPhieuChiView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
