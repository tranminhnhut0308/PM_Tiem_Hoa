using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Model phiếu xuất hàng (xuất kho).
/// </summary>
public sealed partial class PhieuXuatHang : ObservableObject
{
    public uint Id { get; set; }

    /// <summary>Số thứ tự hiển thị trong danh sách.</summary>
    public int SoThuTu { get; set; }

    public string MaPhieuXuat { get; set; } = "";

    public uint ChiNhanhId { get; set; } = 1;

    public string KhoXuat { get; set; } = "Kho Tổng (Kho mặc định)";

    /// <summary>
    /// XUAT_HUY = Xuất hủy / hao hụt / hết hạn,
    /// XUAT_NOI_BO = Xuất tiêu hao nội bộ / trưng bày,
    /// XUAT_TRA_NCC = Xuất trả nhà cung cấp,
    /// XUAT_CHUYEN_KHO = Xuất chuyển kho,
    /// XUAT_KHAC = Xuất khác.
    /// </summary>
    public string LoaiXuat { get; set; } = "XUAT_HUY";

    public string? DoiTuongNhan { get; set; }

    public uint? KhachHangId { get; set; }

    public uint? NguoiXuatId { get; set; }

    public string? TenNhanVien { get; set; }

    public DateTime ThoiGianXuat { get; set; } = DateTime.Now;

    public decimal TongSoLuong { get; set; }

    public decimal TongTien { get; set; }

    public string? GhiChu { get; set; }

    public string TrangThai { get; set; } = "DA_XUAT_KHO";

    // ============================================================
    // THÔNG TIN HIỂN THỊ
    // ============================================================

    public string ThoiGianXuatText =>
        ThoiGianXuat.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TongSoLuongText =>
        TongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongTienText =>
        TongTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string LoaiXuatHienThi => LoaiXuat switch
    {
        "XUAT_HUY" => "Xuất hủy / Hao hụt",
        "XUAT_NOI_BO" => "Xuất sử dụng nội bộ",
        "XUAT_TRA_NCC" => "Xuất trả nhà cung cấp",
        "XUAT_CHUYEN_KHO" => "Xuất chuyển kho",
        _ => "Xuất khác"
    };

    public string TrangThaiHienThi => TrangThai switch
    {
        "LUU_TAM" => "Lưu tạm",
        "DA_HUY" => "Đã hủy",
        _ => "Đã xuất kho"
    };

    public IBrush TrangThaiBadgeBrush => TrangThai switch
    {
        "LUU_TAM" => new SolidColorBrush(Color.Parse("#FEF3C7")),
        "DA_HUY" => new SolidColorBrush(Color.Parse("#F1F5F9")),
        _ => new SolidColorBrush(Color.Parse("#E0F2FE"))
    };

    public IBrush TrangThaiBadgeForeground => TrangThai switch
    {
        "LUU_TAM" => new SolidColorBrush(Color.Parse("#B45309")),
        "DA_HUY" => new SolidColorBrush(Color.Parse("#64748B")),
        _ => new SolidColorBrush(Color.Parse("#0369A1"))
    };
}
