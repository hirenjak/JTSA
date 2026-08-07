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

    private Task Client_OnDisconnected(object? sender, OnDisconnectedArgs e)
    {
        Console.WriteLine("Twitchチャットから切断されました。");
        return Task.CompletedTask;
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

    private async Task Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedEventArgs e)
    {
        await client.JoinChannelAsync(channelName);
    }

    private Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Console.WriteLine($"チャンネル参加: {e.Channel}");
        return Task.CompletedTask;
    }

    private Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage;

        MessageReceived?.Invoke(message);

        return Task.CompletedTask;
    }

    private Task Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        Console.WriteLine($"Twitch接続エラー: {e.Error.Message}");
        return Task.CompletedTask;
    }
}