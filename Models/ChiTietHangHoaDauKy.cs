using System;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho bản ghi chi tiết hàng hóa tồn đầu kỳ
/// (tương thích bảng chi_tiet_hang_hoa_dau_ky và quản lý tồn kho).
/// </summary>
public sealed class ChiTietHangHoaDauKy
{
    public uint Id { get; set; }
    public uint KhoHangId { get; set; }
    public uint HangHoaId { get; set; }
    public uint KyKeToanId { get; set; }
    public decimal SoLuong { get; set; }
    public decimal GiaVon { get; set; }
    public decimal ThanhTien { get; set; }
    public string NoiDung { get; set; } = "Tồn đầu kỳ";
    public DateTime NgayDauKy { get; set; } = DateTime.Today;

    // Hỗ trợ hiển thị
    public string? MaKhoHang { get; set; }
    public string? TenKhoHang { get; set; }
    public string? MaHangHoa { get; set; }
    public string? TenHangHoa { get; set; }
    public string? DonViTinh { get; set; }
}
