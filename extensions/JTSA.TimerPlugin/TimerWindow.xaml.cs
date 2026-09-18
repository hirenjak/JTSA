using System.Globalization;
using System.Media;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using JTSA.Plugin.Abstractions;
using Microsoft.Win32;
using System.Net;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace JTSA.TimerPlugin;

public partial class TimerWindow : Window
{
    private readonly DispatcherTimer timer;
    private TimeSpan remaining = TimeSpan.FromMinutes(5);
    private TimeSpan activeDuration = TimeSpan.FromMinutes(5);
    private DateTime targetUtc;
    private bool isPaused;
    private bool isLoadingOverlaySettings = true;
    private string completionSoundPath = string.Empty;
    private readonly MediaPlayer completionSoundPlayer = new();
    private readonly IJtsaPluginContext context;
    private readonly List<TimerHistoryEntry> timerHistory = [];
    public ObservableCollection<string> History { get; } = [];

    public TimerWindow(IJtsaPluginContext context)
    {
        this.context = context;
        InitializeComponent();
        LoadOverlaySettings();
        isLoadingOverlaySettings = false;
        DataContext = this;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += Timer_Tick;
        Closed += (_, _) =>
        {
            timer.Stop();
            completionSoundPlayer.Close();
            context.RemoveExpansionOverlay("timer");
        };
        completionSoundPlayer.MediaFailed += (_, args) =>
            context.LogError("タイマーの終了音を再生できませんでした。", args.ErrorException);
        Render();
    }

    private void ShowInExpansionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!isLoadingOverlaySettings)
            PublishOverlay();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void OverlayBoundsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isLoadingOverlaySettings || OverlayXTextBox is null ||
            OverlayYTextBox is null || OverlayScaleTextBox is null)
            return;

        if (TryReadOverlayBounds(out _, out _, out _, out _))
        {
            SaveOverlaySettings();
            PublishOverlay();
        }
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (timer.IsEnabled)
            StopTimer();
        else
            StartTimer();
    }

    private void StartTimer()
    {
        if (timer.IsEnabled) return;
        if (!isPaused && !TryReadDuration()) return;

        if (!isPaused)
        {
            activeDuration = remaining;
            AddHistoryEntry(activeDuration);
        }

        targetUtc = DateTime.UtcNow + remaining;
        isPaused = false;
        timer.Start();
        MinutesTextBox.IsEnabled = false;
        StartStopButton.Content = "停止";
        StatusTextBlock.Text = "計測中";
    }

    private void StopTimer()
    {
        UpdateRemaining();
        timer.Stop();
        isPaused = remaining > TimeSpan.Zero;
        MinutesTextBox.IsEnabled = true;
        StartStopButton.Content = "開始";
        StatusTextBlock.Text = "一時停止";
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        timer.Stop();
        isPaused = false;
        MinutesTextBox.IsEnabled = true;
        StartStopButton.Content = "開始";
        if (!TryReadDuration()) remaining = TimeSpan.FromMinutes(5);
        StatusTextBlock.Text = "リセットしました。";
        Render();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateRemaining();
        if (remaining <= TimeSpan.Zero)
        {
            timer.Stop();
            isPaused = false;
            MinutesTextBox.IsEnabled = true;
            StartStopButton.Content = "開始";
            StatusTextBlock.Text = "時間になりました。";
            PlayCompletionSound();
            Activate();
        }
        Render();
    }

    private void UpdateRemaining() =>
        remaining = targetUtc > DateTime.UtcNow ? targetUtc - DateTime.UtcNow : TimeSpan.Zero;

    private void AddHistoryEntry(TimeSpan duration)
    {
        var entry = new TimerHistoryEntry(DateTime.Now, duration.TotalMinutes);
        var existingIndex = timerHistory.FindIndex(item =>
            Math.Abs(item.Minutes - entry.Minutes) < 0.0000001);
        if (existingIndex >= 0)
        {
            timerHistory.RemoveAt(existingIndex);
            History.RemoveAt(existingIndex);
        }
        timerHistory.Insert(0, entry);
        History.Insert(0, FormatHistoryEntry(entry));
        while (timerHistory.Count > 20)
            timerHistory.RemoveAt(timerHistory.Count - 1);
        while (History.Count > 20)
            History.RemoveAt(History.Count - 1);
        SaveOverlaySettings();
    }

    private static string FormatHistoryEntry(TimerHistoryEntry entry) =>
        FormatDuration(TimeSpan.FromMinutes(entry.Minutes));

    private void HistoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox { SelectedIndex: >= 0 } listBox ||
            listBox.SelectedIndex >= timerHistory.Count)
            return;

        var selectedDuration = TimeSpan.FromMinutes(timerHistory[listBox.SelectedIndex].Minutes);
        timer.Stop();
        remaining = selectedDuration;
        activeDuration = selectedDuration;
        isPaused = false;
        MinutesTextBox.Text = timerHistory[listBox.SelectedIndex].Minutes
            .ToString(CultureInfo.CurrentCulture);
        MinutesTextBox.IsEnabled = true;
        StartStopButton.Content = "開始";
        StatusTextBlock.Text = string.Empty;
        Render();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        return totalHours > 0
            ? $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private bool TryReadDuration()
    {
        if (!double.TryParse(MinutesTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var minutes) ||
            minutes <= 0 || minutes > 1440)
        {
            StatusTextBlock.Text = "1～1440分で入力してください。";
            return false;
        }

        remaining = TimeSpan.FromMinutes(minutes);
        Render();
        return true;
    }

    private void Render()
    {
        var totalHours = (int)remaining.TotalHours;
        TimeTextBlock.Text = totalHours > 0
            ? $"{totalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
        PublishOverlay();
    }

    private void PublishOverlay()
    {
        if (ShowInExpansionCheckBox?.IsChecked != true)
        {
            context.RemoveExpansionOverlay("timer");
            return;
        }

        if (!TryReadOverlayBounds(out var overlayX, out var overlayY,
                out var overlayWidth, out var overlayHeight))
            return;

        var time = WebUtility.HtmlEncode(TimeTextBlock.Text);
        var status = WebUtility.HtmlEncode(StatusTextBlock.Text);
        var statusHtml = string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : $"<div style=\"font-size:22px;margin-top:12px\">{status}</div>";
        const int baseWidth = 500;
        const int baseHeight = 190;
        var displayScale = overlayWidth / (double)baseWidth;
        context.SetExpansionOverlay(new ExpansionOverlayContent(
            "timer",
            $"<div style=\"box-sizing:border-box;width:{baseWidth}px;height:{baseHeight}px;transform:scale({displayScale:0.#####});transform-origin:top left;display:flex;flex-direction:column;align-items:center;justify-content:center;color:white;background:rgba(20,20,20,.72);border:3px solid rgba(255,255,255,.8);border-radius:18px;font-family:'Segoe UI',sans-serif;text-shadow:0 3px 8px #000\"><div style=\"font:700 86px Consolas,monospace;line-height:1\">{time}</div>{statusHtml}</div>",
            overlayX, overlayY, overlayWidth, overlayHeight));
    }

    private void PlayCompletionSound()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(completionSoundPath))
            {
                if (!IOFile.Exists(completionSoundPath))
                {
                    StatusTextBlock.Text = "終了音のファイルが見つからないため、標準音を再生しました。";
                    SystemSounds.Exclamation.Play();
                    return;
                }

                completionSoundPlayer.Stop();
                completionSoundPlayer.Close();
                completionSoundPlayer.Open(new Uri(completionSoundPath, UriKind.Absolute));
                completionSoundPlayer.Play();
                return;
            }

            SystemSounds.Exclamation.Play();
        }
        catch (Exception ex)
        {
            context.LogError("タイマーの終了音を再生できませんでした。", ex);
        }
    }

    private void ChangeCompletionSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "タイマーの終了音を選択",
            Filter = "音声ファイル|*.wav;*.mp3;*.aac;*.wma;*.m4a|すべてのファイル|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        completionSoundPath = dialog.FileName;
        UpdateCompletionSoundName();
        SaveOverlaySettings();
    }

    private void UseDefaultCompletionSoundButton_Click(object sender, RoutedEventArgs e)
    {
        completionSoundPlayer.Stop();
        completionSoundPath = string.Empty;
        UpdateCompletionSoundName();
        SaveOverlaySettings();
    }

    private void PreviewCompletionSoundButton_Click(object sender, RoutedEventArgs e) => PlayCompletionSound();

    private void UpdateCompletionSoundName() =>
        CompletionSoundNameTextBlock.Text = string.IsNullOrWhiteSpace(completionSoundPath)
            ? "標準音"
            : IOPath.GetFileName(completionSoundPath);

    private void LoadOverlaySettings()
    {
        var path = IOPath.Combine(context.DataDirectory, "timer-settings.json");
        if (!IOFile.Exists(path)) return;

        try
        {
            var settings = JsonSerializer.Deserialize<TimerOverlaySettings>(IOFile.ReadAllText(path));
            if (settings is null || settings.X < 0 || settings.Y < 0 ||
                settings.ScalePercent is < 25 or > 200)
                return;

            OverlayXTextBox.Text = settings.X.ToString(CultureInfo.InvariantCulture);
            OverlayYTextBox.Text = settings.Y.ToString(CultureInfo.InvariantCulture);
            OverlayScaleTextBox.Text = settings.ScalePercent.ToString(CultureInfo.InvariantCulture);
            completionSoundPath = settings.CompletionSoundPath ?? string.Empty;
            UpdateCompletionSoundName();
            foreach (var entry in settings.TimerHistory ?? [])
            {
                if (entry.Minutes is <= 0 or > 1440 || timerHistory.Any(item =>
                        Math.Abs(item.Minutes - entry.Minutes) < 0.0000001))
                    continue;
                timerHistory.Add(entry);
                History.Add(FormatHistoryEntry(entry));
                if (timerHistory.Count == 20) break;
            }
            if (timerHistory.Count > 0)
            {
                MinutesTextBox.Text = timerHistory[0].Minutes.ToString(CultureInfo.CurrentCulture);
                remaining = TimeSpan.FromMinutes(timerHistory[0].Minutes);
            }
        }
        catch (Exception ex)
        {
            context.LogError("タイマーの表示設定を読み込めませんでした。", ex);
        }
    }

    private void SaveOverlaySettings()
    {
        if (!int.TryParse(OverlayXTextBox.Text, out var x) ||
            !int.TryParse(OverlayYTextBox.Text, out var y) ||
            !int.TryParse(OverlayScaleTextBox.Text, out var scalePercent))
            return;

        try
        {
            var path = IOPath.Combine(context.DataDirectory, "timer-settings.json");
            IOFile.WriteAllText(path, JsonSerializer.Serialize(
                new TimerOverlaySettings(
                    x, y, scalePercent, completionSoundPath, timerHistory.ToArray())));
        }
        catch (Exception ex)
        {
            context.LogError("タイマーの表示設定を保存できませんでした。", ex);
        }
    }

    private bool TryReadOverlayBounds(out int x, out int y, out int width, out int height)
    {
        const int baseWidth = 500;
        const int baseHeight = 190;
        var hasX = int.TryParse(OverlayXTextBox.Text, out x);
        var hasY = int.TryParse(OverlayYTextBox.Text, out y);
        var hasScale = int.TryParse(OverlayScaleTextBox.Text, out var scalePercent);
        width = (int)Math.Round(baseWidth * scalePercent / 100d);
        height = (int)Math.Round(baseHeight * scalePercent / 100d);
        return hasX && hasY && hasScale && x >= 0 && y >= 0 &&
               scalePercent is >= 25 and <= 200;
    }
}

public sealed record TimerOverlaySettings(
    int X,
    int Y,
    int ScalePercent,
    string? CompletionSoundPath = null,
    TimerHistoryEntry[]? TimerHistory = null);

public sealed record TimerHistoryEntry(DateTime StartedAt, double Minutes);
