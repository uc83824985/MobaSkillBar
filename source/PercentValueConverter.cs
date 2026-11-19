using System.Globalization;
using System.Windows.Data;

namespace MobaSkillBar;

public sealed class PercentValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double fraction ? (fraction * 100).ToString("0.##", culture) + "%" : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string number = ((string)value).Trim().TrimEnd('%', '％').TrimEnd();
        return double.TryParse(number, NumberStyles.Float, culture, out double percent) && double.IsFinite(percent)
            ? percent / 100 : Binding.DoNothing;
    }
}
