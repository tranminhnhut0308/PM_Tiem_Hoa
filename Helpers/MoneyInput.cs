using System.Globalization;

namespace ShopHoa.Helpers;

/// <summary>
/// Quy tắc đọc/ghi số tiền theo kiểu Việt Nam.
/// Tách riêng phần tính toán khỏi phần giao diện để dùng chung cho
/// ô nhập và cho việc hiển thị tổng tiền.
/// </summary>
public static class MoneyInput
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Định dạng tiền Việt Nam có dấu phân cách hàng nghìn.
    /// Số nguyên thì không hiện phần thập phân (75.000 chứ không phải 75.000,00).
    /// </summary>
    public static string Format(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("N0", VietnameseCulture)
            : value.ToString("N2", VietnameseCulture);
    }

    /// <summary>Số trần không dấu phân cách, dùng khi người dùng đang gõ/sửa số.</summary>
    public static string ToRawText(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Giá trị lớn nhất mà cột DECIMAL(15,2) của MySQL chịu được (9999999999999.99).
    /// Chặn sớm ở tầng ứng dụng để không đợi tới lúc MySQL báo lỗi.
    /// </summary>
    public const decimal GiaTienToiDa = 9999999999999.99m;

    /// <summary>
    /// Đọc nghiêm ngặt số tiền người dùng gõ, có báo lỗi để hiển thị ngay trên form.
    /// Khác <see cref="Parse"/> ở chỗ: Parse im lặng trả 0 khi sai, còn hàm này nêu rõ lý do.
    /// Chấp nhận "3.842.857", "3842857", "1.500,5" và cho phép giá bằng 0.
    /// </summary>
    public static bool TryParse(
        string? text,
        bool batBuoc,
        out decimal value,
        out string loi)
    {
        value = 0m;
        loi = "";

        var raw = (text ?? "").Trim();

        // Bỏ ký hiệu tiền và mọi dấu phân cách kiểu Việt Nam rồi kiểm tra lại.
        var normalized = raw
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (normalized.Length == 0)
        {
            loi = batBuoc ? "Giá bán không được để trống." : "";
            return !batBuoc;
        }

        // Chỉ có dấu chấm thì coi là dấu phân cách hàng nghìn (kiểu Việt Nam),
        // có dấu phẩy thì dấu phẩy mới là dấu thập phân.
        if (normalized.Contains(','))
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        else
            normalized = normalized.Replace(".", string.Empty);

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value))
        {
            value = 0m;
            loi = "Giá bán chỉ được nhập bằng số, không nhập chữ.";
            return false;
        }

        if (value < 0)
        {
            loi = "Giá bán không được âm.";
            return false;
        }

        if (value > GiaTienToiDa)
        {
            loi = $"Giá bán vượt quá giới hạn cho phép ({Format(GiaTienToiDa)} đ).";
            value = 0m;
            return false;
        }

        // Cột DECIMAL(15,2) chỉ giữ 2 chữ số thập phân.
        value = Math.Round(value, 2, MidpointRounding.AwayFromZero);

        return true;
    }

    /// <summary>
    /// Đọc số từ chuỗi người dùng gõ. Chấp nhận cả "3.842.857", "3842857", "1.500,5".
    /// Không parse được thì trả về 0 để người dùng vẫn nhập tiếp được.
    /// </summary>
    public static decimal Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        var normalized = text
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (normalized.Length == 0)
            return 0m;

        if (normalized.Contains(','))
        {
            // "1.500,5" -> bỏ dấu phân cách nghìn, đổi dấu phẩy thành dấu thập phân
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }
        else
        {
            // Chỉ có dấu chấm -> coi là dấu phân cách hàng nghìn
            normalized = normalized.Replace(".", string.Empty);
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0m;
    }
}
