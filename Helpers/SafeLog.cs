using System.Text.RegularExpressions;

namespace ShopHoa.Helpers;

/// <summary>
/// Ghi log an toàn: loại bỏ mật khẩu / thông tin bí mật khỏi thông báo lỗi
/// trước khi in ra console hoặc hiển thị cho người dùng.
/// </summary>
public static partial class SafeLog
{
    [GeneratedRegex(
        @"(?i)\b(password|pass|pwd|matkhau|mat_khau|mat_khau_ma|secret|token|apikey|api_key)\b\s*[:=]\s*(""[^""]*""|'[^']*'|[^\s;,&]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    private static readonly Regex ConnectionStringPattern = new(
        @"(?i)\b(password|pwd)\s*=\s*[^;]*",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Chuẩn hoá chuỗi để có thể ghi log an toàn.
    /// </summary>
    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return "";

        var text = ConnectionStringPattern.Replace(message, "$1=***");

        text = SecretPattern().Replace(text, "$1=***");

        return text;
    }

    /// <summary>
    /// Ghi lỗi ra console sau khi đã loại bỏ thông tin bí mật.
    /// </summary>
    public static void WriteError(string tieuDe, Exception ex)
    {
        Console.WriteLine($"========== {tieuDe} ==========");
        Console.WriteLine(Redact(ex.Message));

        if (ex.InnerException is not null)
        {
            Console.WriteLine(Redact(ex.InnerException.Message));
        }

        Console.WriteLine("======================================");
    }
}
