using System;

namespace ShopHoa.Models;

/// <summary>
/// Một bản ghi số dư quỹ đầu kỳ (bảng so_quy_dau_ky).
/// Người dùng khai báo số tiền hiện có trong từng quỹ tại thời điểm bắt đầu
/// sử dụng phần mềm; số này được cộng vào "quỹ đầu kỳ" khi tính tồn quỹ.
/// </summary>
public sealed class SoQuyDauKy
{
    public uint Id { get; set; }

    public DateTime NgayDauKy { get; set; } = DateTime.Today;

    /// <summary>Mã quỹ (khớp cột phuong_thuc của phiếu thu/chi, ví dụ TIEN_MAT).</summary>
    public string PhuongThuc { get; set; } = "TIEN_MAT";

    public decimal SoTien { get; set; }

    public string? NoiDung { get; set; }

    public uint ChiNhanhId { get; set; } = 1;

    public DateTime? NgayTao { get; set; }

    public string NgayDauKyHienThi => NgayDauKy.ToString("dd/MM/yyyy");

    public string TenQuy => TenHienThiQuy(PhuongThuc);

    public string SoTienHienThi => $"{SoTien:N0} đ";

    /// <summary>Nhãn hiển thị thân thiện cho mã quỹ (phương thức thanh toán).</summary>
    public static string TenHienThiQuy(string? ma)
    {
        return (ma ?? "").Trim().ToUpperInvariant() switch
        {
            "TIEN_MAT" or "" => "Tiền mặt",
            "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Ngân hàng",
            "THE" or "THE_TIN_DUNG" => "Thẻ",
            "VI_DIEN_TU" or "VI" => "Ví điện tử",
            _ => ma ?? ""
        };
    }
}
