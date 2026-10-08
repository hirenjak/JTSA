using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;

namespace JTSA.Utility;

/// <summary>VTube Studio Public API への接続とリクエスト送信。</summary>
public sealed class VtsClient : IDisposable
{
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> pending = new();
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;

    public bool IsConnected => socket?.State == WebSocketState.Open;
    public bool IsAuthenticated { get; private set; }
    public string? VtsVersion { get; private set; }
    public string? LastError { get; private set; }

    public event Action? StateChanged;
    public event Action<string>? MessageLogged;

    public async Task ConnectAsync(string url, string? existingToken, Func<string, Task>? persistToken = null)
    {
        await DisconnectAsync();

        var uri = new Uri(string.IsNullOrWhiteSpace(url) ? VtsProtocol.DefaultWebSocketUrl : url.Trim());
        socket = new ClientWebSocket();
        receiveCts = new CancellationTokenSource();

        try
        {
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await socket.ConnectAsync(uri, connectTimeout.Token);
        }
        catch (Exception ex)
        {
            LastError = "VTube Studio に接続できません。URL と「Allow Plugin API access」を確認してください。";
            IsAuthenticated = false;
            await CleanupSocketAsync();
            StateChanged?.Invoke();
            throw new InvalidOperationException(LastError, ex);
        }

        StartReceiveLoop();
        Log($"接続しました: {uri}");

        try
        {
            var state = await SendAsync("APIStateRequest");
            VtsVersion = state["data"]?.Value<string>("vTubeStudioVersion");
            StateChanged?.Invoke();

            var token = existingToken;
            if (!string.IsNullOrWhiteSpace(token))
            {
                var auth = await AuthenticateAsync(token);
                if (VtsProtocol.IsAuthenticationSuccess(auth))
                {
                    IsAuthenticated = true;
                    LastError = null;
                    StateChanged?.Invoke();
                    return;
                }

                if (VtsProtocol.ShouldRequestNewToken(auth, hadToken: true))
                    token = null;
            }

            token = await RequestTokenAsync();
            if (persistToken is not null && !string.IsNullOrWhiteSpace(token))
                await persistToken(token);

            var retry = await AuthenticateAsync(token);
            if (!VtsProtocol.IsAuthenticationSuccess(retry))
            {
                LastError = retry["data"]?.Value<string>("reason")
                    ?? retry["data"]?.Value<string>("message")
                    ?? "認証に失敗しました。";
                IsAuthenticated = false;
                StateChanged?.Invoke();
                throw new InvalidOperationException(LastError);
            }

            IsAuthenticated = true;
            LastError = null;
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            LastError ??= ex.Message;
            await DisconnectAsync();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        IsAuthenticated = false;
        VtsVersion = null;
        await CleanupSocketAsync();
        StateChanged?.Invoke();
    }

    public void Disconnect() => DisconnectAsync().GetAwaiter().GetResult();

    public Task<JObject> SendAsync(string messageType, object? data = null)
        => SendEnvelopeAsync(VtsProtocol.CreateEnvelope(messageType, data));

    public Task<JObject> SendRawJsonAsync(string json)
        => SendEnvelopeAsync(VtsProtocol.CompleteRawJson(json));

    public Task<JObject> GetStatisticsAsync() => SendAsync("StatisticsRequest");

    public Task<JObject> GetCurrentModelAsync() => SendAsync("CurrentModelRequest");

    public Task<JObject> GetAvailableModelsAsync() => SendAsync("AvailableModelsRequest");

    public Task<JObject> LoadModelAsync(string modelId)
        => SendAsync("ModelLoadRequest", new { modelID = modelId });

    public Task<JObject> MoveModelAsync(double timeInSeconds, bool valuesAreRelativeToModel, double? x, double? y, double? rotation, double? size)
        => SendAsync("MoveModelRequest", new
        {
            timeInSeconds,
            valuesAreRelativeToModel,
            positionX = x,
            positionY = y,
            rotation,
            size
        });

    public Task<JObject> GetHotkeysAsync() => SendAsync("HotkeysInCurrentModelRequest");

    public Task<JObject> TriggerHotkeyAsync(string hotkeyId)
        => SendAsync("HotkeyTriggerRequest", new { hotkeyID = hotkeyId });

    public Task<JObject> GetExpressionsAsync()
        => SendAsync("ExpressionStateRequest", new { details = true });

    public Task<JObject> SetExpressionAsync(string file, bool active)
        => SendAsync("ExpressionActivationRequest", new { expressionFile = file, active });

    public Task<JObject> GetItemsAsync(bool includeAvailableSpots, bool includeItemInstancesInScene, bool includeAvailableItemFiles)
        => SendAsync("ItemListRequest", new
        {
            includeAvailableSpots,
            includeItemInstancesInScene,
            includeAvailableItemFiles
        });

    public Task<JObject> LoadItemAsync(string fileName)
        => SendAsync("ItemLoadRequest", new { fileName });

    public Task<JObject> UnloadItemAsync(string instanceId)
        => SendAsync("ItemUnloadRequest", new { instanceIDs = new[] { instanceId } });

    public Task<JObject> UnloadItemByFileNameAsync(string fileName)
        => SendAsync("ItemUnloadRequest", new { fileNames = new[] { fileName } });

    public Task<JObject> ControlItemAnimationAsync(string instanceId, int frameRate, int? frame = null, bool? play = null)
        => SendAsync("ItemAnimationControlRequest", new
        {
            itemInstanceID = instanceId,
            framerate = frameRate,
            frame,
            play
        });

    public Task<JObject> MoveItemAsync(string instanceId, double timeInSeconds, double? x, double? y, double? rotation, double? size)
        => SendAsync("ItemMoveRequest", new
        {
            itemsToMove = new[]
            {
                new
                {
                    itemInstanceID = instanceId,
                    timeInSeconds,
                    positionX = x,
                    positionY = y,
                    rotation,
                    size
                }
            }
        });

    public Task<JObject> GetArtMeshesAsync() => SendAsync("ArtMeshListRequest");

    public Task<JObject> TintArtMeshesAsync(int colorR, int colorG, int colorB, int colorA, IEnumerable<string>? namesExact = null, bool colorTintAll = false)
        => SendAsync("ColorTintRequest", new
        {
            colorTint = new { colorR, colorG, colorB, colorA },
            artMeshMatcher = new
            {
                tintAll = colorTintAll,
                nameExact = namesExact?.ToArray() ?? Array.Empty<string>()
            }
        });

    public Task<JObject> GetPostProcessingAsync() => SendAsync("PostProcessingListRequest");

    public Task<JObject> UpdatePostProcessingAsync(bool setPostProcessingPreset, bool postProcessingOn, IEnumerable<object>? config = null)
        => SendAsync("PostProcessingUpdateRequest", new
        {
            postProcessingOn,
            setPostProcessingPreset,
            postProcessingPreset = (string?)null,
            postProcessingFadeTime = 0.5,
            setPostProcessingValues = config is not null,
            postProcessingValues = config
        });

    public void Dispose()
    {
        receiveCts?.Cancel();
        try { socket?.Abort(); } catch { /* ignore */ }
        try { socket?.Dispose(); } catch { /* ignore */ }
        socket = null;
        sendLock.Dispose();
        receiveCts?.Dispose();
        receiveCts = null;
    }

    private async Task<string> RequestTokenAsync()
    {
        Log("プラグイン許可を VTube Studio に要求しています。");
        var response = await SendAsync("AuthenticationTokenRequest", new
        {
            pluginName = VtsProtocol.PluginName,
            pluginDeveloper = VtsProtocol.PluginDeveloper
        });

        ThrowIfApiError(response);
        var token = response["data"]?.Value<string>("authenticationToken");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("認証トークンを取得できませんでした。");
        return token;
    }

    private Task<JObject> AuthenticateAsync(string token)
        => SendEnvelopeAsync(VtsProtocol.CreateEnvelope("AuthenticationRequest", new
        {
            pluginName = VtsProtocol.PluginName,
            pluginDeveloper = VtsProtocol.PluginDeveloper,
            authenticationToken = token
        }), allowApiError: true);

    private async Task<JObject> SendEnvelopeAsync(JObject envelope, bool allowApiError = false)
    {
        if (!IsConnected)
            throw new InvalidOperationException("VTube Studio に接続していません。");

        var requestId = envelope.Value<string>("requestID") ?? VtsProtocol.NewRequestId();
        envelope["requestID"] = requestId;
        var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(requestId, tcs))
            throw new InvalidOperationException("同じ requestID の送信が重複しました。");

        var payload = envelope.ToString(Newtonsoft.Json.Formatting.None);
        var bytes = Encoding.UTF8.GetBytes(payload);

        await sendLock.WaitAsync();
        try
        {
            await socket!.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            Log($"→ {envelope.Value<string>("messageType")}");
        }
        catch
        {
            pending.TryRemove(requestId, out _);
            throw;
        }
        finally
        {
            sendLock.Release();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var registration = timeout.Token.Register(() => tcs.TrySetException(
            new TimeoutException("VTube Studio からの応答がタイムアウトしました。")));
        try
        {
            var response = await tcs.Task;
            if (!allowApiError)
                ThrowIfApiError(response);
            return response;
        }
        finally
        {
            pending.TryRemove(requestId, out _);
        }
    }

    private void StartReceiveLoop()
    {
        var cts = receiveCts!;
        receiveTask = Task.Run(() => ReceiveLoopAsync(cts.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket?.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                HandleIncoming(text);
            }
        }
        catch (OperationCanceledException)
        {
            // disconnect
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log($"受信エラー: {ex.Message}");
        }
        finally
        {
            FailPending(new InvalidOperationException("VTube Studio との接続が切れました。"));
            IsAuthenticated = false;
            StateChanged?.Invoke();
        }
    }

    private void HandleIncoming(string text)
    {
        JObject json;
        try
        {
            json = JObject.Parse(text);
        }
        catch
        {
            Log($"← 不正な JSON: {text}");
            return;
        }

        var type = json.Value<string>("messageType") ?? "?";
        var requestId = json.Value<string>("requestID");
        Log($"← {type}");

        if (!string.IsNullOrEmpty(requestId) && pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(json);
            return;
        }

        MessageLogged?.Invoke(json.ToString(Newtonsoft.Json.Formatting.Indented));
    }

    private static void ThrowIfApiError(JObject response)
    {
        if (!string.Equals(response.Value<string>("messageType"), "APIError", StringComparison.Ordinal))
            return;

        var message = response["data"]?.Value<string>("message") ?? "VTube Studio API エラー";
        throw new InvalidOperationException(message);
    }

    private void FailPending(Exception ex)
    {
        foreach (var pair in pending)
        {
            if (pending.TryRemove(pair.Key, out var tcs))
                tcs.TrySetException(ex);
        }
    }

    private async Task CleanupSocketAsync()
    {
        receiveCts?.Cancel();
        if (receiveTask is not null)
        {
            try { await receiveTask; } catch { /* ignore */ }
        }

        FailPending(new InvalidOperationException("切断しました。"));

        if (socket is not null)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None);
            }
            catch { /* ignore */ }
            socket.Dispose();
            socket = null;
        }

        receiveCts?.Dispose();
        receiveCts = null;
        receiveTask = null;
    }

    private void Log(string message) => MessageLogged?.Invoke($"{DateTime.Now:HH:mm:ss} {message}");
}
