using System.Globalization;

namespace ShopHoa.Helpers;

/// <summary>
/// Quy tắc định dạng SỐ LƯỢNG hàng hóa (tồn kho, khách đặt...).
/// Khác với tiền: không gắn ký hiệu tiền tệ và không giữ các số 0 thập phân
/// vô nghĩa do cột DECIMAL(15,3) sinh ra (100.000 phải hiện là 100, không phải
/// "100.000" khiến người dùng tưởng là một trăm nghìn).
/// </summary>
public static class SoLuongFormat
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Chuỗi dùng cho ô nhập liệu: không có dấu phân cách hàng nghìn (để người
    /// dùng sửa số bình thường và không bị nhầm dấu chấm), bỏ số 0 thập phân vô
    /// nghĩa nhưng vẫn giữ phần thập phân thật.
    /// Ví dụ: 100.000 -> "100"; 15.000 -> "15"; 100.500 -> "100.5".
    /// </summary>
    public static string Raw(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    /// <summary>
    /// Chuỗi hiển thị theo văn hóa Việt Nam: có dấu phân cách hàng nghìn, giữ
    /// phần thập phân thật và bỏ số 0 thập phân vô nghĩa.
    /// Ví dụ: 100.000 -> "100"; 1500 -> "1.500"; 100.500 -> "100,5".
    /// </summary>
    public static string HienThi(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("N0", VietnameseCulture)
            : value.ToString("#,0.###", VietnameseCulture);
    }
}
