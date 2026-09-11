using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JTSA.Utility;

public sealed class CachedImageConverter : IValueConverter
{
    private static readonly Dictionary<(string Url, int Width), BitmapImage> Images = [];

    // URI画像はWPFの非同期取得を使い、同じURL・表示サイズのデコード結果を共有する。
    public static BitmapImage GetImage(string url, int width = 144)
    {
        lock (Images)
        {
            var key = (url, width);
            if (Images.TryGetValue(key, out var cached)) return cached;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.DecodePixelWidth = width;
            bitmap.UriSource = new Uri(url, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.DownloadFailed += (_, _) => { lock (Images) Images.Remove(key); };
            if (bitmap.CanFreeze) bitmap.Freeze();
            if (Images.Count >= 256) Images.Remove(Images.Keys.First());
            Images[key] = bitmap;
            return bitmap;
        }
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ImageSource source) return source;
        if (value is not string url || string.IsNullOrWhiteSpace(url)) return null;
        try { return GetImage(url, int.TryParse(parameter?.ToString(), out var width) ? width : 144); }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or System.IO.IOException) { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
