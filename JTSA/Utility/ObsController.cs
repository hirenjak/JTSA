using OBSWebsocketDotNet;
using Newtonsoft.Json.Linq;

namespace JTSA.Utility;

/// <summary>OBS WebSocketへの接続と配信操作をまとめる。</summary>
public sealed class ObsController : IDisposable
{
    private readonly OBSWebsocket client = new();
    private readonly SemaphoreSlim connectionLock = new(1, 1);

    public event Action<bool>? StreamingStateChanged;

    public ObsController()
    {
        client.StreamStateChanged += (_, e) =>
            StreamingStateChanged?.Invoke(e.OutputState.IsActive);
    }

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

    public IReadOnlyList<string> GetSceneNames()
    {
        EnsureConnected();
        return client.GetSceneList().Scenes
            .Select(scene => scene.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string GetCurrentProgramScene()
    {
        EnsureConnected();
        return client.GetCurrentProgramScene();
    }

    public void SetCurrentProgramScene(string sceneName)
    {
        EnsureConnected();
        client.SetCurrentProgramScene(sceneName);
    }

    public IReadOnlyList<string> GetTextSourceNames(string sceneName)
    {
        EnsureConnected();
        return client.GetSceneItemList(sceneName)
            .Where(item => item.SourceKind?.StartsWith("text_gdiplus", StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => item.SourceName)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ObsSceneSource> GetSceneSources(string sceneName)
    {
        EnsureConnected();
        return client.GetSceneItemList(sceneName)
            .Select(item => new ObsSceneSource(
                item.SourceName,
                client.GetSceneItemEnabled(sceneName, item.ItemId)))
            .OrderBy(item => item.SourceName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public bool GetSceneSourceEnabled(string sceneName, string sourceName)
    {
        EnsureConnected();
        var item = client.GetSceneItemList(sceneName).FirstOrDefault(item =>
            string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            throw new InvalidOperationException($"シーン「{sceneName}」にソース「{sourceName}」がありません。");
        return client.GetSceneItemEnabled(sceneName, item.ItemId);
    }

    public void SetSceneSourceEnabled(string sceneName, string sourceName, bool enabled)
    {
        EnsureConnected();
        var item = client.GetSceneItemList(sceneName).FirstOrDefault(item =>
            string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            throw new InvalidOperationException($"シーン「{sceneName}」にソース「{sourceName}」がありません。");
        client.SetSceneItemEnabled(sceneName, item.ItemId, enabled);
    }

    public string GetTextSourceText(string inputName)
    {
        EnsureConnected();
        return client.GetInputSettings(inputName).Settings.Value<string>("text") ?? string.Empty;
    }

    public void SetTextSourceText(string inputName, string text)
    {
        EnsureConnected();
        client.SetInputSettings(inputName, new JObject { ["text"] = text }, true);
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

public sealed record ObsSceneSource(string SourceName, bool IsEnabled);
