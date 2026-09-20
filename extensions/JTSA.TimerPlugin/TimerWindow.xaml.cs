using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using JTSA.Plugin.Abstractions;
using System.Net;

namespace JTSA.TimerPlugin;

public partial class TimerWindow : Window
{
    private readonly DispatcherTimer timer;
    private TimeSpan remaining = TimeSpan.FromMinutes(5);
    private DateTime targetUtc;
    private bool isPaused;
    private readonly IJtsaPluginContext context;

    public TimerWindow(IJtsaPluginContext context)
    {
        this.context = context;
        InitializeComponent();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += Timer_Tick;
        Closed += (_, _) =>
        {
            timer.Stop();
            context.RemoveExpansionOverlay("timer");
        };
        Render();
    }

    private void ShowInExpansionCheckBox_Changed(object sender, RoutedEventArgs e) => PublishOverlay();

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (timer.IsEnabled) return;
        if (!isPaused && !TryReadDuration()) return;

        targetUtc = DateTime.UtcNow + remaining;
        isPaused = false;
        timer.Start();
        MinutesTextBox.IsEnabled = false;
        StatusTextBlock.Text = "計測中";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateRemaining();
        timer.Stop();
        isPaused = remaining > TimeSpan.Zero;
        MinutesTextBox.IsEnabled = true;
        StatusTextBlock.Text = "一時停止";
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        timer.Stop();
        isPaused = false;
        MinutesTextBox.IsEnabled = true;
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
            StatusTextBlock.Text = "時間になりました。";
            Activate();
        }
        Render();
    }

    private void UpdateRemaining() =>
        remaining = targetUtc > DateTime.UtcNow ? targetUtc - DateTime.UtcNow : TimeSpan.Zero;

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

        var time = WebUtility.HtmlEncode(TimeTextBlock.Text);
        var status = WebUtility.HtmlEncode(StatusTextBlock.Text);
        context.SetExpansionOverlay(new ExpansionOverlayContent(
            "timer",
            $"<div style=\"box-sizing:border-box;width:100%;height:100%;display:flex;flex-direction:column;align-items:center;justify-content:center;color:white;background:rgba(20,20,20,.72);border:3px solid rgba(255,255,255,.8);border-radius:18px;font-family:'Segoe UI',sans-serif;text-shadow:0 3px 8px #000\"><div style=\"font:700 86px Consolas,monospace;line-height:1\">{time}</div><div style=\"font-size:22px;margin-top:12px\">{status}</div></div>",
            710, 70, 500, 190));
    }
}
