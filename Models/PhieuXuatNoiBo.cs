using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện một phiếu xuất hàng nội bộ trong phần mềm tiệm hoa WINWON.
/// Ánh xạ với bảng phieu_xuat_noi_bo.
/// Dùng cho các nghiệp vụ:
/// - Xuất chuyển kho / chuyển chi nhánh
/// - Xuất cấp phát cho bộ phận cắm hoa / quầy bán
/// - Xuất sử dụng nội bộ (trang trí tiệm, sự kiện công ty)
/// - Xuất hủy hỏng hoa dập nát, héo úa
/// - Xuất nội bộ khác
/// </summary>
public sealed partial class PhieuXuatNoiBo : ObservableObject
{
    public uint Id { get; set; }

    [ObservableProperty]
    private int soThuTu;

    public string MaPhieuXuatNoiBo { get; set; } = "";

    public uint ChiNhanhId { get; set; } = 1;

    public string LoaiXuatNoiBo { get; set; } = "XUAT_CHUYEN_KHO";

    public DateTime ThoiGianXuat { get; set; } = DateTime.Now;

    public string KhoXuat { get; set; } = "Kho Tổng (Kho mặc định)";

    public string? KhoNhanHoacBoPhan { get; set; }

    public decimal TongSoLuong { get; set; }

    public decimal TongGiaTri { get; set; }

    public string? GhiChu { get; set; }

    public string TrangThai { get; set; } = "HOAN_THANH";

    public uint? NguoiTaoId { get; set; }

    public string? TenNguoiTao { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    // ============================================================
    // THÔNG TIN ĐỊNH DẠNG HIỂN THỊ
    // ============================================================

    public string ThoiGianXuatText =>
        ThoiGianXuat.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TongSoLuongText =>
        TongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongGiaTriText =>
        TongGiaTri.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string BoPhanNhanHienThi =>
        string.IsNullOrWhiteSpace(KhoNhanHoacBoPhan) ? "Nội bộ" : KhoNhanHoacBoPhan;

    public string LoaiXuatNoiBoText => LoaiXuatNoiBo switch
    {
        "XUAT_CHUYEN_KHO" => "Xuất chuyển kho",
        "XUAT_CAP_PHAT" => "Xuất cấp phát",
        "XUAT_SU_DUNG" => "Xuất sử dụng",
        "XUAT_HUY_HONG" => "Xuất hủy hỏng",
        _ => "Xuất khác"
    };

    public string LoaiXuatBadgeBackground => LoaiXuatNoiBo switch
    {
        "XUAT_CHUYEN_KHO" => "#EFF6FF",
        "XUAT_CAP_PHAT" => "#F0FDF4",
        "XUAT_SU_DUNG" => "#F5F3FF",
        "XUAT_HUY_HONG" => "#FEF2F2",
        _ => "#F1F5F9"
    };

    public string LoaiXuatBadgeForeground => LoaiXuatNoiBo switch
    {
        "XUAT_CHUYEN_KHO" => "#1D4ED8",
        "XUAT_CAP_PHAT" => "#15803D",
        "XUAT_SU_DUNG" => "#6D28D9",
        "XUAT_HUY_HONG" => "#B91C1C",
        _ => "#475569"
    };

    public string TrangThaiText => TrangThai switch
    {
        "HOAN_THANH" => "Hoàn thành",
        "DA_HUY" => "Đã hủy",
        _ => TrangThai
    };

    public string TrangThaiBadgeBackground => TrangThai switch
    {
        "HOAN_THANH" => "#DCFCE7",
        "DA_HUY" => "#FEE2E2",
        _ => "#F1F5F9"
    };

    public string TrangThaiBadgeForeground => TrangThai switch
    {
        "HOAN_THANH" => "#166534",
        "DA_HUY" => "#991B1B",
        _ => "#475569"
    };
}
