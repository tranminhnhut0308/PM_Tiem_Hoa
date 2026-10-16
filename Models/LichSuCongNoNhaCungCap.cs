namespace ShopHoa.Models;

/// <summary>
/// Một dòng của sổ cái công nợ nhà cung cấp (bảng lich_su_cong_no_nha_cung_cap).
/// Dùng cho tab "Công nợ" của vùng chi tiết nhà cung cấp.
/// </summary>
public sealed class LichSuCongNoNhaCungCap
{
    public ulong Id { get; set; }

    /// <summary>Loại giao dịch, ví dụ PHIẾU_NHAP_HANG / PHIEU_CHI / NHAP_HANG.</summary>
    public string LoaiGiaoDich { get; set; } = "";

    /// <summary>Mã chứng từ liên quan, ví dụ mã phiếu nhập hàng.</summary>
    public string? MaChungTu { get; set; }

    /// <summary>Số tiền phát sinh nợ (ghi nợ).</summary>
    public decimal GhiNo { get; set; }

    /// <summary>Số tiền đã trả (ghi có).</summary>
    public decimal GhiCo { get; set; }

    /// <summary>Số dư công nợ sau giao dịch.</summary>
    public decimal SoDu { get; set; }

    public DateTime ThoiGian { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Số tiền phát sinh nợ trong giao dịch này (bỏ qua phần đã trả).</summary>
    public decimal PhatSinhNo => Math.Max(0, GhiNo - GhiCo);

    // ============================================================
    // CHỮ HIỂN THỊ
    // ============================================================

    /// <summary>Định dạng dd/MM/yyyy HH:mm cho cột "Thời gian".</summary>
    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Loại giao dịch đã dịch sang tiếng Việt, mã lạ thì hiện nguyên văn.</summary>
    public string LoaiGiaoDichHienThi => LoaiGiaoDich.Trim().ToUpperInvariant() switch
    {
        "PHIEU_NHAP_HANG" or "NHAP_HANG" or "PHIEU_NHAP" => "Phiếu nhập hàng",
        "PHIEU_CHI" or "CHI" or "THANH_TOAN" => "Phiếu chi",
        "HUY" or "DA_HUY" or "PHIEU_HUY" => "Hủy phiếu",
        "NHAN_HANG" or "TRA_HANG" => "Trả hàng",
        _ => string.IsNullOrWhiteSpace(LoaiGiaoDich) ? "—" : LoaiGiaoDich
    };

    public string PhatSinhNoText => $"{PhatSinhNo:N0} đ";

    public string GhiCoText => $"{GhiCo:N0} đ";

    public string SoDuText => $"{SoDu:N0} đ";

    public string MaChungTuHienThi =>
        string.IsNullOrWhiteSpace(MaChungTu) ? "—" : MaChungTu.Trim();

    public string GhiChuHienThi =>
        string.IsNullOrWhiteSpace(GhiChu) ? "—" : GhiChu.Trim();
}
