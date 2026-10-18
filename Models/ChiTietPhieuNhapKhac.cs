using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Chi tiết mặt hàng trong phiếu nhập khác.
/// Ánh xạ với bảng chi_tiet_phieu_nhap_khac.
/// </summary>
public sealed partial class ChiTietPhieuNhapKhac : ObservableObject
{
    public uint Id { get; set; }

    [ObservableProperty]
    private int soThuTu;

    public uint PhieuNhapKhacId { get; set; }

    public uint SanPhamId { get; set; }

    public string MaSanPham { get; set; } = "";

    public string TenSanPham { get; set; } = "";

    public string DonViTinh { get; set; } = "";

    [ObservableProperty]
    private decimal tonKho;

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
    private string? ghiChu;

    public string TonKhoText =>
        TonKho.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string SoLuongText =>
        SoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string DonGiaText =>
        DonGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string ThanhTienText =>
        ThanhTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public ChiTietPhieuNhapKhac()
    {
    }

    public ChiTietPhieuNhapKhac(SanPham sp, decimal donGia = 0, decimal soLuong = 1, string? ghiChu = null)
    {
        SanPhamId = sp.Id;
        MaSanPham = sp.MaSanPham ?? "";
        TenSanPham = sp.TenSanPham ?? "";
        DonViTinh = sp.DonViTinh ?? "";
        TonKho = sp.TonKhoExcel;
        DonGia = donGia;
        SoLuong = soLuong;
        ThanhTien = Math.Round(soLuong * donGia, 2);
        GhiChu = ghiChu;
    }

    partial void OnSoLuongChanged(decimal value)
    {
        if (value < 0) SoLuong = 0;
        ThanhTien = Math.Round(SoLuong * DonGia, 2);
    }

    partial void OnDonGiaChanged(decimal value)
    {
        if (value < 0) DonGia = 0;
        ThanhTien = Math.Round(SoLuong * DonGia, 2);
    }
}
