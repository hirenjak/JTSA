using JTSA.Plugin.Abstractions;
using System.Windows;

namespace JTSA.NizimaLivePlugin;

public sealed class NizimaLivePlugin : IJtsaPlugin
{
    private IJtsaPluginContext? context;
    private NizimaLiveSettings settings = new();
    private NizimaLiveWindow? window;
    private CancellationTokenSource? autoConnectCts;
    private Action<ExpansionTriggerInfo>? triggerHandler;

    public string Id => "jtsa.nizimalive";
    public string Name => "nizima LIVE";
    public string Description => "nizima LIVE と連携し、配信拡張イベントからモデル操作を送ります。";
    public Version Version => new(1, 0, 0);

    public NizimaClient Client { get; } = new();

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        settings = NizimaSettingsStore.Load(pluginContext.DataDirectory);
        triggerHandler = info => _ = HandleTriggerAsync(info);
        pluginContext.ExpansionTriggered += triggerHandler;
        Client.StatusChanged += () => window?.Dispatcher.BeginInvoke(window.RefreshConnectionUi);
        if (settings.AutoConnect)
            StartAutoConnect();
        pluginContext.Log("nizima LIVE 連携を読み込みました。");
    }

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new NizimaLiveWindow(this, context!, settings);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        if (context is not null && triggerHandler is not null)
            context.ExpansionTriggered -= triggerHandler;
        autoConnectCts?.Cancel();
        window?.Close();
        window = null;
        Client.Dispose();
    }

    public void SaveSettings()
    {
        if (context is null)
            return;
        NizimaSettingsStore.Save(context.DataDirectory, settings);
    }

    public void StartAutoConnect()
    {
        settings.AutoConnect = true;
        autoConnectCts?.Cancel();
        autoConnectCts = new CancellationTokenSource();
        _ = RunAutoConnectLoopAsync(autoConnectCts.Token);
    }

    public void StopAutoConnect()
    {
        settings.AutoConnect = false;
        autoConnectCts?.Cancel();
    }

    public async Task ConnectNowAsync(CancellationToken cancellationToken)
    {
        await Client.ConnectAsync(
            settings.WebSocketUrl,
            settings.AuthToken,
            token =>
            {
                settings.AuthToken = token;
                SaveSettings();
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAutoConnectLoopAsync(CancellationToken cancellationToken)
    {
        var isFirstAttempt = true;
        var retryDelay = NizimaAutoConnectRetry.FirstRetryDelay;
        while (!cancellationToken.IsCancellationRequested && settings.AutoConnect)
        {
            var wait = isFirstAttempt
                ? NizimaAutoConnectRetry.InitialDelay
                : retryDelay;
            try
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Client.IsConnected)
                return;

            try
            {
                await ConnectNowAsync(cancellationToken).ConfigureAwait(false);
                if (Client.IsConnected)
                    return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                context?.LogError("nizima LIVE 自動接続に失敗しました。", ex);
            }

            if (!isFirstAttempt)
                retryDelay = NizimaAutoConnectRetry.NextDelay(retryDelay);
            isFirstAttempt = false;
        }
    }

    private Task HandleTriggerAsync(ExpansionTriggerInfo info) =>
        NizimaTriggerExecutor.HandleTriggerAsync(
            Client,
            settings.Rules,
            info,
            message => context?.Log(message),
            (message, exception) => context?.LogError(message, exception));
}
