namespace ShopHoa.Models;

/// <summary>
/// Số liệu tổng hợp của sổ quỹ, luôn tính đúng theo bộ lọc đang áp dụng.
/// </summary>
public sealed class SoQuyTongHop
{
    /// <summary>
    /// Quỹ đầu kỳ: tiền dư của các phiếu có trước mốc bắt đầu khoảng thời gian
    /// (thu trừ chi). Xem toàn thời gian thì bằng 0.
    /// </summary>
    public decimal QuyDauKy { get; set; }

    /// <summary>Tổng tiền thu trong khoảng đang xem.</summary>
    public decimal TongThu { get; set; }

    /// <summary>Tổng tiền chi trong khoảng đang xem.</summary>
    public decimal TongChi { get; set; }

    /// <summary>Tổng số phiếu khớp bộ lọc (kể cả phiếu đã hủy).</summary>
    public int TongSoPhieu { get; set; }

    private decimal? _tonQuy;

    /// <summary>Công thức nghiệp vụ: tồn quỹ = quỹ đầu kỳ + tổng thu - tổng chi (hoặc được gán trực tiếp theo bộ lọc).</summary>
    public decimal TonQuy
    {
        get => _tonQuy ?? (QuyDauKy + TongThu - TongChi);
        set => _tonQuy = value;
    }
}
