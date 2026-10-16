namespace ShopHoa.Models;

using System.Globalization;

public sealed class HoaDon
{
    public uint Id { get; set; }

    public string MaHoaDon { get; set; } = "";

    public DateTime ThoiGianBan { get; set; }

    public string? MaKhachHang { get; set; }

    public string? TenKhachHang { get; set; }

    public decimal TongTienHang { get; set; }

    public decimal GiamGia { get; set; }

    public decimal ThuKhac { get; set; }

    public decimal KhachCanTra { get; set; }

    public decimal KhachDaThanhToan { get; set; }

    public decimal ConNo { get; set; }

    public string TrangThaiThanhToan { get; set; } = "";

    /// <summary>Ghi chú riêng của hóa đơn (yêu cầu riêng của khách...).</summary>
    public string? GhiChu { get; set; }

    // ============================================================
    // THÔNG TIN CHỈ DÙNG CHO GIAO DIỆN
    // ============================================================

    /// <summary>Định dạng dd/MM/yyyy HH:mm cho cột "Thời gian".</summary>
    public string ThoiGianBanText =>
        ThoiGianBan.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TrangThaiThanhToanHienThi => TrangThaiThanhToan switch
    {
        "DA_THANH_TOAN" => "Đã thanh toán",
        "CONG_NO" => "Còn công nợ",
        "HUY" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(TrangThaiThanhToan) ? "Chưa có" : TrangThaiThanhToan
    };

    /// <summary>Hóa đơn này còn dư nợ (dùng để tô màu cảnh báo).</summary>
    public bool ConNoDuong => ConNo > 0;

    /// <summary>Có ghi chú để hiển thị kèm hay không.</summary>
    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);
}