using JTSA.Plugin.Abstractions;
using System.Windows;
using System.Windows.Threading;

namespace JTSA.TomozotchiPlugin;

public sealed class TomozotchiPlugin : IJtsaPlugin
{
    private const string OverlayId = "main";

    private IJtsaPluginContext? context;
    private DispatcherTimer? timer;
    private TomozotchiWindow? window;

    public string Id => "jtsa.tomozotchi";
    public string Name => "ともぞっち";
    public string Description => "時間で減るゲージを配信拡張に表示し、チャネポ交換で増減させます。";
    public Version Version => new(1, 0, 0);

    public TomozotchiSettings Settings { get; private set; } = new();
    public TomozotchiGame Game { get; private set; } = null!;

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        Settings = TomozotchiStore.Load(pluginContext.DataDirectory);
        Game = new TomozotchiGame(Settings.Game);
        Game.Changed += UpdateOverlay;
        Game.ConfigChanged += SaveSettings;
        pluginContext.ChannelPointRedeemed += OnChannelPointRedeemed;

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Game.Tick();
        timer.Start();
        UpdateOverlay();
        pluginContext.Log("ともぞっちを読み込みました。");
    }

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new TomozotchiWindow(this, context!);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        timer?.Stop();
        timer = null;
        if (context is not null)
        {
            context.ChannelPointRedeemed -= OnChannelPointRedeemed;
            context.RemoveExpansionOverlay(OverlayId);
        }
        window?.Close();
        window = null;
    }

    public void SaveSettings()
    {
        if (context is null) return;
        try
        {
            TomozotchiStore.Save(context.DataDirectory, Settings);
        }
        catch (Exception ex)
        {
            context.LogError("ともぞっちの設定を保存できませんでした。", ex);
        }
    }

    public void UpdateOverlay()
    {
        context?.SetExpansionOverlay(new ExpansionOverlayContent(
            OverlayId,
            TomozotchiOverlay.Render(Game),
            Settings.OverlayX,
            Settings.OverlayY,
            Settings.OverlayWidth,
            Settings.OverlayHeight));
    }

    // イベントは UI スレッド以外から来る可能性があるため、ゲーム操作は UI スレッドへ集める
    private void OnChannelPointRedeemed(ChannelPointRedemptionInfo info) =>
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (timer is null) return;
            var result = Game.FireReward(info.RewardId);
            if (result is null) return;
            var message = $"チャネポ by {info.UserName}: {result.Message}";
            if (result.Success) context?.Log(message);
            else context?.LogError(message);
        });
}
