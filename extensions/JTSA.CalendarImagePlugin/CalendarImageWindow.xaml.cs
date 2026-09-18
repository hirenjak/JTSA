using JTSA.Plugin.Abstractions;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JTSA.CalendarImagePlugin;

public partial class CalendarImageWindow : Window
{
    private const int ImageWidth = 1600;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly IJtsaCalendarPluginContext context;
    private DateTime displayedWeek = StartOfWeek(DateTime.Today);
    private RenderTargetBitmap? renderedImage;

    public CalendarImageWindow(IJtsaCalendarPluginContext context)
    {
        this.context = context;
        InitializeComponent();
        Loaded += async (_, _) => await RenderCalendarAsync();
    }

    private async void PreviousMonthButton_Click(object sender, RoutedEventArgs e)
    {
        displayedWeek = displayedWeek.AddDays(-7);
        await RenderCalendarAsync();
    }

    private async void NextMonthButton_Click(object sender, RoutedEventArgs e)
    {
        displayedWeek = displayedWeek.AddDays(7);
        await RenderCalendarAsync();
    }

    private async void CurrentMonthButton_Click(object sender, RoutedEventArgs e)
    {
        displayedWeek = StartOfWeek(DateTime.Today);
        await RenderCalendarAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RenderCalendarAsync();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (renderedImage is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "カレンダー画像を保存",
            Filter = "PNG画像|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = $"JTSA-calendar-week-{displayedWeek:yyyy-MM-dd}.png"
        };
        if (dialog.ShowDialog(this) != true) return;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderedImage));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
        StatusTextBlock.Text = $"保存しました：{Path.GetFileName(dialog.FileName)}";
    }

    private async Task RenderCalendarAsync()
    {
        StatusTextBlock.Text = "予定とBoxArtを読み込んでいます…";
        var weekEnd = displayedWeek.AddDays(7);
        var entries = context.GetCalendarEntries(displayedWeek, weekEnd)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.StartTime)
            .ToArray();
        var images = await LoadBoxArtsAsync(entries);
        var days = Enumerable.Range(0, 7)
            .Select(index =>
            {
                var date = displayedWeek.AddDays(index);
                return new CalendarDay(date, entries.Where(entry => entry.Date == date).ToArray());
            })
            .ToArray();
        var imageHeight = CalculateImageHeight(days);

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            DrawCalendar(drawing, days, images, imageHeight);

        renderedImage = new RenderTargetBitmap(ImageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32);
        renderedImage.Render(visual);
        renderedImage.Freeze();
        PreviewImage.Source = renderedImage;
        MonthTextBlock.Text = FormatWeekRange(displayedWeek);
        StatusTextBlock.Text = $"{entries.Length}件の予定・{ImageWidth}×{imageHeight}px";
    }

    private static int CalculateImageHeight(IEnumerable<CalendarDay> days) =>
        145 + days.Sum(day => Math.Max(94, day.Entries.Length * 104)) + 45;

    private void DrawCalendar(DrawingContext drawing, IReadOnlyList<CalendarDay> days,
        IReadOnlyDictionary<string, BitmapSource> images, int imageHeight)
    {
        var background = new SolidColorBrush(Color.FromRgb(24, 29, 34));
        var panel = new SolidColorBrush(Color.FromRgb(36, 43, 49));
        var border = new Pen(new SolidColorBrush(Color.FromRgb(76, 91, 99)), 2);
        drawing.DrawRectangle(background, null, new Rect(0, 0, ImageWidth, imageHeight));

        DrawText(drawing, FormatWeekRange(displayedWeek), 48, FontWeights.Bold, Brushes.White,
            new Point(64, 28));
        DrawText(drawing, "JTSA WEEKLY STREAM SCHEDULE", 18, FontWeights.SemiBold,
            new SolidColorBrush(Color.FromRgb(84, 196, 184)), new Point(66, 91));

        const double left = 55;
        const double width = 1490;
        const double dateWidth = 220;
        var y = 132d;
        foreach (var day in days)
        {
            var rowHeight = Math.Max(94, day.Entries.Length * 104);
            var headerBrush = day.Date.DayOfWeek == DayOfWeek.Sunday ? Brushes.LightCoral
                : day.Date.DayOfWeek == DayOfWeek.Saturday ? Brushes.LightSkyBlue : Brushes.White;
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(38, 86, 83)), null,
                new Rect(left, y, dateWidth, rowHeight));
            DrawText(drawing, day.Date.ToString("M/d", CultureInfo.InvariantCulture), 31,
                FontWeights.Bold, headerBrush, new Point(left + 22, y + 17));
            DrawText(drawing, day.Date.ToString("dddd", CultureInfo.GetCultureInfo("ja-JP")), 18,
                FontWeights.SemiBold, headerBrush, new Point(left + 24, y + 57));

            drawing.DrawRectangle(panel, border, new Rect(left + dateWidth, y, width - dateWidth, rowHeight));
            var entryY = y;
            foreach (var entry in day.Entries)
            {
                var artRect = new Rect(left + dateWidth + 10, entryY + 5, 61, 86);
                if (!string.IsNullOrWhiteSpace(entry.CategoryBoxArtUrl) &&
                    images.TryGetValue(entry.CategoryBoxArtUrl, out var image))
                    drawing.DrawImage(image, artRect);
                else
                    drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(55, 63, 69)), null, artRect);

                DrawText(drawing, entry.StartTime.ToString("hh\\:mm"), 23, FontWeights.Bold,
                    new SolidColorBrush(Color.FromRgb(84, 196, 184)), new Point(left + dateWidth + 90, entryY + 13));
                DrawWrappedText(drawing, entry.ResolvedTitle, 23, FontWeights.SemiBold, Brushes.White,
                    new Point(left + dateWidth + 205, entryY + 7), width - dateWidth - 225, 55);
                DrawText(drawing, Trim(entry.CategoryName, 76), 16, FontWeights.Normal, Brushes.LightGray,
                    new Point(left + dateWidth + 205, entryY + 68));
                entryY += 104;
                if (entryY < y + rowHeight)
                    drawing.DrawLine(border, new Point(left + dateWidth, entryY - 5), new Point(left + width, entryY - 5));
            }
            y += rowHeight;
        }
    }

    private static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-(int)date.DayOfWeek);

    private static string FormatWeekRange(DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(6);
        return weekStart.Year == weekEnd.Year
            ? $"{weekStart:yyyy年 M月d日} — {weekEnd:M月d日}"
            : $"{weekStart:yyyy年 M月d日} — {weekEnd:yyyy年 M月d日}";
    }

    private static async Task<IReadOnlyDictionary<string, BitmapSource>> LoadBoxArtsAsync(
        IEnumerable<CalendarEntryInfo> entries)
    {
        var urls = entries.Select(entry => entry.CategoryBoxArtUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().ToArray();
        var results = await Task.WhenAll(urls.Select(LoadBoxArtAsync));
        return results.Where(result => result.Image is not null)
            .ToDictionary(result => result.Url, result => result.Image!);
    }

    private static async Task<(string Url, BitmapSource? Image)> LoadBoxArtAsync(string url)
    {
        try
        {
            var resolvedUrl = url.Replace("{width}", "144", StringComparison.OrdinalIgnoreCase)
                .Replace("{height}", "192", StringComparison.OrdinalIgnoreCase);
            var bytes = await HttpClient.GetByteArrayAsync(resolvedUrl);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 144;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return (url, bitmap);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or NotSupportedException)
        {
            return (url, null);
        }
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private static void DrawText(DrawingContext drawing, string text, double size, FontWeight weight,
        Brush brush, Point origin)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("ja-JP"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Yu Gothic UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size, brush, 1.0);
        drawing.DrawText(formatted, origin);
    }

    private static void DrawWrappedText(DrawingContext drawing, string text, double size, FontWeight weight,
        Brush brush, Point origin, double maxWidth, double maxHeight)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("ja-JP"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Yu Gothic UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size, brush, 1.0)
        {
            MaxTextWidth = maxWidth,
            MaxTextHeight = maxHeight,
            Trimming = TextTrimming.CharacterEllipsis,
            LineHeight = 27
        };
        drawing.DrawText(formatted, origin);
    }

    private sealed record CalendarDay(DateTime Date, CalendarEntryInfo[] Entries);

}
