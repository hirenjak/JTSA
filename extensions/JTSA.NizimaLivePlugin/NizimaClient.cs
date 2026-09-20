using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JTSA.NizimaLivePlugin;

public sealed class NizimaClient : IDisposable
{
    private static readonly HashSet<string> UnrestrictedMethods =
    [
        "RegisterPlugin",
        "EstablishConnection",
        "GetConnectionStatus"
    ];

    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly List<PendingRequest> pending = [];
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;
    private bool disposed;

    public bool IsConnected => socket?.State == WebSocketState.Open;
    public bool IsEnabled { get; private set; }
    public bool CanSendMethods => IsConnected && IsEnabled;
    public string StatusText { get; private set; } = "未接続";

    public event Action? StatusChanged;
    public event Action? CatalogChanged;

    public async Task ConnectAsync(
        string url,
        string? existingToken,
        Func<string, Task> persistToken,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await DisconnectAsync().ConfigureAwait(false);

        var ws = new ClientWebSocket();
        socket = ws;
        StatusText = "接続中…";
        RaiseStatus();

        await ws.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        receiveTask = ReceiveLoopAsync(receiveCts.Token);

        try
        {
            if (!string.IsNullOrWhiteSpace(existingToken))
            {
                try
                {
                    var established = await SendRequestAsync(
                        "EstablishConnection",
                        new JsonObject
                        {
                            ["Name"] = NizimaProtocol.PluginName,
                            ["Token"] = existingToken
                        },
                        cancellationToken).ConfigureAwait(false);
                    ApplyEnabled(ReadEnabled(established));
                    StatusText = IsEnabled ? "接続済み（有効）" : "接続済み（nizima LIVE で有効化待ち）";
                    RaiseStatus();
                    return;
                }
                catch (NizimaApiException ex) when (NizimaProtocol.ShouldReregister(ex.ErrorType))
                {
                    // トークン無効。再登録する。
                }
            }

            var registered = await SendRequestAsync(
                "RegisterPlugin",
                new JsonObject
                {
                    ["Name"] = NizimaProtocol.PluginName,
                    ["Developer"] = NizimaProtocol.PluginDeveloper,
                    ["Version"] = "1.0.0"
                },
                cancellationToken).ConfigureAwait(false);
            var token = registered.TryGetProperty("Token", out var tokenNode)
                ? tokenNode.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(token))
                await persistToken(token).ConfigureAwait(false);

            ApplyEnabled(false);
            StatusText = "登録済み（nizima LIVE で有効化してください）";
            RaiseStatus();
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        receiveCts?.Cancel();
        FailPending(new OperationCanceledException("切断しました。"));
        if (socket is { } ws)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None)
                        .ConfigureAwait(false);
            }
            catch
            {
                // 切断時の失敗は無視する。
            }

            ws.Dispose();
        }

        socket = null;
        if (receiveTask is not null)
        {
            try { await receiveTask.ConfigureAwait(false); }
            catch { }
        }

        receiveCts?.Dispose();
        receiveCts = null;
        receiveTask = null;
        ApplyEnabled(false);
        StatusText = "未接続";
        RaiseStatus();
    }

    public async Task<JsonElement> SendRequestAsync(
        string method,
        JsonNode? data,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (socket is not { State: WebSocketState.Open })
            throw new InvalidOperationException("nizima LIVE に接続していません。");
        if (!UnrestrictedMethods.Contains(method) && !IsEnabled)
            throw new InvalidOperationException("nizima LIVE 側でプラグインを有効化してください。");

        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingRequest = new PendingRequest(method, tcs);
        lock (pending)
            pending.Add(pendingRequest);

        var json = NizimaProtocol.CreateRequest(method, data).ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(json);
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            CompletePending(pendingRequest, ex: new InvalidOperationException("送信に失敗しました。"));
            throw;
        }
        finally
        {
            sendLock.Release();
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var cancelReg = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));
        return await tcs.Task.ConfigureAwait(false);
    }

    public Task<JsonElement> SendRawJsonAsync(string json, CancellationToken cancellationToken)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            return Task.FromException<JsonElement>(new InvalidOperationException("JSONが不正です。", ex));
        }

        if (node is not JsonObject obj)
            return Task.FromException<JsonElement>(new InvalidOperationException("JSONオブジェクトを指定してください。"));

        var method = obj["Method"]?.GetValue<string>() ?? "Raw";
        return SendRequestAsync(method, obj["Data"], cancellationToken);
    }

    public async Task<string> ResolveModelIdAsync(string? preferred, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred;
        var response = await SendRequestAsync("GetCurrentModelId", null, cancellationToken)
            .ConfigureAwait(false);
        var id = response.TryGetProperty("ModelId", out var node) ? node.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("対象モデルがありません。ModelId を指定してください。");
        return id;
    }

    public async Task ChangeModelAsync(string modelId, string modelPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(modelPath))
            throw new InvalidOperationException("ChangeModel には ModelId と ModelPath が必要です。");
        await SendRequestAsync(
            "ChangeModel",
            new JsonObject { ["ModelId"] = modelId, ["ModelPath"] = modelPath },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<NizimaNamedOption>> GetExpressionsAsync(
        string? modelId,
        CancellationToken cancellationToken) =>
        GetNamedOptionsAsync("GetExpressions", "Expressions", "Name", "ExpressionPath", modelId, cancellationToken);

    public Task<IReadOnlyList<NizimaNamedOption>> GetMotionsAsync(
        string? modelId,
        CancellationToken cancellationToken) =>
        GetNamedOptionsAsync("GetMotions", "Motions", "Name", "MotionPath", modelId, cancellationToken);

    public async Task<NizimaRuleCatalogs> GetRuleCatalogsAsync(string? preferredModelId, CancellationToken cancellationToken)
    {
        var expressions = await GetExpressionsAsync(preferredModelId, cancellationToken).ConfigureAwait(false);
        var motions = await GetMotionsAsync(preferredModelId, cancellationToken).ConfigureAwait(false);
        var models = await SendRequestAsync("GetModels", new JsonObject(), cancellationToken).ConfigureAwait(false);
        var registeredModels = await SendRequestAsync("GetRegisteredModels", new JsonObject(), cancellationToken)
            .ConfigureAwait(false);
        var scenes = await SendRequestAsync("GetScenes", new JsonObject(), cancellationToken).ConfigureAwait(false);
        var registeredItems = await SendRequestAsync("GetRegisteredItems", new JsonObject(), cancellationToken)
            .ConfigureAwait(false);
        var items = await SendRequestAsync("GetItems", new JsonObject(), cancellationToken).ConfigureAwait(false);
        var effects = await SendRequestAsync("GetEffectGroups", new JsonObject(), cancellationToken).ConfigureAwait(false);

        return new NizimaRuleCatalogs
        {
            Expressions = expressions,
            Motions = motions,
            ModelsOnScreen = NizimaNamedOption.FromArray(models, "Models", "Name", "ModelId"),
            RegisteredModels = NizimaNamedOption.FromArray(registeredModels, "RegisteredModels", "Name", "ModelPath"),
            Scenes = NizimaNamedOption.FromScenes(scenes),
            RegisteredItems = NizimaNamedOption.FromArray(registeredItems, "RegisteredItems", "Name", "ItemPath"),
            ItemsOnScreen = NizimaNamedOption.FromArray(items, "Items", "Name", "ItemId"),
            EffectGroups = NizimaNamedOption.FromArray(effects, "EffectGroups", "Name", "GroupId")
        };
    }

    private async Task<IReadOnlyList<NizimaNamedOption>> GetNamedOptionsAsync(
        string method,
        string arrayName,
        string nameProperty,
        string pathProperty,
        string? preferredModelId,
        CancellationToken cancellationToken)
    {
        var modelId = await ResolveModelIdAsync(preferredModelId, cancellationToken).ConfigureAwait(false);
        var data = await SendRequestAsync(
            method,
            new JsonObject { ["ModelId"] = modelId },
            cancellationToken).ConfigureAwait(false);
        return NizimaNamedOption.FromArray(data, arrayName, nameProperty, pathProperty);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket is { State: WebSocketState.Open } ws)
            {
                using var stream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                HandleMessage(Encoding.UTF8.GetString(stream.ToArray()));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            StatusText = "切断されました";
            RaiseStatus();
        }
    }

    internal void HandleMessage(string json)
    {
        JsonObject? obj;
        try
        {
            obj = JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return;
        }

        if (obj is null)
            return;

        var type = obj["Type"]?.GetValue<string>();
        var method = obj["Method"]?.GetValue<string>() ?? "";
        var data = obj["Data"];

        if (string.Equals(type, "Event", StringComparison.OrdinalIgnoreCase))
        {
            HandleEvent(method, data);
            return;
        }

        if (string.Equals(type, "Error", StringComparison.OrdinalIgnoreCase))
        {
            var errorType = data?["ErrorType"]?.GetValue<string>();
            if (NizimaProtocol.IsPluginDisabled(errorType))
            {
                ApplyEnabled(false);
                StatusText = "nizima LIVE 側で無効化されました";
                RaiseStatus();
            }

            CompleteNext(method, ex: new NizimaApiException(method, errorType));
            return;
        }

        if (string.Equals(type, "Response", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(method, "EstablishConnection", StringComparison.OrdinalIgnoreCase))
            {
                ApplyEnabled(ReadEnabled(ToElement(data)));
                StatusText = IsEnabled ? "接続済み（有効）" : "接続済み（nizima LIVE で有効化待ち）";
                RaiseStatus();
            }

            CompleteNext(method, result: ToElement(data));
        }
    }

    internal bool TryPeekWouldSend(string method) =>
        UnrestrictedMethods.Contains(method) || IsEnabled;

    internal void ApplyEnabledForTests(bool enabled) => ApplyEnabled(enabled);

    private void HandleEvent(string method, JsonNode? data)
    {
        if (string.Equals(method, "NotifyEnabledChanged", StringComparison.OrdinalIgnoreCase))
        {
            ApplyEnabled(data?["Enabled"]?.GetValue<bool>() == true);
            StatusText = IsEnabled ? "接続済み（有効）" : "接続済み（nizima LIVE で有効化待ち）";
            RaiseStatus();
            return;
        }

        if (method.Contains("Changed", StringComparison.OrdinalIgnoreCase))
            CatalogChanged?.Invoke();
    }

    private void CompleteNext(string method, JsonElement result = default, Exception? ex = null)
    {
        PendingRequest? found = null;
        lock (pending)
        {
            found = pending.FirstOrDefault(item => item.Method == method);
            if (found is not null)
                pending.Remove(found);
        }

        if (found is null)
            return;
        CompletePending(found, result, ex);
    }

    private static void CompletePending(PendingRequest request, JsonElement result = default, Exception? ex = null)
    {
        if (ex is not null)
            request.Tcs.TrySetException(ex);
        else
            request.Tcs.TrySetResult(result);
    }

    private void FailPending(Exception ex)
    {
        List<PendingRequest> copy;
        lock (pending)
        {
            copy = [.. pending];
            pending.Clear();
        }

        foreach (var item in copy)
            item.Tcs.TrySetException(ex);
    }

    private void ApplyEnabled(bool enabled) => IsEnabled = enabled;

    private static bool ReadEnabled(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object &&
        data.TryGetProperty("Enabled", out var node) &&
        node.ValueKind is JsonValueKind.True;

    private static JsonElement ToElement(JsonNode? data)
    {
        if (data is null)
            return JsonDocument.Parse("{}").RootElement.Clone();
        return JsonSerializer.Deserialize<JsonElement>(data.ToJsonString());
    }

    private void RaiseStatus() => StatusChanged?.Invoke();

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try { DisconnectAsync().GetAwaiter().GetResult(); }
        catch { }
        sendLock.Dispose();
    }

    private sealed record PendingRequest(string Method, TaskCompletionSource<JsonElement> Tcs);
}
