using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ShopHoa.Converters;

/// <summary>
/// Nối một <c>RadioButton</c> với một giá trị chuỗi của ViewModel.
///
/// RadioButton chỉ hiểu <c>IsChecked</c> kiểu bool, còn bộ lọc lưu chuỗi
/// ("tatca" / "conno" / "khongconno"). Converter này so chuỗi đang chọn với
/// <c>ConverterParameter</c> để tô đúng nút radio, và khi người dùng bấm vào
/// một nút thì trả ngược chuỗi đó về cho ViewModel.
///
/// Nhiều nút radio dùng chung một <c>GroupName</c> nên Avalonia tự lo trừ
/// việc bỏ tick nút cũ; phần "bỏ tick" đó trả về
/// <see cref="BindingOperations.DoNothing"/> để không ghi đè giá trị mới.
/// </summary>
public sealed class LuaChonRadioConverter : IValueConverter
{
    public static LuaChonRadioConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var giaTriHienTai = value?.ToString();
        var giaTriCuaNut = parameter?.ToString();

        if (string.IsNullOrEmpty(giaTriCuaNut))
            return false;

        return string.Equals(giaTriHienTai, giaTriCuaNut, StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Chỉ ghi đè khi nút này được tick. Nút vừa bị bỏ tick sẽ trả false và
        // bị bỏ qua, nếu không giá trị chọn sẽ bị xoá mất.
        if (value is not true)
            return BindingOperations.DoNothing;

        return parameter?.ToString();
    }
}
