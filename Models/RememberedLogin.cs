namespace ShopHoa.Models;

/// <summary>
/// Dữ liệu đăng nhập được ghi nhớ trên máy.
/// MatKhau luôn ở dạng đã mã hoá, không bao giờ lưu plain text.
/// </summary>
public sealed class RememberedLogin
{
    public string TenDangNhap { get; set; } = "";

    /// <summary>
    /// Mật khẩu đã mã hoá (DPAPI trên Windows, AES-256-GCM trên nền tảng khác).
    /// Chuỗi rỗng nghĩa là không có credential nào được lưu.
    /// </summary>
    public string MatKhauDaMaHoa { get; set; } = "";

    public DateTime? LuuLuc { get; set; }
}
