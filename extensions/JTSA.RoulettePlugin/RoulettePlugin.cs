using JTSA.Plugin.Abstractions;
using System.Windows;

namespace JTSA.RoulettePlugin;

public sealed class RoulettePlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private RouletteWindow? window;

    public string Id => "jtsa.roulette";
    public string Name => "ルーレット";
    public string Description => "入力した候補からランダムに1件を抽選し、配信拡張にも表示します。";
    public Version Version => new(1, 0, 0);

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        context.Log("ルーレットを読み込みました。");
    }

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new RouletteWindow(context!);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        window?.Close();
        context?.RemoveExpansionOverlay("roulette");
        window = null;
    }
}
