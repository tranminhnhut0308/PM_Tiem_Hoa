using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện một phiếu nhập khác trong phần mềm tiệm hoa WINWON.
/// Ánh xạ với bảng phieu_nhap_khac.
/// Dùng cho các nghiệp vụ: Nhập điều chuyển nội bộ, Nhập điều chỉnh kiểm kê,
/// Nhập hàng tặng/khuyến mãi từ đối tác, Nhập thu hồi từ sự kiện/chi nhánh.
/// </summary>
public sealed partial class PhieuNhapKhac : ObservableObject
{
    public uint Id { get; set; }

    [ObservableProperty]
    private int soThuTu;

    public string MaPhieuNhapKhac { get; set; } = "";

    public uint ChiNhanhId { get; set; } = 1;

    public string LoaiNhapKhac { get; set; } = "NHAP_KHAC";

    public DateTime ThoiGianNhap { get; set; } = DateTime.Now;

    public string KhoNhan { get; set; } = "Kho Tổng (Kho mặc định)";

    public string? DoiTuongLienQuan { get; set; }

    public decimal TongSoLuong { get; set; }

    public decimal TongGiaTri { get; set; }

    public string? GhiChu { get; set; }

    public string TrangThai { get; set; } = "HOAN_THANH";

    public uint? NguoiTaoId { get; set; }

    public string? TenNguoiTao { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    // ============================================================
    // THÔNG TIN HIỂN THỊ
    // ============================================================

    public string ThoiGianNhapText =>
        ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TongSoLuongText =>
        TongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongGiaTriText =>
        TongGiaTri.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string DoiTuongHienThi =>
        string.IsNullOrWhiteSpace(DoiTuongLienQuan) ? "Nội bộ" : DoiTuongLienQuan;

    public string LoaiNhapKhacText => LoaiNhapKhac switch
    {
        "NHAP_NOI_BO" => "Nhập nội bộ",
        "NHAP_DIEU_CHINH" => "Nhập điều chỉnh",
        "NHAP_KHUYEN_MAI" => "Nhập khuyến mãi",
        "NHAP_THU_HOI" => "Nhập thu hồi",
        _ => "Nhập khác"
    };

    public string LoaiNhapKhacBadgeBackground => LoaiNhapKhac switch
    {
        "NHAP_NOI_BO" => "#EFF6FF",
        "NHAP_DIEU_CHINH" => "#FEF3C7",
        "NHAP_KHUYEN_MAI" => "#F0FDF4",
        "NHAP_THU_HOI" => "#F5F3FF",
        _ => "#F1F5F9"
    };

    public string LoaiNhapKhacBadgeForeground => LoaiNhapKhac switch
    {
        "NHAP_NOI_BO" => "#1D4ED8",
        "NHAP_DIEU_CHINH" => "#B45309",
        "NHAP_KHUYEN_MAI" => "#15803D",
        "NHAP_THU_HOI" => "#6D28D9",
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
        "HOAN_THANH" => "#15803D",
        "DA_HUY" => "#B91C1C",
        _ => "#475569"
    };
}
