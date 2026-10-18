namespace ShopHoa.Models;

using System.Globalization;
using Avalonia.Media;

public sealed class HoaDon
{
    public uint Id { get; set; }

    /// <summary>Số thứ tự hiển thị trên danh sách theo phân trang.</summary>
    public int SoThuTu { get; set; }

    public string MaHoaDon { get; set; } = "";

    public DateTime ThoiGianBan { get; set; }

    public string? MaKhachHang { get; set; }

    public string? TenKhachHang { get; set; }

    public string? TenNhanVien { get; set; }

    public string? SoDienThoai { get; set; }

    public string? PhuongThucThanhToan { get; set; }

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

    public string TongTienHangText =>
        TongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiamGiaText =>
        GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string KhachCanTraText =>
        KhachCanTra.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string KhachDaThanhToanText =>
        KhachDaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string ConNoText =>
        ConNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string PhuongThucThanhToanHienThi => PhuongThucThanhToan switch
    {
        "CHUYEN_KHOAN" or "NGAN_HANG" => "Chuyển khoản",
        "THE" => "Thẻ",
        "VI_DIEN_TU" => "Ví điện tử",
        _ => "Tiền mặt"
    };

    public string TrangThaiThanhToanHienThi => TrangThaiThanhToan switch
    {
        "DA_THANH_TOAN" => "Đã thanh toán",
        "CONG_NO" => "Còn công nợ",
        "HUY" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(TrangThaiThanhToan) ? "Chưa có" : TrangThaiThanhToan
    };

    public IBrush TrangThaiBadgeBrush => TrangThaiThanhToan switch
    {
        "DA_THANH_TOAN" => new SolidColorBrush(Color.Parse("#E0F2FE")),
        "CONG_NO" => new SolidColorBrush(Color.Parse("#FEE2E2")),
        "HUY" => new SolidColorBrush(Color.Parse("#F1F5F9")),
        _ => new SolidColorBrush(Color.Parse("#E2E8F0"))
    };

    public IBrush TrangThaiBadgeForeground => TrangThaiThanhToan switch
    {
        "DA_THANH_TOAN" => new SolidColorBrush(Color.Parse("#0369A1")),
        "CONG_NO" => new SolidColorBrush(Color.Parse("#B91C1C")),
        "HUY" => new SolidColorBrush(Color.Parse("#64748B")),
        _ => new SolidColorBrush(Color.Parse("#334155"))
    };

    /// <summary>Hóa đơn này còn dư nợ (dùng để tô màu cảnh báo).</summary>
    public bool ConNoDuong => ConNo > 0;

    public IBrush ConNoForeground => ConNo > 0
        ? new SolidColorBrush(Color.Parse("#DC2626"))
        : new SolidColorBrush(Color.Parse("#059669"));

    /// <summary>Có ghi chú để hiển thị kèm hay không.</summary>
    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    public bool DaHuy => TrangThaiThanhToan == "HUY";

    public bool CoTheHuy => TrangThaiThanhToan != "HUY";
}