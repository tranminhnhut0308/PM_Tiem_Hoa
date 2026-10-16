using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ShopHoa.Converters;

/// <summary>
/// Nối <c>RadioButton</c> của Dashboard với một chuỗi khoá (chuỗi khoảng thời gian,
/// chiều hay kiểu xếp hạng) trong <c>DashboardViewModel</c>.
/// <para>
/// Dùng chung cho cả chiều OneWay (hiển thị) và TwoWay (bấm chuột). Với chiều
/// ghi, <see cref="BindingOperations.DoNothing"/> được trả về khi nút vừa được
/// bỏ tick để tránh ghi đè giá trị đang chọn bằng chuỗi rỗng.
/// </para>
/// </summary>
public sealed class TongQuanChonRadioConverter : IValueConverter
{
    public static TongQuanChonRadioConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var giaTriHienTai = value?.ToString();
        var giaTriNut = parameter?.ToString();

        if (string.IsNullOrEmpty(giaTriNut))
            return false;

        return string.Equals(giaTriHienTai, giaTriNut, StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true)
            return BindingOperations.DoNothing;

        return parameter?.ToString();
    }
}