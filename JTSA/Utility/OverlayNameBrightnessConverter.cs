using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JTSA.Controls;

/// <summary>
/// オーバーレイの表示名だけに、暗い色ほど強い明度補正をかける。
/// </summary>
public sealed class OverlayNameBrightnessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var source = value as string ?? string.Empty;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(source);
            var luminance = GetLuminance(color);

            // 元から明るい色は変更せず、暗い領域だけを強く持ち上げる。
            const double correctionLimit = 0.58;
            if (luminance >= correctionLimit)
            {
                var originalBrush = new SolidColorBrush(color);
                originalBrush.Freeze();
                return originalBrush;
            }

            var darkness = (correctionLimit - luminance) / correctionLimit;
            var targetLuminance = luminance + (0.75 - luminance) * Math.Sqrt(darkness);
            var whiteMix = luminance >= 1
                ? 0
                : Math.Clamp((targetLuminance - luminance) / (1 - luminance), 0, 1);

            byte Lighten(byte channel) =>
                (byte)Math.Round(channel + (255 - channel) * whiteMix);

            var brush = new SolidColorBrush(Color.FromRgb(
                Lighten(color.R),
                Lighten(color.G),
                Lighten(color.B)));
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.White;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double GetLuminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
}
