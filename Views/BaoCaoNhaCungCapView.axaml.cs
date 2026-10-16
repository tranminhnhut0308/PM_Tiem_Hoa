using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class BaoCaoNhaCungCapView : UserControl
{
    public BaoCaoNhaCungCapView()
    {
        InitializeComponent();
    }

    private void ChartPlotArea_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is BaoCaoNhaCungCapViewModel vm && e.NewSize.Width > 20)
        {
            vm.CapNhatDoRongBieuDo(e.NewSize.Width);
        }
    }
}

