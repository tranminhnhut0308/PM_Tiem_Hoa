using Avalonia.Controls;

namespace ShopHoa.Views;

/// <summary>
/// Màn "Thông tin tiệm".
/// <para>
/// UserControl này không tự đặt DataContext: <c>MainWindow.axaml</c> gán
/// <c>DataContext="{Binding ThongTinTiem}"</c> từ <c>MainWindowViewModel</c>,
/// đúng như cách màn "Tổng quan" và "Báo cáo cuối ngày" đang làm.
/// </para>
/// </summary>
public partial class ThongTinTiemView : UserControl
{
    public ThongTinTiemView()
    {
        InitializeComponent();
    }
}