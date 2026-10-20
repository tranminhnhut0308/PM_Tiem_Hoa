using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class TraNoNhaCungCapWindow : Window
{
    private static readonly IReadOnlyList<PhuongThucOption> DanhSachPhuongThuc = new[]
    {
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    };

    private readonly decimal _conNo;

    public TraNoNhaCungCapWindow() : this("Nhà cung cấp", 0m)
    {
    }

    public TraNoNhaCungCapWindow(string tenNhaCungCap, decimal conNo)
    {
        InitializeComponent();

        CboPhuongThuc.ItemsSource = DanhSachPhuongThuc;
        CboPhuongThuc.SelectedIndex = 0;

        _conNo = conNo;

        TxtNhaCungCap.Text = string.IsNullOrWhiteSpace(tenNhaCungCap)
            ? "Nhà cung cấp"
            : tenNhaCungCap;
        TxtConNo.Text = $"{conNo:N0} đ";

        // Mặc định trả hết phần còn nợ
        TienChiBox.Value = conNo;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var soTien = TienChiBox.Value;

        if (soTien <= 0)
        {
            HienLoi("Số tiền thanh toán phải lớn hơn 0.");
            return;
        }

        if (soTien > _conNo)
        {
            HienLoi($"Số tiền thanh toán ({soTien.ToString("N0", CultureInfo.InvariantCulture)} đ) "
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
