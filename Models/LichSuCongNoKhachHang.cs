using System;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng trong sổ cái công nợ khách hàng (lich_su_cong_no_khach_hang).
/// </summary>
public sealed class LichSuCongNoKhachHang
{
    public ulong Id { get; set; }

    public string LoaiGiaoDich { get; set; } = "";

    public string? LoaiChungTu { get; set; }

    public string? MaChungTu { get; set; }

    public decimal GhiNo { get; set; }

    public decimal GhiCo { get; set; }

    public decimal SoDu { get; set; }

    public DateTime ThoiGian { get; set; }

    public string? GhiChu { get; set; }

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    public string LoaiGiaoDichHienThi => LoaiGiaoDich.Trim().ToUpperInvariant() switch
    {
        "NO_DAU_KY" => "Nợ đầu kỳ",
        "BAN_HANG" or "HOA_DON" or "GHI_NO" => "Bán hàng ghi nợ",
        "THU_TIEN" or "THANH_TOAN" => "Thu tiền",
        "DIEU_CHINH" or "DIEU_CHINH_TANG" => "Điều chỉnh tăng",
        "DIEU_CHINH_GIAM" => "Điều chỉnh giảm",
        "TRA_HANG" or "TRA_LAI" => "Trả hàng",
        "HUY" or "DA_HUY" => "Hủy chứng từ",
        _ => string.IsNullOrWhiteSpace(LoaiGiaoDich) ? "—" : LoaiGiaoDich
    };

    public string PhatSinhNoText => $"{GhiNo:N0} đ";

    public string GhiCoText => $"{GhiCo:N0} đ";

    public string SoDuText => $"{SoDu:N0} đ";

    public string MaChungTuHienThi =>
        string.IsNullOrWhiteSpace(MaChungTu) ? "—" : MaChungTu.Trim();

    public string GhiChuHienThi =>
        string.IsNullOrWhiteSpace(GhiChu) ? "—" : GhiChu.Trim();
}
