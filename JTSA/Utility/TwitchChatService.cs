using System;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;

public sealed class TwitchChatService
{
    private readonly TwitchClient client;
    private readonly string channelName;

    public event Action<ChatMessage>? MessageReceived;
    public event Action? SubscriptionReceived;
    public event Action<string>? RaidReceived;

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
        client.OnRaidNotification += Client_OnRaidNotification;
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
        return Task.CompletedTask;
    }


    /// <summary>
    /// 接続処理
    /// </summary>
    public async Task ConnectAsync()
    {
        if (client.IsConnected) return;

        await client.ConnectAsync();
    }


    /// <summary>
    /// 切断処理
    /// </summary>
    /// <returns></returns>
    public async Task DisconnectAsync()
    {
        if (!client.IsConnected)
            return;

        await client.DisconnectAsync();
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
        JTSA.Utility.StreamSupportTracker.AddSubscription(GetString(e.Subscriber, "DisplayName", "Login"));
        SubscriptionReceived?.Invoke();
        return Task.CompletedTask;
    }

    private Task Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
    {
        JTSA.Utility.StreamSupportTracker.AddSubscription(GetString(e.ReSubscriber, "DisplayName", "Login"));
        SubscriptionReceived?.Invoke();
        return Task.CompletedTask;
    }

    private Task Client_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
    {
        JTSA.Utility.StreamSupportTracker.AddSubscription(GetString(e.GiftedSubscription, "DisplayName", "Login"));
        SubscriptionReceived?.Invoke();
        return Task.CompletedTask;
    }

    private Task Client_OnRaidNotification(object? sender, OnRaidNotificationArgs e)
    {
        var raid = e.RaidNotification;
        var userName = GetString(raid, "DisplayName", "Login");
        JTSA.Utility.StreamSupportTracker.AddRaid(
            userName,
            GetInt(raid, "MsgParamViewerCount", "ViewerCount"));
        RaidReceived?.Invoke(userName);
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

    private static int GetInt(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
            if (value != null && int.TryParse(value.ToString(), out var result)) return result;
        }
        return 0;
    }

    private Task Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        Console.WriteLine($"Twitch接続エラー: {e.Error.Message}");
        return Task.CompletedTask;
    }
}
