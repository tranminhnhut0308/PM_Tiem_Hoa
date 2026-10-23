using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class DieuChinhCongNoWindow : Window
{
    private readonly decimal _conNo;

    public DieuChinhCongNoWindow() : this("Khách hàng", 0m)
    {
    }

    public DieuChinhCongNoWindow(string tenKhachHang, decimal conNo)
    {
        InitializeComponent();

        _conNo = conNo;
        TxtKhachHang.Text = string.IsNullOrWhiteSpace(tenKhachHang) ? "Khách lẻ" : tenKhachHang;
        TxtConNo.Text = $"{conNo:N0} đ";
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var soTien = TienBox.Value;
        var tangNo = CboLoai.SelectedIndex == 1;
        var lyDo = TxtLyDo.Text?.Trim() ?? "";

        if (soTien <= 0)
        {
            HienLoi("Số tiền điều chỉnh phải lớn hơn 0.");
            return;
        }

        if (!tangNo && soTien > _conNo)
        {
            HienLoi($"Số tiền giảm ({soTien:N0} đ) vượt quá công nợ hiện tại ({_conNo:N0} đ).");
            return;
        }

        if (lyDo.Length == 0)
        {
            HienLoi("Vui lòng nhập lý do điều chỉnh công nợ.");
            return;
        }

        Close(new DieuChinhCongNoInput
        {
            SoTien = soTien,
            TangNo = tangNo,
            LyDo = lyDo
        });
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void HienLoi(string thongDiep)
    {
        TxtLoi.Text = thongDiep;
        TxtLoi.IsVisible = true;
    }
}
