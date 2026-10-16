using System.Security.Cryptography;
using System.Text;

namespace ShopHoa.Services;

/// <summary>
/// Bảo vệ bí mật (mật khẩu) trước khi ghi xuống đĩa.
///
/// - Windows: dùng DPAPI (DataProtectionScope.CurrentUser).
///   Chỉ chính tài khoản Windows đang đăng nhập mới giải mã được,
///   tự động gắn với thông tin đăng nhập hệ điều hành.
///
/// - Linux / macOS: AES-256-GCM với khoá sinh từ PBKDF2 trên một
///   khoá máy ngẫu nhiên lưu ở thư mục dữ liệu ứng dụng với quyền
///   hạn chế (chỉ user hiện tại đọc được trên Unix).
///
/// Mọi lỗi đều được nuốt và trả về null/false để ứng dụng không crash
/// khi dữ liệu ghi nhớ bị hỏng hoặc không tương thích phiên bản khác.
/// </summary>
public static class SecretProtector
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Pbkdf2Iterations = 210_000;

    private const string PrefixDpapi = "dpapi:v1:";
    private const string PrefixAes = "aesgcm:v1:";

    private static readonly object Gate = new();
    private static byte[]? _cachedKey;

    // =========================================================
    // DPAPI (chỉ Windows)
    // =========================================================

    private static string ProtectWithDpapi(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);

        byte[] cipher;

        if (OperatingSystem.IsWindows())
        {
            cipher = ProtectedData.Protect(
                data,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "DPAPI chỉ hoạt động trên Windows.");
        }

        return PrefixDpapi + Convert.ToBase64String(cipher);
    }

    private static string? UnprotectWithDpapi(string payload)
    {
        var cipher = Convert.FromBase64String(payload);

        if (!OperatingSystem.IsWindows())
            return null;

        var data = ProtectedData.Unprotect(
            cipher,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(data);
    }

    // =========================================================
    // AES-256-GCM (fallback cho Linux/macOS)
    // =========================================================

    private static string GetAppDataDirectory()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ShopHoa");

        Directory.CreateDirectory(directory);

        return directory;
    }

    /// <summary>
    /// Khoá máy 32 byte, tạo 1 lần rồi giữ lại. Trên Unix được đặt quyền 600
    /// để chỉ user hiện tại đọc được.
    /// </summary>
    private static byte[] GetOrCreateMachineKey()
    {
        lock (Gate)
        {
            if (_cachedKey is not null)
                return _cachedKey;

            var keyPath = Path.Combine(
                GetAppDataDirectory(),
                "credential.key");

            if (File.Exists(keyPath))
            {
                var existing = Convert.FromBase64String(
                    File.ReadAllText(keyPath).Trim());

                if (existing.Length == KeySize)
                {
                    _cachedKey = existing;
                    return existing;
                }
            }

            var key = RandomNumberGenerator.GetBytes(KeySize);

            File.WriteAllText(keyPath, Convert.ToBase64String(key));

            TryRestrictFilePermissions(keyPath);

            _cachedKey = key;

            return key;
        }
    }

    private static void TryRestrictFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Không đặt được quyền cũng không ảnh hưởng tới luồng đăng nhập.
        }
    }

    private static byte[] DeriveKey(byte[] machineKey, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            machineKey,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    private static string ProtectWithAesGcm(string plainText)
    {
        var machineKey = GetOrCreateMachineKey();

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(DeriveKey(machineKey, salt), TagSize))
        {
            aes.Encrypt(
                nonce,
                plainBytes,
                cipher,
                tag);
        }

        var payload = new byte[SaltSize + NonceSize + TagSize + cipher.Length];

        Buffer.BlockCopy(salt, 0, payload, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, payload, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, payload, SaltSize + NonceSize + TagSize, cipher.Length);

        return PrefixAes + Convert.ToBase64String(payload);
    }

    private static string? UnprotectWithAesGcm(string payload)
    {
        var raw = Convert.FromBase64String(payload);

        if (raw.Length < SaltSize + NonceSize + TagSize)
            return null;

        var salt = raw.AsSpan(0, SaltSize).ToArray();
        var nonce = raw.AsSpan(SaltSize, NonceSize).ToArray();
        var tag = raw.AsSpan(SaltSize + NonceSize, TagSize).ToArray();
        var cipher = raw.AsSpan(SaltSize + NonceSize + TagSize).ToArray();

        var machineKey = GetOrCreateMachineKey();

        var plainBytes = new byte[cipher.Length];

        using (var aes = new AesGcm(DeriveKey(machineKey, salt), TagSize))
        {
            aes.Decrypt(
                nonce,
                cipher,
                tag,
                plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    // =========================================================
    // API CHÍNH
    // =========================================================

    /// <summary>
    /// Mã hoá chuỗi bí mật. Trả về null nếu không mã hoá được.
    /// </summary>
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        try
        {
            return OperatingSystem.IsWindows()
                ? ProtectWithDpapi(plainText)
                : ProtectWithAesGcm(plainText);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Giải mã chuỗi đã mã hoá. Trả về null nếu dữ liệu hỏng,
    /// sai định dạng hoặc không thuộc về máy/user hiện tại.
    /// </summary>
    public static string? Unprotect(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            if (payload.StartsWith(PrefixDpapi, StringComparison.Ordinal))
            {
                return UnprotectWithDpapi(payload[PrefixDpapi.Length..]);
            }

            if (payload.StartsWith(PrefixAes, StringComparison.Ordinal))
            {
                return UnprotectWithAesGcm(payload[PrefixAes.Length..]);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
