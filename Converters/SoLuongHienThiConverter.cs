using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ShopHoa.Helpers;

namespace ShopHoa.Converters;

/// <summary>
/// Hiển thị số lượng hàng hóa (tồn kho, khách đặt...) theo quy ước Việt Nam,
/// giữ phần thập phân thật nhưng bỏ các số 0 thập phân vô nghĩa.
/// Không dùng chung định dạng tiền tệ.
/// </summary>
public sealed class SoLuongHienThiConverter : IValueConverter
{
    public static SoLuongHienThiConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal number ? SoLuongFormat.HienThi(number) : "0";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
