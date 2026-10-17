using System;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho bản ghi công nợ đầu kỳ của đối tác (Khách hàng hoặc Nhà cung cấp).
/// </summary>
public sealed class ChiTietCongNoDauKy
{
    public uint Id { get; set; }

    /// <summary>
    /// Loại đối tác: "KHACH_HANG" (Phải thu khách hàng) hoặc "NHA_CUNG_CAP" (Phải trả nhà cung cấp).
    /// </summary>
    public string LoaiDoiTac { get; set; } = "KHACH_HANG";

    public uint DoiTacId { get; set; }
    public string MaDoiTac { get; set; } = "";
    public string TenDoiTac { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public decimal SoTien { get; set; }
    public DateTime NgayDauKy { get; set; } = DateTime.Today;
    public DateTime? NgayHenTra { get; set; }
    public string NoiDung { get; set; } = "Nợ đầu kỳ";
    public uint ChiNhanhId { get; set; } = 1;
    public uint? KyKeToanId { get; set; }
    public DateTime? NgayTao { get; set; }
}

/// <summary>
/// Dùng để hiển thị và tìm kiếm gợi ý đối tác trong AutoCompleteBox.
/// </summary>
public sealed class DoiTacOption
{
    public uint Id { get; init; }
    public string MaDoiTac { get; init; } = "";
    public string TenDoiTac { get; init; } = "";
    public string? SoDienThoai { get; init; }
    public string LoaiDoiTac { get; init; } = "KHACH_HANG";
    public decimal CongNoHienTai { get; init; }

    public string HienThiGoiY => string.IsNullOrWhiteSpace(SoDienThoai)
        ? $"{MaDoiTac} - {TenDoiTac}"
        : $"{MaDoiTac} - {TenDoiTac} ({SoDienThoai})";

    public override string ToString() => TenDoiTac;
}
