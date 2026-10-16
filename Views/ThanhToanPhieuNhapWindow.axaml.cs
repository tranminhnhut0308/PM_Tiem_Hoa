using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ThanhToanPhieuNhapWindow : Window
{
    private static readonly IReadOnlyList<PhuongThucOption> DanhSachPhuongThuc = new[]
    {
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    };

    private readonly decimal _tongTienNhap;
    private readonly decimal _conNo;

    public ThanhToanPhieuNhapWindow()
        : this(new PhieuNhapHang())
    {
    }

    public ThanhToanPhieuNhapWindow(PhieuNhapHang phieu)
        : this(
            phieu.MaPhieuNhap,
            phieu.TenNhaCungCap,
            phieu.TongTienNhap,
            phieu.DaThanhToan,
            phieu.ConNo,
            phieu.PhuongThucThanhToan)
    {
    }

    public ThanhToanPhieuNhapWindow(PhieuNhapHangDetail detail)
        : this(
            detail.MaPhieuNhap,
            detail.TenNhaCungCap,
            detail.TongTienNhap,
            detail.DaThanhToan,
            detail.ConNo,
            detail.PhuongThucThanhToan)
    {
    }

    private ThanhToanPhieuNhapWindow(
        string maPhieuNhap,
        string? tenNhaCungCap,
        decimal tongTienNhap,
        decimal daThanhToan,
        decimal conNo,
        string? phuongThuc)
    {
        InitializeComponent();

        CboPhuongThuc.ItemsSource = DanhSachPhuongThuc;
        CboPhuongThuc.SelectedIndex = TimChiSoPhuongThuc(phuongThuc);
        NgayThanhToanBox.SelectedDate = new System.DateTimeOffset(System.DateTime.Today);

        _tongTienNhap = tongTienNhap;
        _conNo = conNo;

        TxtMaPhieuNhap.Text = string.IsNullOrWhiteSpace(maPhieuNhap) ? "—" : maPhieuNhap;
        TxtNhaCungCap.Text = string.IsNullOrWhiteSpace(tenNhaCungCap)
            ? "Không có nhà cung cấp"
            : tenNhaCungCap;
        TxtTongTien.Text = $"{tongTienNhap:N0} đ";
        TxtDaThanhToan.Text = $"{daThanhToan:N0} đ";
        TxtConNo.Text = $"{conNo:N0} đ";

        // Mặc định thanh toán hết phần còn nợ cho tiện.
        TienThanhToanBox.Value = conNo > 0m ? conNo : 0m;
    }

    private static int TimChiSoPhuongThuc(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return 0;

        for (var i = 0; i < DanhSachPhuongThuc.Count; i++)
        {
            if (string.Equals(DanhSachPhuongThuc[i].Ma, ma.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var soTien = TienThanhToanBox.Value;

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

        if (_tongTienNhap <= 0 && _conNo <= 0)
        {
            HienLoi("Phiếu nhập không còn công nợ để thanh toán.");
            return;
        }

        var thoiGian = NgayThanhToanBox.SelectedDate?.DateTime ?? System.DateTime.Now;
        var bayGio = System.DateTime.Now;

        // Giữ đúng giờ phút hiện tại nhưng theo ngày người dùng chọn.
        var thoiGianThanhToan = new System.DateTime(
            thoiGian.Year, thoiGian.Month, thoiGian.Day,
            bayGio.Hour, bayGio.Minute, bayGio.Second);

        var ketQua = new ThanhToanPhieuNhapYeuCau
        {
            SoTien = soTien,
            PhuongThuc = (CboPhuongThuc.SelectedItem as PhuongThucOption)?.Ma ?? "TIEN_MAT",
            ThoiGian = thoiGianThanhToan,
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
