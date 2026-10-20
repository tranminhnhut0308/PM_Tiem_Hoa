using System;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một chứng từ có công nợ (Hóa đơn bán hàng hoặc Phiếu nhập hàng).
/// </summary>
public sealed class ChungTuNoOption
{
    public string MaChungTu { get; set; } = "";
    public string LoaiChungTu { get; set; } = "Hóa đơn bán hàng";
    public DateTime NgayPhatSinh { get; set; }
    public decimal TongTien { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string? GhiChu { get; set; }

    public string HienThiGoiY =>
        $"{MaChungTu} ({NgayPhatSinh:dd/MM/yyyy}) - Còn nợ: {ConNo:N0} đ / Tổng: {TongTien:N0} đ";

    public override string ToString() => HienThiGoiY;
}

/// <summary>
/// Dữ liệu đầu vào khi thực hiện thanh toán nợ.
/// </summary>
public sealed class ThanhToanNoYeuCau
{
    public string LoaiDoiTac { get; set; } = "KHACH_HANG";
    public uint DoiTacId { get; set; }
    public string MaDoiTac { get; set; } = "";
    public string TenDoiTac { get; set; } = "";
    public string? MaChungTu { get; set; }
    public DateTime NgayThanhToan { get; set; } = DateTime.Today;
    public decimal SoTien { get; set; }
    public string PhuongThuc { get; set; } = "TIEN_MAT";
    public string? GhiChu { get; set; }
}

/// <summary>
/// Dòng kết quả trong màn hình Tìm kiếm và Tra cứu công nợ (Giao diện 48).
/// </summary>
public sealed class CongNoItemRow
{
    public int SoThuTu { get; set; }
    public string LoaiDoiTac { get; set; } = "KHACH_HANG"; // KHACH_HANG hoặc NHA_CUNG_CAP
    public string LoaiCongNo { get; set; } = "Phải thu";  // Phải thu hoặc Phải trả
    public uint DoiTacId { get; set; }
    public string MaDoiTac { get; set; } = "";
    public string TenDoiTac { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string MaChungTu { get; set; } = "";
    public string LoaiChungTu { get; set; } = "Hóa đơn";
    public DateTime NgayPhatSinh { get; set; }
    public decimal TongTien { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string TrangThaiThanhToan => ConNo <= 0 ? "Đã thanh toán đủ" : "Còn nợ";
    public bool CoTheThanhToan => ConNo > 0;

    public string MauLoaiCongNo => LoaiDoiTac == "KHACH_HANG" ? "#D92D20" : "#175CD3";
    public string NenLoaiCongNo => LoaiDoiTac == "KHACH_HANG" ? "#FEE4E2" : "#EFF8FF";

    public string MauTrangThai => ConNo <= 0 ? "#027A48" : "#B42318";
    public string NenTrangThai => ConNo <= 0 ? "#ECFDF3" : "#FEF3F2";
}

/// <summary>
/// Tiêu chí lọc tìm kiếm công nợ.
/// </summary>
public sealed class CongNoFilterCriteria
{
    public string? LoaiDoiTac { get; set; } = "TAT_CA"; // TAT_CA, KHACH_HANG, NHA_CUNG_CAP
    public string? TrangThaiThanhToan { get; set; } = "TAT_CA"; // TAT_CA, CON_NO, DA_THANH_TOAN
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public string? TuKhoa { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Thống kê tổng hợp số liệu công nợ.
/// </summary>
public sealed class CongNoThongKe
{
    public decimal TongNoPhaiThu { get; set; }
    public decimal TongNoPhaiTra { get; set; }
    public decimal TongDaThanhToan { get; set; }
    public decimal TongConNoRong => TongNoPhaiThu - TongNoPhaiTra;
}
