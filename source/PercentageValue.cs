using System.Globalization;

namespace MobaSkillBar;

internal static class PercentageValue
{
    public static string Format(double fraction, CultureInfo culture) =>
        (fraction * 100).ToString("0.##", culture) + "%";

    public static bool TryParse(string text, CultureInfo culture, out double fraction)
    {
        string number = text.Trim().TrimEnd('%', '％').TrimEnd();
        bool valid = double.TryParse(number, NumberStyles.Float, culture, out double percent) && double.IsFinite(percent);
        fraction = percent / 100;
        return valid;
    }
}
