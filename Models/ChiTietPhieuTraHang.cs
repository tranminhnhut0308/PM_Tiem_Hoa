using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Chi tiết mặt hàng trong phiếu trả hàng.
/// Ánh xạ với bảng chi_tiet_phieu_tra_hang.
/// Hỗ trợ chỉnh sửa số lượng, đơn giá và lý do trả trên giao diện lập phiếu trả lại.
/// </summary>
public sealed partial class ChiTietPhieuTraHang : ObservableObject
{
    public uint Id { get; set; }

    [ObservableProperty]
    private int soThuTu;

    public uint PhieuTraId { get; set; }

    public uint SanPhamId { get; set; }

    public string MaSanPham { get; set; } = "";

    public string TenSanPham { get; set; } = "";

    public string DonViTinh { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThanhTien))]
    [NotifyPropertyChangedFor(nameof(ThanhTienText))]
    [NotifyPropertyChangedFor(nameof(SoLuongText))]
    private decimal soLuong = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThanhTien))]
    [NotifyPropertyChangedFor(nameof(ThanhTienText))]
    [NotifyPropertyChangedFor(nameof(DonGiaText))]
    private decimal donGia;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThanhTienText))]
    private decimal thanhTien;

    [ObservableProperty]
    private string? lyDoTra;

    /// <summary>Số lượng mua trên hóa đơn gốc (nếu có)</summary>
    public decimal SoLuongMua { get; set; }

    /// <summary>Số lượng đã trả trong các phiếu trước (nếu có)</summary>
    public decimal DaTra { get; set; }

    /// <summary>Số lượng tối đa còn được phép trả</summary>
    public decimal SoLuongToiDa => SoLuongMua > 0 ? Math.Max(0, SoLuongMua - DaTra) : 999999;

    public bool HasHoaDonGoc => SoLuongMua > 0;

    // ============================================================
    // THÔNG TIN HIỂN THỊ
    // ============================================================

    public string SoLuongText =>
        SoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string DonGiaText =>
        DonGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string ThanhTienText =>
        ThanhTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public ChiTietPhieuTraHang()
    {
    }

    public ChiTietPhieuTraHang(SanPham sp, decimal donGiaTra = 0, decimal soLuongTra = 1, string? lyDo = null)
    {
        SanPhamId = sp.Id;
        MaSanPham = sp.MaSanPham;
        TenSanPham = sp.TenSanPham;
        DonViTinh = sp.DonViTinh ?? "";
        DonGia = donGiaTra > 0 ? donGiaTra : sp.GiaBan;
        SoLuong = soLuongTra > 0 ? soLuongTra : 1;
        ThanhTien = Math.Round(SoLuong * DonGia, 2);
        LyDoTra = lyDo ?? "Khách đổi trả hàng";
    }

    partial void OnSoLuongChanged(decimal value)
    {
        if (value < 0)
            SoLuong = 0;
        else if (HasHoaDonGoc && value > SoLuongToiDa)
            SoLuong = SoLuongToiDa;

        ThanhTien = Math.Round(SoLuong * DonGia, 2);
    }

    partial void OnDonGiaChanged(decimal value)
    {
        if (value < 0)
            DonGia = 0;

        ThanhTien = Math.Round(SoLuong * DonGia, 2);
    }
}
