using JTSA.Plugin.Abstractions;
using System.Windows;

namespace JTSA.TimerPlugin;

public sealed class TimerPlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private TimerWindow? window;

    public string Id => "jtsa.timer";
    public string Name => "タイマー";
    public string Description => "配信中に使える独立ウィンドウのカウントダウンタイマーです。";
    public Version Version => new(1, 0, 0);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        context.Log("タイマーを読み込みました。");
    }

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new TimerWindow(context!);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        window?.Close();
        context?.RemoveExpansionOverlay("timer");
        window = null;
    }
}
