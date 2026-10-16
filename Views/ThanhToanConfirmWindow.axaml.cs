using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ThanhToanConfirmWindow : Window
{
    private static readonly IReadOnlyList<PhuongThucOption> DanhSachPhuongThuc = new[]
    {
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    };

    public ThanhToanConfirmWindow()
    {
        InitializeComponent();

        CboPhuongThuc.ItemsSource = DanhSachPhuongThuc;
        CboPhuongThuc.SelectedIndex = 0;
    }

    public ThanhToanConfirmWindow(string tenKhach, decimal tongTien, decimal khachDua, decimal tienThoi, decimal congNo) : this()
    {
        TxtKhachHang.Text = tenKhach;
        TxtTongTien.Text = $"{tongTien:N0} đ";
        TxtTienKhachDua.Text = $"{khachDua:N0} đ";
        TxtTienThoi.Text = $"{tienThoi:N0} đ";
        TxtCongNo.Text = $"{congNo:N0} đ";
    }

    /// <summary>Mã phương thức thanh toán người dùng chọn (mặc định TIEN_MAT).</summary>
    public string PhuongThucThanhToan =>
        (CboPhuongThuc.SelectedItem as PhuongThucOption)?.Ma ?? "TIEN_MAT";

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
