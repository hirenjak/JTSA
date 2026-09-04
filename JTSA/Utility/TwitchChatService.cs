using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;

public sealed class TwitchChatService
{
    private readonly TwitchClient client;
    private readonly string channelName;
    private readonly SemaphoreSlim reconnectLock = new(1, 1);
    private CancellationTokenSource? healthCheckCancellation;
    private volatile bool disconnectRequested;
    private long lastReceivedAt = Environment.TickCount64;
    private volatile bool channelJoined;

    public event Action<string>? StatusChanged;

    public event Action<ChatMessage>? MessageReceived;
    public event Action? SubscriptionReceived;
    public event Action? HealthCheck;

    public TwitchChatService(string channelName) : this(channelName, new TwitchClient()) { }

    internal TwitchChatService(string channelName, TwitchClient client)
    {
        this.channelName = channelName;

        // 閲覧だけなら匿名接続でOK
        var credentials = new ConnectionCredentials();

        this.client = client;
        client.Initialize(credentials);

        #region イベントハンドラ

        client.OnConnected += Client_OnConnected;
        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnMessageReceived += Client_OnMessageReceived;
        client.OnNewSubscriber += Client_OnNewSubscriber;
        client.OnReSubscriber += Client_OnReSubscriber;
        client.OnGiftedSubscription += Client_OnGiftedSubscription;
        client.OnDisconnected += Client_OnDisconnected;
        client.OnConnectionError += Client_OnConnectionError;
        client.OnSendReceiveData += (_, e) =>
        {
            if (e.Direction == TwitchLib.Client.Enums.SendReceiveDirection.Received)
                Interlocked.Exchange(ref lastReceivedAt, Environment.TickCount64);
            return Task.CompletedTask;
        };

        #endregion
    }


    /// <summary>
    /// チャット接続時
    /// </summary>
    private Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Console.WriteLine($"チャンネル参加: {e.Channel}");
        channelJoined = true;
        StatusChanged?.Invoke("チャットの受信を開始しました。");
        return Task.CompletedTask;
    }


    /// <summary>
    /// チャット切断時
    /// </summary>
    private Task Client_OnDisconnected(object? sender, OnDisconnectedArgs e)
    {
        Console.WriteLine("Twitchチャットから切断されました。");
        channelJoined = false;

        if (!disconnectRequested)
            _ = Task.Run(() => ReconnectWithRetryAsync());

        return Task.CompletedTask;
    }


    /// <summary>
    /// 接続処理
    /// </summary>
    public async Task ConnectAsync()
    {
        if (client.IsConnected) return;

        disconnectRequested = false;
        healthCheckCancellation?.Cancel();
        healthCheckCancellation?.Dispose();
        healthCheckCancellation = new CancellationTokenSource();
        _ = MonitorConnectionAsync(healthCheckCancellation.Token);
        await client.ConnectAsync();
    }


    /// <summary>
    /// 切断処理
    /// </summary>
    /// <returns></returns>
    public async Task DisconnectAsync()
    {
        disconnectRequested = true;
        healthCheckCancellation?.Cancel();

        await reconnectLock.WaitAsync();
        try
        {
            await client.DisconnectAsync();
        }
        finally { reconnectLock.Release(); }
    }

    private async Task MonitorConnectionAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    var stalled = Environment.TickCount64 - Interlocked.Read(ref lastReceivedAt)
                        >= TimeSpan.FromMinutes(3).TotalMilliseconds;
                    if (!disconnectRequested && (!client.IsConnected || stalled || !channelJoined))
                    {
                        StatusChanged?.Invoke("チャットの受信停止を検知しました。再接続します。");
                        await ReconnectWithRetryAsync(stalled || !channelJoined);
                    }
                    else if (client.IsConnected)
                    {
                        // チャットがない配信でも、サーバーの応答で接続を確認する。
                        await client.SendRawAsync("PING :jtsa-health");
                    }
                    HealthCheck?.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"チャット接続監視エラー: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 意図的な切断時は監視も終了する。
        }
    }

    private async Task ReconnectWithRetryAsync(bool force = false)
    {
        if (!await reconnectLock.WaitAsync(0)) return;

        try
        {
            var delay = TimeSpan.FromSeconds(2);

            while (!disconnectRequested && (force || !client.IsConnected))
            {
                try
                {
                    Console.WriteLine("Twitchチャットへ再接続します。");
                    // 古い参加状態をクリアして、新しい接続で確実にJOINする。
                    channelJoined = false;
                    await client.DisconnectAsync();
                    if (disconnectRequested) return;
                    Interlocked.Exchange(ref lastReceivedAt, Environment.TickCount64);
                    await client.ConnectAsync();
                    force = false;

                    if (client.IsConnected)
                    {
                        Console.WriteLine("Twitchチャットへ再接続しました。");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Twitchチャット再接続エラー: {ex.Message}");
                }

                try { await Task.Delay(delay, healthCheckCancellation?.Token ?? CancellationToken.None); }
                catch (OperationCanceledException) { return; }
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }
        finally
        {
            reconnectLock.Release();
        }
    }

    private async Task Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedEventArgs e)
    {
        if (disconnectRequested) return;
        Interlocked.Exchange(ref lastReceivedAt, Environment.TickCount64);
        await client.JoinChannelAsync(channelName, overrideCheck: true);
    }


    private Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage;

        try { MessageReceived?.Invoke(message); }
        catch (Exception ex)
        {
            Console.WriteLine($"チャット処理エラー: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
    {
        JTSA.Utility.StreamSupportTracker.AddSubscription(
            GetString(e.Subscriber, "DisplayName", "Login"),
            Math.Max(1, GetInt(e.Subscriber, 1, "MsgParamCumulativeMonths")),
            GetString(e.Subscriber, "MsgParamSubPlan"));
        SubscriptionReceived?.Invoke();
        return Task.CompletedTask;
    }

    private Task Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
    {
        JTSA.Utility.StreamSupportTracker.AddSubscription(
            GetString(e.ReSubscriber, "DisplayName", "Login"),
            Math.Max(1, GetInt(e.ReSubscriber, 1, "MsgParamCumulativeMonths")),
            GetString(e.ReSubscriber, "MsgParamSubPlan"));
        SubscriptionReceived?.Invoke();
        return Task.CompletedTask;
    }

    private Task Client_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
    {
        // GiftedSubscription の DisplayName は受取側になる場合があるため、
        // IRC の送信者を表す Login を優先してギフトした側を集計する。
        JTSA.Utility.StreamSupportTracker.AddGiftSubscription(
            GetString(e.GiftedSubscription, "Login", "DisplayName"),
            GetString(e.GiftedSubscription, "MsgParamSubPlan"));
        SubscriptionReceived?.Invoke();
        return Task.CompletedTask;
    }

    private static string GetString(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = source.GetType().GetProperty(propertyName)?.GetValue(source)?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return "不明なユーザー";
    }

    private static int GetInt(object source, int fallback, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
            if (value is not null && int.TryParse(value.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    private Task Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        Console.WriteLine($"Twitch接続エラー: {e.Error.Message}");
        if (!disconnectRequested)
            _ = Task.Run(() => ReconnectWithRetryAsync());
        return Task.CompletedTask;
    }
}
