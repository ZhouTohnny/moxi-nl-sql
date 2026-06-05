using System.Globalization;
using System.Windows.Data;

namespace NL2SQL.GUI;

/// <summary>
/// 文本截断转换器
/// </summary>
public class TruncateConverter : IValueConverter
{
    public int MaxLength { get; set; } = 100;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && text.Length > MaxLength)
            return text[..MaxLength] + "...";
        return value ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
