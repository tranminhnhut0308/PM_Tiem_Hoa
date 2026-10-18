using System;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Model phiếu xuất hàng trả lại cho Nhà cung cấp (Vendor Return / Trả hàng NCC).
/// Ánh xạ với bảng phieu_xuat_tra_ncc.
/// </summary>
public sealed partial class PhieuXuatTraHang : ObservableObject
{
    public uint Id { get; set; }

    /// <summary>Số thứ tự hiển thị trong danh sách.</summary>
    public int SoThuTu { get; set; }

    public string MaPhieuXuatTra { get; set; } = "";

    public uint ChiNhanhId { get; set; } = 1;

    public uint? NhaCungCapId { get; set; }

    public string? TenNhaCungCap { get; set; }

    public string? MaNhaCungCap { get; set; }

    public string? SoDienThoaiNcc { get; set; }

    public uint? PhieuNhapGocId { get; set; }

    public string? MaPhieuNhapGoc { get; set; }

    public DateTime ThoiGianXuatTra { get; set; } = DateTime.Now;

    public string KhoXuat { get; set; } = "Kho Tổng (Kho mặc định)";

    public decimal TongSoLuong { get; set; }

    public decimal TongTienTra { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>
    /// HOAN_THANH = Đã xuất kho trả NCC & trừ công nợ / nhận hoàn tiền,
    /// DA_HUY = Đã hủy phiếu xuất trả,
    /// DANG_XU_LY = Đang đóng gói / chờ NCC xác nhận.
    /// </summary>
    public string TrangThai { get; set; } = "HOAN_THANH";

    public uint? NguoiTaoId { get; set; }

    public string? TenNguoiTao { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    // ============================================================
    // THÔNG TIN HIỂN THỊ TRÊN GIAO DIỆN
    // ============================================================

    public string ThoiGianXuatTraText =>
        ThoiGianXuatTra.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TongSoLuongText =>
        TongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongTienTraText =>
        TongTienTra.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string NhaCungCapHienThi =>
        string.IsNullOrWhiteSpace(TenNhaCungCap) ? "Nhà cung cấp lẻ / Chưa rõ" : TenNhaCungCap;

    public string PhieuNhapGocHienThi =>
        string.IsNullOrWhiteSpace(MaPhieuNhapGoc) ? "—" : MaPhieuNhapGoc;

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
