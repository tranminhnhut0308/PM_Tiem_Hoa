using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class PhieuNhapDetailWindow : Window
{
    /// <summary>
    /// Hàm thanh toán công nợ cho phiếu nhập đang xem. Trả về chi tiết phiếu đã
    /// đọc lại sau khi thanh toán để cửa sổ cập nhật số nợ/trạng thái nút. Null khi
    /// người dùng hủy hoặc gặp lỗi.
    /// </summary>
    private readonly Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? _thanhToan;

    public PhieuNhapDetailWindow()
        : this(new PhieuNhapHangDetail())
    {
    }

    public PhieuNhapDetailWindow(PhieuNhapHangDetail detail)
        : this(detail, null)
    {
    }

    public PhieuNhapDetailWindow(
        PhieuNhapHangDetail detail,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? thanhToan)
    {
        InitializeComponent();
        _thanhToan = thanhToan;
        DataContext = detail;
        CapNhatNutThanhToan(detail);
    }

    private void CapNhatNutThanhToan(PhieuNhapHangDetail detail)
    {
        // Chỉ hiện nút khi phiếu đã hoàn thành và còn công nợ phải trả.
        ThanhToanButton.IsVisible = detail.CoTheThanhToan;
    }

    private async void ThanhToanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PhieuNhapHangDetail detail || detail.ConNo <= 0m)
            return;

        if (_thanhToan is null)
            return;

        ThanhToanButton.IsEnabled = false;

        try
        {
            var capNhat = await _thanhToan(detail, this);

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatNutThanhToan(capNhat);
            }
        }
        finally
        {
            ThanhToanButton.IsEnabled = true;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
