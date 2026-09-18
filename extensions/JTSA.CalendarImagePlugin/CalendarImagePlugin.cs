using JTSA.Plugin.Abstractions;
using System.Windows;

namespace JTSA.CalendarImagePlugin;

public sealed class CalendarImagePlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private CalendarImageWindow? window;

    public string Id => "jtsa.calendar-image";
    public string Name => "カレンダー画像化";
    public string Description => "JTSAの月間予定を配信用のPNG画像として保存します。";
    public Version Version => new(1, 0, 0);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        context.Log("カレンダー画像化を読み込みました。");
    }

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        if (context is not IJtsaCalendarPluginContext calendarContext)
        {
            MessageBox.Show("このJTSAではカレンダー予定の取得に対応していません。", Name);
            return;
        }

        window = new CalendarImageWindow(calendarContext);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        window?.Close();
        window = null;
    }
}
