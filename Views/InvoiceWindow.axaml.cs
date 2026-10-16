using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class InvoiceWindow : Window
{
    /// <summary>
    /// Hàm thu công nợ cho hóa đơn đang xem. Trả về hóa đơn đã đọc lại sau khi
    /// thu để cửa sổ cập nhật số nợ và trạng thái nút. Null khi không thu (hủy
    /// hoặc lỗi). Không truyền gì thì cửa sổ dùng cơ chế thu nợ mặc định qua DatabaseService.
    /// </summary>
    private readonly Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>? _thuCongNo;

    public InvoiceWindow() : this(new HoaDonBanHangReceipt())
    {
    }

    public InvoiceWindow(HoaDonBanHangReceipt receipt)
        : this(receipt, (Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>?)null)
    {
    }

    public InvoiceWindow(
        HoaDonBanHangReceipt receipt,
        Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>? thuCongNo)
    {
        InitializeComponent();
        _thuCongNo = thuCongNo;
        DataContext = receipt;
        CapNhatNutThanhToan(receipt);
    }

    public InvoiceWindow(
        HoaDonBanHangReceipt receipt,
        Func<HoaDonBanHangReceipt, Task<HoaDonBanHangReceipt?>>? thuCongNo)
        : this(receipt, thuCongNo == null ? null : (r, _) => thuCongNo(r))
    {
    }

    private void CapNhatNutThanhToan(HoaDonBanHangReceipt receipt)
    {
        // Yêu cầu: Nếu công nợ là 0 (khách không nợ) thì hóa đơn không có nút thanh toán công nợ.
        // Chỉ hiển thị nút khi hóa đơn còn nợ (ConNo > 0).
        ThanhToanCongNoButton.IsVisible = receipt.ConNo > 0;
    }

    private async void ThanhToanCongNoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt || receipt.ConNo <= 0)
            return;

        ThanhToanCongNoButton.IsEnabled = false;

        try
        {
            HoaDonBanHangReceipt? capNhat = null;

            if (_thuCongNo is not null)
            {
                capNhat = await _thuCongNo(receipt, this);
            }
            else
            {
                capNhat = await ThuCongNoMacDinhAsync(receipt);
            }

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatNutThanhToan(capNhat);
            }
        }
        finally
        {
            ThanhToanCongNoButton.IsEnabled = true;
        }
    }

    private async Task<HoaDonBanHangReceipt?> ThuCongNoMacDinhAsync(HoaDonBanHangReceipt receipt)
    {
        if (receipt.ConNo <= 0)
            return null;

        if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CustomersDebtPay)
            && !Quyen.IsAdmin(Session.CurrentUser))
        {
            await HienThiThongBaoAsync("Thông báo", "Bạn không có quyền thu công nợ khách hàng.");
            return null;
        }

        var hoaDon = new HoaDon
        {
            MaHoaDon = receipt.MaHoaDon,
            TenKhachHang = receipt.TenKhachHang,
            KhachCanTra = receipt.KhachCanTra,
            KhachDaThanhToan = receipt.KhachDaThanhToan,
            ConNo = receipt.ConNo
        };

        var dialog = new ThuNoHoaDonWindow(hoaDon);
        var ketQua = await dialog.ShowDialog<ThuNoHoaDonKetQua?>(this);

        if (ketQua is null)
            return null;

        try
        {
            var db = new DatabaseService();
            var capNhat = await db.ThuNoHoaDonAsync(
                hoaDon.MaHoaDon,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.GhiChu);

            capNhat.ThongTinTiem = receipt.ThongTinTiem;

            var thongBaoPhieu = string.IsNullOrWhiteSpace(capNhat.MaPhieuThu)
                ? ""
                : $"\nMã phiếu thu: {capNhat.MaPhieuThu} (đã ghi vào Sổ quỹ).";

            await HienThiThongBaoAsync(
                "Thu nợ thành công",
                $"Đã thu {ketQua.SoTien:N0} đ cho hóa đơn {hoaDon.MaHoaDon}.\n"
                + $"Còn nợ: {capNhat.ConNo:N0} đ.{thongBaoPhieu}");

            return capNhat;
        }
        catch (Exception ex)
        {
            await HienThiThongBaoAsync("Không thu được công nợ", ex.Message);
            SafeLog.WriteError("Loi thu no hoa don mac dinh trong InvoiceWindow", ex);
            return null;
        }
    }

    private async Task HienThiThongBaoAsync(string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 460,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var okButton = new Button
        {
            Content = "Đã hiểu",
            Width = 100,
            Height = 36,
            Background = Avalonia.Media.Brush.Parse("#087F6E"),
            Foreground = Avalonia.Media.Brushes.White,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14
        });
        panel.Children.Add(okButton);

        window.Content = panel;
        okButton.Click += (_, _) => window.Close();

        await window.ShowDialog(this);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PrintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt)
            return;

        try
        {
            var duongDan = HoaDonPrintService.LuuFileTam(receipt);

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            PrintStatus.IsVisible = false;
        }
        catch (Exception ex)
        {
            PrintStatus.Text = "Không mở được bản in: " + ex.Message;
            PrintStatus.IsVisible = true;
        }
    }
}
