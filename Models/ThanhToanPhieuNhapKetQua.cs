namespace ShopHoa.Models;

/// <summary>
/// Trạng thái thanh toán công nợ của phiếu nhập hàng. Trạng thái này được SUY RA
/// từ cặp số liệu (đã thanh toán, còn nợ) chứ không lưu thành cột riêng, để tránh
/// trùng nguồn dữ liệu với cột <c>da_thanh_toan</c>/<c>con_no</c> của phieu_nhap_hang.
/// </summary>
public static class TrangThaiThanhToanPhieuNhap
{
    public const string ChuaThanhToan = "CHUA_THANH_TOAN";
    public const string ThanhToanMotPhan = "THANH_TOAN_MOT_PHAN";
    public const string DaThanhToan = "DA_THANH_TOAN";

    /// <summary>Phiếu ở trạng thái này mới được phép ghi nhận thanh toán.</summary>
    public static bool LaPhieuThanhToanDuoc(string? trangThaiPhieu)
        => !string.Equals(trangThaiPhieu, "LUU_TAM", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(trangThaiPhieu, "HUY", StringComparison.OrdinalIgnoreCase);

    /// <summary>Suy ra mã trạng thái thanh toán từ số đã trả và số còn nợ.</summary>
    public static string Tinh(decimal daThanhToan, decimal conNo)
        => conNo <= 0m
            ? DaThanhToan
            : daThanhToan > 0m
                ? ThanhToanMotPhan
                : ChuaThanhToan;

    /// <summary>Nhãn tiếng Việt của mã trạng thái thanh toán.</summary>
    public static string HienThi(string? maTrangThai) => maTrangThai switch
    {
        DaThanhToan => "Đã thanh toán",
        ThanhToanMotPhan => "Thanh toán một phần",
        ChuaThanhToan => "Chưa thanh toán",
        _ => "—"
    };
}

/// <summary>
/// Kết quả người dùng nhập ở hộp thoại thanh toán công nợ phiếu nhập.
/// Trả về null khi người dùng hủy.
/// </summary>
public sealed class ThanhToanPhieuNhapYeuCau
{
    /// <summary>Số tiền trả cho phiếu nhập trong lần này.</summary>
    public decimal SoTien { get; init; }

    /// <summary>Mã phương thức thanh toán (TIEN_MAT, CHUYEN_KHOAN...).</summary>
    public string PhuongThuc { get; init; } = "TIEN_MAT";

    /// <summary>Thời điểm ghi nhận thanh toán.</summary>
    public DateTime ThoiGian { get; init; } = DateTime.Now;

    /// <summary>Ghi chú của lần thanh toán (không bắt buộc).</summary>
    public string? GhiChu { get; init; }
}

/// <summary>
/// Kết quả sau khi ghi nhận thanh toán công nợ cho một phiếu nhập hàng: cập nhật
/// đầu phiếu, lưu lịch sử thanh toán, ghi giảm công nợ nhà cung cấp và tạo phiếu chi
/// trong sổ quỹ.
/// </summary>
public sealed class ThanhToanPhieuNhapKetQua
{
    public uint PhieuNhapId { get; init; }

    public string MaPhieuNhap { get; init; } = "";

    public string? TenNhaCungCap { get; init; }

    public decimal TongTienNhap { get; init; }

    /// <summary>Tổng số tiền đã trả của phiếu sau lần thanh toán này.</summary>
    public decimal DaThanhToan { get; init; }

    /// <summary>Số công nợ còn lại của phiếu sau lần thanh toán này.</summary>
    public decimal ConNo { get; init; }

    /// <summary>Số tiền vừa trả trong lần thanh toán này.</summary>
    public decimal SoTienThanhToan { get; init; }

    /// <summary>Mã lịch sử thanh toán nhà cung cấp (TTNCC....).</summary>
    public string MaThanhToan { get; init; } = "";

    /// <summary>Mã phiếu chi đã ghi vào sổ quỹ (PC....).</summary>
    public string? MaPhieuChi { get; init; }

    /// <summary>Mã trạng thái thanh toán sau cùng (xem <see cref="TrangThaiThanhToanPhieuNhap"/>).</summary>
    public string TrangThaiThanhToan { get; init; } = "";
}
