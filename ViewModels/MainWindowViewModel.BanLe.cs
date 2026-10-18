using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 27 – THÊM PHIẾU BÁN LẺ (HELPERS & QUICK BUTTONS)
    // =========================================================

    /// <summary>
    /// Mã phiếu bán hiển thị xem trước (HDyyMMdd-NNN).
    /// </summary>
    public string SaleMaPhieuHienThi =>
        string.IsNullOrWhiteSpace(SaleMaHoaDonHienThi)
            ? $"HD{DateTime.Now:yyMMdd}-001"
            : SaleMaHoaDonHienThi;

    /// <summary>
    /// Ngày bán hiện tại định dạng dd/MM/yyyy HH:mm.
    /// </summary>
    public string SaleNgayBanHienThi => DateTime.Now.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Nhân viên lập phiếu bán lẻ (lấy từ tài khoản đăng nhập).
    /// </summary>
    public string SaleNhanVienHienThi => HoTenHienThi;

    /// <summary>
    /// Kho xuất bán lẻ (mặc định Kho Tổng).
    /// </summary>
    public string SaleKhoXuatHienThi => "Kho Tổng (Kho mặc định)";

    /// <summary>
    /// Tổng số lượng các sản phẩm có trong giỏ hàng.
    /// </summary>
    public decimal SaleTongSoLuongItem => SaleCart.Sum(i => i.SoLuong);

    /// <summary>
    /// Tổng số dòng mặt hàng trong giỏ bán.
    /// </summary>
    public int SaleTongSoMatHang => SaleCart.Count;

    /// <summary>
    /// Cảnh báo khách lẻ ghi nợ (khách lẻ không được nợ).
    /// </summary>
    public bool SaleCanhBaoKhachLeNo => LaKhachLe && TienCongNo > 0;

    /// <summary>
    /// Lệnh chọn nhanh tiền khách đưa từ các nút mệnh giá:
    /// 50.000, 100.000, 200.000, 500.000 hoặc "DU" (đúng số tiền).
    /// </summary>
    [RelayCommand]
    private void ChonTienKhachDuaNhanh(string? amountStr)
    {
        if (string.IsNullOrWhiteSpace(amountStr))
            return;

        if (string.Equals(amountStr, "DU", StringComparison.OrdinalIgnoreCase))
        {
            TienKhachDua = Math.Max(0m, SaleTotal);
        }
        else if (decimal.TryParse(amountStr, out var val))
        {
            TienKhachDua = val;
        }

        RecalculatePayment();
        NotifyBanLeProperties();
    }

    /// <summary>
    /// Thông báo cập nhật các thuộc tính phụ thuộc của phiếu bán lẻ.
    /// </summary>
    public void NotifyBanLeProperties()
    {
        OnPropertyChanged(nameof(SaleMaPhieuHienThi));
        OnPropertyChanged(nameof(SaleNgayBanHienThi));
        OnPropertyChanged(nameof(SaleNhanVienHienThi));
        OnPropertyChanged(nameof(SaleKhoXuatHienThi));
        OnPropertyChanged(nameof(SaleTongSoLuongItem));
        OnPropertyChanged(nameof(SaleTongSoMatHang));
        OnPropertyChanged(nameof(SaleCanhBaoKhachLeNo));
    }
}
