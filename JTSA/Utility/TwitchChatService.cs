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

    public event Action<ChatMessage>? MessageReceived;
    public event Action? SubscriptionReceived;
    public event Action? HealthCheck;

    public TwitchChatService(string channelName)
    {
        this.channelName = channelName;

        // 閲覧だけなら匿名接続でOK
        var credentials = new ConnectionCredentials();

        client = new TwitchClient();
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

        #endregion
    }


    /// <summary>
    /// チャット接続時
    /// </summary>
    private Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Console.WriteLine($"チャンネル参加: {e.Channel}");
        return Task.CompletedTask;
    }


    /// <summary>
    /// チャット切断時
    /// </summary>
    private Task Client_OnDisconnected(object? sender, OnDisconnectedArgs e)
    {
        Console.WriteLine("Twitchチャットから切断されました。");

        if (!disconnectRequested)
            _ = ReconnectWithRetryAsync();

        return Task.CompletedTask;
    }


    /// <summary>
    /// 接続処理
    /// </summary>
    public async Task ConnectAsync()
    {
        if (client.IsConnected) return;

        disconnectRequested = false;
        await client.ConnectAsync();

        healthCheckCancellation?.Cancel();
        healthCheckCancellation?.Dispose();
        healthCheckCancellation = new CancellationTokenSource();
        _ = MonitorConnectionAsync(healthCheckCancellation.Token);
    }


    /// <summary>
    /// 切断処理
    /// </summary>
    /// <returns></returns>
    public async Task DisconnectAsync()
    {
        disconnectRequested = true;
        healthCheckCancellation?.Cancel();

        if (!client.IsConnected)
            return;

        await client.DisconnectAsync();
    }

    private async Task MonitorConnectionAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!client.IsConnected && !disconnectRequested)
                {
                    Console.WriteLine("Twitchチャットの接続停止を検知しました。");
                    _ = ReconnectWithRetryAsync();
                }

                HealthCheck?.Invoke();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 意図的な切断時は監視も終了する。
        }
    }

    private async Task ReconnectWithRetryAsync()
    {
        if (!await reconnectLock.WaitAsync(0)) return;

        try
        {
            var delay = TimeSpan.FromSeconds(2);

            while (!disconnectRequested && !client.IsConnected)
            {
                try
                {
                    Console.WriteLine("Twitchチャットへ再接続します。");
                    await client.ReconnectAsync();

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

                await Task.Delay(delay);
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
        await client.JoinChannelAsync(channelName);
    }


    private Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage;

        if (message.Bits > 0)
            JTSA.Utility.StreamSupportTracker.AddBits(message.DisplayName, message.Bits);

        MessageReceived?.Invoke(message);

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
        return Task.CompletedTask;
    }
}
