using JTSA.Plugin.Abstractions;
using System.Windows;

namespace JTSA.ExternalAppsPlugin;

public sealed class ExternalAppsPlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private ExternalAppsWindow? window;

    public string Id => "jtsa.external-apps";
    public string Name => "外部アプリ";
    public string Description => "登録した外部アプリの起動、配置、停止をまとめて管理します。";
    public Version Version => new(1, 0, 0);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        pluginContext.Log("外部アプリ管理を読み込みました。");
    }

    public void Open()
    {
        if (context is null) return;
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new ExternalAppsWindow(context);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        window?.Close();
        window = null;
        context = null;
    }
}
