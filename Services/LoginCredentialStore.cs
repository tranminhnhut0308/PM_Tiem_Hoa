using System.Text.Json;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Lưu / đọc trạng thái "Ghi nhớ đăng nhập".
///
/// Cùng pattern với DatabaseConfigService: JSON trong
/// %LocalAppData%\ShopHoa\remember_login.json
///
/// - TenDangNhap lưu dạng rõ (cần thiết để điền lại form).
/// - MatKhau luôn được mã hoá bởi SecretProtector, không bao giờ plain text.
/// - Mọi lỗi đọc/ghi đều bị xử lý êm: hỏng file thì coi như chưa ghi nhớ.
/// </summary>
public sealed class LoginCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public LoginCredentialStore()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShopHoa",
            "remember_login.json");
    }

    public string GetFilePath() => _filePath;

    // =========================================================
    // ĐỌC
    // =========================================================

    /// <summary>
    /// Đọc trạng thái đăng nhập đã ghi nhớ.
    /// Trả về null nếu chưa có, file hỏng, hoặc không giải mã được mật khẩu.
    /// Không bao giờ ném exception.
    /// </summary>
    public RememberedLogin? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            var data = JsonSerializer.Deserialize<RememberedLogin>(
                json,
                JsonOptions);

            if (data is null ||
                string.IsNullOrWhiteSpace(data.TenDangNhap))
            {
                return null;
            }

            // Mật khẩu hỏng / không giải mã được (ví dụ đổi máy, đổi user)
            // thì coi như không có gì để ghi nhớ.
            if (string.IsNullOrWhiteSpace(data.MatKhauDaMaHoa))
                return null;

            var secret = SecretProtector.Unprotect(data.MatKhauDaMaHoa);

            if (string.IsNullOrEmpty(secret))
            {
                Clear();
                return null;
            }

            return data;
        }
        catch
        {
            // File hỏng / không đọc được -> bỏ qua, người dùng nhập lại.
            return null;
        }
    }

    // =========================================================
    // GHI
    // =========================================================

    /// <summary>
    /// Lưu tên đăng nhập + mật khẩu (đã mã hoá).
    /// Trả về false nếu không mã hoá/lưu được.
    /// </summary>
    public bool Save(string tenDangNhap, string matKhau)
    {
        if (string.IsNullOrWhiteSpace(tenDangNhap) ||
            string.IsNullOrEmpty(matKhau))
        {
            return false;
        }

        var encrypted = SecretProtector.Protect(matKhau);

        if (string.IsNullOrEmpty(encrypted))
        {
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new RememberedLogin
            {
                TenDangNhap = tenDangNhap.Trim(),
                MatKhauDaMaHoa = encrypted,
                LuuLuc = DateTime.Now
            };

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(data, JsonOptions));

            return true;
        }
        catch
        {
            return false;
        }
    }

    // =========================================================
    // XOÁ
    // =========================================================

    /// <summary>
    /// Xoá toàn bộ thông tin đăng nhập đã ghi nhớ.
    /// Trả về true nếu sau khi gọi không còn gì được lưu.
    /// </summary>
    public bool Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
