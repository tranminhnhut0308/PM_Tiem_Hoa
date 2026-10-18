using System;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Model phiếu nhập trả lại hàng từ khách hàng (hàng bán bị trả lại).
/// Ánh xạ với bảng phieu_tra_hang.
/// </summary>
public sealed partial class PhieuTraHang : ObservableObject
{
    public uint Id { get; set; }

    /// <summary>Số thứ tự hiển thị trong danh sách.</summary>
    public int SoThuTu { get; set; }

    public string MaPhieuTra { get; set; } = "";

    public uint ChiNhanhId { get; set; } = 1;

    public uint? KhachHangId { get; set; }

    public string? TenKhachHang { get; set; }

    public string? MaKhachHang { get; set; }

    public string? SoDienThoaiKhach { get; set; }

    public uint? HoaDonId { get; set; }

    public string? MaHoaDon { get; set; }

    public DateTime ThoiGianTra { get; set; } = DateTime.Now;

    public string KhoNhan { get; set; } = "Kho Tổng (Kho mặc định)";

    public decimal TongSoLuong { get; set; }

    public decimal TongTienTra { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>
    /// HOAN_THANH = Đã hoàn tất và nhập lại kho / hoàn tiền cho khách,
    /// DA_HUY = Đã hủy phiếu trả hàng,
    /// DANG_XU_LY = Đang xử lý kiểm hàng.
    /// </summary>
    public string TrangThai { get; set; } = "HOAN_THANH";

    public uint? NguoiTaoId { get; set; }

    public string? TenNguoiTao { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    // ============================================================
    // THÔNG TIN HIỂN THỊ TRÊN GIAO DIỆN
    // ============================================================

    public string ThoiGianTraText =>
        ThoiGianTra.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TongSoLuongText =>
        TongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongTienTraText =>
        TongTienTra.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string KhachHangHienThi =>
        string.IsNullOrWhiteSpace(TenKhachHang) ? "Khách lẻ" : TenKhachHang;

    public string HoaDonGocHienThi =>
        string.IsNullOrWhiteSpace(MaHoaDon) ? "—" : MaHoaDon;

    public string TrangThaiText => TrangThai switch
    {
        "HOAN_THANH" => "Hoàn thành",
        "DA_HUY" => "Đã hủy",
        "DANG_XU_LY" => "Đang xử lý",
        _ => TrangThai
    };

    public IBrush MauNenTrangThai => TrangThai switch
    {
        "HOAN_THANH" => Brush.Parse("#DEF7EC"),
        "DA_HUY" => Brush.Parse("#FDE8E8"),
        _ => Brush.Parse("#FEF08A")
    };

    public IBrush MauChuTrangThai => TrangThai switch
    {
        "HOAN_THANH" => Brush.Parse("#03543F"),
        "DA_HUY" => Brush.Parse("#9B1C1C"),
        _ => Brush.Parse("#854D0E")
    };

    public IBrush MauBorderTrangThai => TrangThai switch
    {
        "HOAN_THANH" => Brush.Parse("#BCF0DA"),
        "DA_HUY" => Brush.Parse("#F8B4B4"),
        _ => Brush.Parse("#FDE047")
    };
}
