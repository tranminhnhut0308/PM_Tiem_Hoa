using Avalonia.Controls;

namespace ShopHoa.Views;

/// <summary>
/// Màn "Báo cáo cuối ngày".
/// <para>
/// UserControl này không tự đặt DataContext: <c>MainWindow.axaml</c> gán
/// <c>DataContext="{Binding BaoCaoCuoiNgay}"</c> từ <c>MainWindowViewModel</c>,
/// đúng như cách màn "Tổng quan" đang làm. Nhờ vậy biểu thức
/// <c>IsVisible="{Binding CoDuLieu}"</c> ở lớp ngoài vẫn đọc được property của
/// MainWindowViewModel, còn bên trong đọc property của ViewModel báo cáo.
/// </para>
/// </summary>
public partial class BaoCaoCuoiNgayView : UserControl
{
    public BaoCaoCuoiNgayView()
    {
        InitializeComponent();
    }
}