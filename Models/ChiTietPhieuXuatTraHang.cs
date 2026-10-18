using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Chi tiết mặt hàng trong phiếu xuất hàng trả lại Nhà cung cấp.
/// Ánh xạ với bảng chi_tiet_phieu_xuat_tra_ncc.
/// Hỗ trợ chỉnh sửa số lượng, đơn giá, lý do và ràng buộc số lượng theo phiếu nhập gốc.
/// </summary>
public sealed partial class ChiTietPhieuXuatTraHang : ObservableObject
{
    public uint Id { get; set; }

    [ObservableProperty]
    private int soThuTu;

    public uint PhieuXuatTraId { get; set; }

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
    private string? lyDoXuatTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TonKhoText))]
    private decimal tonKho;

    /// <summary>Số lượng đã mua trên phiếu nhập gốc (nếu có)</summary>
    public decimal SoLuongMua { get; set; }

    /// <summary>Số lượng đã trả trước đó cho phiếu nhập này (nếu có)</summary>
    public decimal DaXuatTra { get; set; }

    /// <summary>Số lượng tối đa còn được phép xuất trả</summary>
    public decimal SoLuongToiDa => SoLuongMua > 0 ? Math.Max(0, SoLuongMua - DaXuatTra) : 999999;

    public bool HasPhieuNhapGoc => SoLuongMua > 0;

    // ============================================================
    // THÔNG TIN HIỂN THỊ
    // ============================================================

    public string SoLuongText =>
        SoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string DonGiaText =>
        DonGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string ThanhTienText =>
        ThanhTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string TonKhoText =>
        TonKho.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public ChiTietPhieuXuatTraHang()
    {
    }

    public ChiTietPhieuXuatTraHang(SanPham sp, decimal donGiaTra = 0, decimal soLuongTra = 1, string? lyDo = null)
    {
        SanPhamId = sp.Id;
        MaSanPham = sp.MaSanPham;
        TenSanPham = sp.TenSanPham;
        DonViTinh = sp.DonViTinh ?? "";
        TonKho = sp.TonKhoExcel;
        DonGia = donGiaTra > 0 ? donGiaTra : (sp.GiaVon > 0 ? sp.GiaVon : sp.GiaBan);
        SoLuong = soLuongTra > 0 ? soLuongTra : 1;
        ThanhTien = Math.Round(SoLuong * DonGia, 2);
        LyDoXuatTra = lyDo ?? "Trả hàng Nhà cung cấp do lỗi / dập nát";
    }

    partial void OnSoLuongChanged(decimal value)
    {
        if (value < 0)
            SoLuong = 0;
        else if (HasPhieuNhapGoc && value > SoLuongToiDa)
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
