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

    public IReadOnlyList<ObsCaptureSource> GetCaptureSources()
    {
        EnsureConnected();
        return client.GetInputList(null)
            .Where(input => TryGetCaptureProperty(input.InputKind, out _, out _))
            .Select(input =>
            {
                TryGetCaptureProperty(input.InputKind, out var propertyName, out var typeName);
                return new ObsCaptureSource(input.InputName, input.InputKind, propertyName, typeName);
            })
            .OrderBy(input => input.InputName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public ObsCaptureSettings GetCaptureSettings(ObsCaptureSource source)
    {
        EnsureConnected();
        var settings = client.GetInputSettings(source.InputName).Settings;
        var currentValue = settings.Value<string>(source.PropertyName) ?? string.Empty;
        // obs-websocket-dotnet 5.0.1 のラッパーは propertyItems (JArray) を
        // JToken.Value<T>() で変換しようとして例外になるため、生レスポンスを読む。
        var response = client.SendRequest("GetInputPropertiesListPropertyItems", new JObject
        {
            ["inputName"] = source.InputName,
            ["propertyName"] = source.PropertyName
        });
        var items = response["propertyItems"] as JArray ?? [];
        var destinations = items
            .OfType<JObject>()
            .Where(item => item.Value<bool?>("itemEnabled") != false)
            .Select(item =>
            {
                var value = item["itemValue"]?.ToString() ?? string.Empty;
                return new ObsCaptureDestination(item["itemName"]?.ToString() ?? value, value);
            })
            .Where(item => !string.IsNullOrEmpty(item.Value))
            .ToList();
        return new ObsCaptureSettings(currentValue, destinations);
    }

    public void SetCaptureDestination(ObsCaptureSource source, string value)
    {
        EnsureConnected();
        client.SetInputSettings(source.InputName, new JObject { [source.PropertyName] = value }, true);
    }

    public void SetInputVisibleAcrossScenes(string inputName, bool visible)
    {
        EnsureConnected();
        foreach (var sceneName in GetSceneNames())
        {
            foreach (var item in client.GetSceneItemList(sceneName).Where(item =>
                         string.Equals(item.SourceName, inputName, StringComparison.OrdinalIgnoreCase)))
                client.SetSceneItemEnabled(sceneName, item.ItemId, visible);
        }

        foreach (var groupName in client.GetGroupList())
        {
            foreach (var item in client.GetGroupSceneItemList(groupName).Where(item =>
                         string.Equals(item.Value<string>("sourceName"), inputName,
                             StringComparison.OrdinalIgnoreCase)))
                client.SetSceneItemEnabled(groupName, item.Value<int>("sceneItemId"), visible);
        }
    }

    private static bool TryGetCaptureProperty(string inputKind, out string propertyName, out string typeName)
    {
        var kind = inputKind.Split('_').TakeWhile(part => !part.StartsWith("v", StringComparison.OrdinalIgnoreCase)).ToArray();
        var unversionedKind = string.Join('_', kind);
        (propertyName, typeName) = unversionedKind switch
        {
            "window_capture" => ("window", "ウィンドウキャプチャ"),
            "game_capture" => ("window", "ゲームキャプチャ"),
            "monitor_capture" => ("monitor", "画面キャプチャ"),
            "dshow_input" => ("video_device_id", "映像キャプチャデバイス"),
            _ => (string.Empty, string.Empty)
        };
        return propertyName.Length > 0;
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
public sealed record ObsCaptureSource(string InputName, string InputKind, string PropertyName, string TypeName);
public sealed record ObsCaptureDestination(string Name, string Value);
public sealed record ObsCaptureSettings(string CurrentValue, IReadOnlyList<ObsCaptureDestination> Destinations);
