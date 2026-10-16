using System.Globalization;
using Avalonia.Data.Converters;

namespace ShopHoa.Converters;

public sealed class ThousandsSeparatorConverter : IValueConverter
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal number)
            return number.ToString("N0", VietnameseCulture);

        return "0";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        var normalized = text.Replace(".", string.Empty).Replace(",", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0m;
    }
}
