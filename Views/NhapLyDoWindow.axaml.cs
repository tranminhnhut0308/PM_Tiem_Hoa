using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

public partial class NhapLyDoWindow : Window
{
    public NhapLyDoWindow() : this("Nhập lý do", null)
    {
    }

    public NhapLyDoWindow(string tieuDe, string? moTa, string? giaTriMacDinh = null)
    {
        InitializeComponent();
        TxtTieuDe.Text = tieuDe;
        if (!string.IsNullOrWhiteSpace(moTa))
            TxtMoTa.Text = moTa;
        if (!string.IsNullOrWhiteSpace(giaTriMacDinh))
            TxtLyDo.Text = giaTriMacDinh;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var value = TxtLyDo.Text?.Trim() ?? "";
        if (value.Length == 0)
        {
            TxtLoi.Text = "Vui lòng nhập lý do.";
            TxtLoi.IsVisible = true;
            return;
        }

        Close(value);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
