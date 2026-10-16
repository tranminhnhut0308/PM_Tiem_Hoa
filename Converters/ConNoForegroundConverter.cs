using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ShopHoa.Converters;

/// <summary>
/// Đổi màu số tiền cột "Còn nợ" theo giá trị nợ thực tế (không dựa vào
/// tên trạng thái hay định dạng chuỗi):
/// <list type="bullet">
/// <item>Lớn hơn 0: màu đỏ cảnh báo (khách chưa thanh toán đủ).</item>
/// <item>Bằng 0: màu xanh lá (đã thanh toán đủ).</item>
/// <item>Nhỏ hơn 0: màu cam (dữ liệu không hợp lệ, không coi là đã thanh toán).</item>
/// </list>
/// </summary>
public sealed class ConNoForegroundConverter : IValueConverter
{
    public static ConNoForegroundConverter Instance { get; } = new();

    private static readonly IBrush ConNoCanhBao = new SolidColorBrush(Color.Parse("#D64545"));
    private static readonly IBrush DaThanhToanDu = new SolidColorBrush(Color.Parse("#087F6E"));
    private static readonly IBrush DuLieuKhongHopLe = new SolidColorBrush(Color.Parse("#D97706"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var conNo = value switch
        {
            decimal so => so,
            double so => (decimal)so,
            float so => (decimal)so,
            int so => so,
            long so => so,
            _ => 0m
        };

        if (conNo > 0m)
            return ConNoCanhBao;

        return conNo < 0m ? DuLieuKhongHopLe : DaThanhToanDu;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
