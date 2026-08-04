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

    public event Action<TwitchChatData>? MessageReceived;

    public TwitchChatService(string channelName)
    {
        this.channelName = channelName;

        // 閲覧だけなら匿名接続でOK
        var credentials = new ConnectionCredentials();

        client = new TwitchClient();
        client.Initialize(credentials);

        client.OnConnected += Client_OnConnected;
        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnMessageReceived += Client_OnMessageReceived;
        client.OnDisconnected += Client_OnDisconnected;
        client.OnConnectionError += Client_OnConnectionError;
    }

    private async Task Client_OnDisconnected(object? sender, OnDisconnectedArgs e)
    {
        throw new NotImplementedException();
    }

    public async Task ConnectAsync()
    {
        if (client.IsConnected)
            return;

        await client.ConnectAsync();
    }

    public async Task DisconnectAsync()
    {
        if (!client.IsConnected)
            return;

        await client.DisconnectAsync();
    }

    private async Task Client_OnConnected(
        object? sender,
        TwitchLib.Client.Events.OnConnectedEventArgs e)
    {
        await client.JoinChannelAsync(channelName);
    }

    private Task Client_OnJoinedChannel(
        object? sender,
        OnJoinedChannelArgs e)
    {
        Console.WriteLine($"チャンネル参加: {e.Channel}");
        return Task.CompletedTask;
    }

    private Task Client_OnMessageReceived(
        object? sender,
        OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage;

        var data = new TwitchChatData
        {
            Channel = message.Channel,
            UserId = message.UserId,
            UserName = message.Username,
            DisplayName = message.DisplayName,
            Message = message.Message,
            ColorHex = message.HexColor,
            MessageId = message.Id,

            IsModerator = message.UserDetail.IsModerator,
            IsSubscriber = message.UserDetail.IsSubscriber,
            IsVip = message.UserDetail.IsVip,
        };

        MessageReceived?.Invoke(data);

        return Task.CompletedTask;
    }

    private Task Client_OnDisconnected(object? sender, OnDisconnectedEventArgs e)
    {
        Console.WriteLine("Twitchチャットから切断されました。");
        return Task.CompletedTask;
    }

    private Task Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        Console.WriteLine($"Twitch接続エラー: {e.Error.Message}");
        return Task.CompletedTask;
    }
}

public sealed class TwitchChatData
{
    public string Channel { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Message { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public string MessageId { get; set; } = "";

    public bool IsModerator { get; set; }
    public bool IsSubscriber { get; set; }
    public bool IsVip { get; set; }
}