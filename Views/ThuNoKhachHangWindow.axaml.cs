using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ThuNoKhachHangWindow : Window
{
    private static readonly IReadOnlyList<PhuongThucOption> DanhSachPhuongThuc = new[]
    {
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    };

    private readonly decimal _conNo;

    public ThuNoKhachHangWindow() : this("Khách hàng", 0m)
    {
    }

    public ThuNoKhachHangWindow(string tenKhachHang, decimal conNo)
    {
        InitializeComponent();

        CboPhuongThuc.ItemsSource = DanhSachPhuongThuc;
        CboPhuongThuc.SelectedIndex = 0;

        _conNo = conNo;

        TxtKhachHang.Text = string.IsNullOrWhiteSpace(tenKhachHang)
            ? "Khách lẻ"
            : tenKhachHang;
        TxtConNo.Text = $"{conNo:N0} đ";

        // Mặc định thu hết phần còn nợ cho tiện.
        TienThuBox.Value = conNo;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var soTien = TienThuBox.Value;

        if (soTien <= 0)
        {
            HienLoi("Số tiền thu phải lớn hơn 0.");
            return;
        }

        if (soTien > _conNo)
        {
            HienLoi($"Số tiền thu ({soTien.ToString("N0", CultureInfo.InvariantCulture)} đ) "
                + $"vượt quá công nợ còn lại ({_conNo.ToString("N0", CultureInfo.InvariantCulture)} đ).");
            return;
        }

        var ketQua = new ThuNoHoaDonKetQua
        {
            SoTien = soTien,
            PhuongThuc = (CboPhuongThuc.SelectedItem as PhuongThucOption)?.Ma ?? "TIEN_MAT",
            GhiChu = string.IsNullOrWhiteSpace(TxtGhiChu.Text) ? null : TxtGhiChu.Text.Trim()
        };

        Close(ketQua);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void HienLoi(string thongDiep)
    {
        TxtLoi.Text = thongDiep;
        TxtLoi.IsVisible = true;
    }
}
