using OBSWebsocketDotNet;

namespace JTSA.Utility;

/// <summary>OBS WebSocketへの接続と配信操作をまとめる。</summary>
public sealed class ObsController : IDisposable
{
    private readonly OBSWebsocket client = new();
    private readonly SemaphoreSlim connectionLock = new(1, 1);

    public bool IsConnected => client.IsConnected;

    public async Task ConnectAsync(string url, string password)
    {
        if (client.IsConnected)
            return;

        await connectionLock.WaitAsync();
        try
        {
            if (!client.IsConnected)
            {
                // このライブラリのConnectAsyncはvoidで、接続処理の開始直後に戻る。
                // IsConnectedになるまで待たないと、直後の疎通確認が未接続として失敗する。
                client.ConnectAsync(url, password);

                var timeoutAt = DateTime.UtcNow.AddSeconds(10);
                while (!client.IsConnected && DateTime.UtcNow < timeoutAt)
                    await Task.Delay(50);

                if (!client.IsConnected)
                    throw new TimeoutException(
                        "OBSへの接続がタイムアウトしました。URL、パスワード、OBS側のWebSocket設定を確認してください。");
            }
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public void Disconnect()
    {
        if (client.IsConnected)
            client.Disconnect();
    }

    public async Task DisconnectAsync()
    {
        Disconnect();

        // obs-websocket-dotnetの切断通知は非同期なので、直後の再接続と競合させない。
        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (client.IsConnected && DateTime.UtcNow < timeoutAt)
            await Task.Delay(50);
    }

    public bool IsStreaming()
    {
        EnsureConnected();
        return client.GetStreamStatus().IsActive;
    }

    public void StartStreaming()
    {
        EnsureConnected();
        client.StartStream();
    }

    public void StopStreaming()
    {
        EnsureConnected();
        client.StopStream();
    }

    private void EnsureConnected()
    {
        if (!client.IsConnected)
            throw new InvalidOperationException("OBSに接続されていません。");
    }

    public void Dispose()
    {
        Disconnect();
        connectionLock.Dispose();
    }
}
