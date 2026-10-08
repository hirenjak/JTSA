using JTSA.Plugin.Abstractions;
using System.Windows;

namespace JTSA.MultiPlatformPlugin;

public sealed class MultiPlatformPlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private MultiPlatformWindow? window;

    public string Id => "jtsa.multi-platform";
    public string Name => "外部プラットフォーム連携";
    public string Description => "YouTubeとKickの配信タイトル・カテゴリ設定とコメント取得をまとめます。";
    public Version Version => new(1, 1, 20260929);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        context.Log("外部プラットフォーム連携を読み込みました。");
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

        window = new MultiPlatformWindow(context);
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
