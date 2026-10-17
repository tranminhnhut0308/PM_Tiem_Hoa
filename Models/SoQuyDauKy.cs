using System;
using Avalonia.Media;
using ShopHoa.Services;

namespace ShopHoa.Models;

/// <summary>
/// Một bản ghi số dư quỹ đầu kỳ (bảng so_quy_dau_ky).
/// Người dùng khai báo số tiền hiện có trong từng quỹ tại thời điểm bắt đầu
/// sử dụng phần mềm; số này được cộng vào "quỹ đầu kỳ" khi tính tồn quỹ.
/// </summary>
public sealed class SoQuyDauKy
{
    public uint Id { get; set; }

    public int Stt { get; set; }

    public DateTime NgayDauKy { get; set; } = DateTime.Today;

    /// <summary>Mã quỹ (khớp cột phuong_thuc của phiếu thu/chi, ví dụ TIEN_MAT, NGAN_HANG).</summary>
    public string PhuongThuc { get; set; } = "TIEN_MAT";

    public decimal SoTien { get; set; }

    public string? NoiDung { get; set; }

    public uint ChiNhanhId { get; set; } = 1;

    public DateTime? NgayTao { get; set; }

    public string NgayDauKyHienThi => NgayDauKy.ToString("dd/MM/yyyy");

    public string TenQuy => TenHienThiQuy(PhuongThuc);

    public string TenQuyIcon => (PhuongThuc ?? "").Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" or "" => "💵 Tiền mặt",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "💳 Ngân hàng",
        "THE" or "THE_TIN_DUNG" => "💳 Thẻ tín dụng",
        "VI_DIEN_TU" or "VI" => "📱 Ví điện tử",
        _ => $"💰 {PhuongThuc}"
    };

    public string SoTienHienThi => $"{SoTien:N0} đ";

    public string QuyBadgeBg => (PhuongThuc ?? "").Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" or "" => "#F0FDF4",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "#EFF6FF",
        "VI_DIEN_TU" or "VI" => "#FAF5FF",
        _ => "#F8FAFC"
    };

    public string QuyBadgeFg => (PhuongThuc ?? "").Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" or "" => "#166534",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "#1E40AF",
        "VI_DIEN_TU" or "VI" => "#7E22CE",
        _ => "#334155"
    };

    public string QuyBadgeBorder => (PhuongThuc ?? "").Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" or "" => "#BBF7D0",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "#BFDBFE",
        "VI_DIEN_TU" or "VI" => "#E9D5FF",
        _ => "#E2E8F0"
    };

    public IBrush QuyBadgeBrush => new SolidColorBrush(Color.Parse(QuyBadgeBg));
    public IBrush QuyBadgeFgBrush => new SolidColorBrush(Color.Parse(QuyBadgeFg));
    public IBrush QuyBadgeBorderBrush => new SolidColorBrush(Color.Parse(QuyBadgeBorder));

    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookOpeningBalance) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookOpeningBalance) || Quyen.IsAdmin(Session.CurrentUser));

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
