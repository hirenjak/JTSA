using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace JTSA.Utility;

internal sealed class RemotePanelServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim connectionSlots = new(16);
    private readonly Func<RemotePanelSnapshot> getSnapshot;
    private readonly Func<TodoChange, bool> applyTodoChange;
    private readonly Func<RemoteObsChange, Task<bool>> applyObsChange;
    private readonly Func<bool, Task<RemotePreview>> getPreview;
    private readonly RemoteWebRtcPreview webRtc;
    private readonly Dispatcher dispatcher;
    private readonly Action<Exception> onError;
    private readonly Task serving;
    private readonly byte[] page;
    private readonly byte[] noSleepScript;
    private readonly byte[] noSleepLicense;
    private readonly object pairingLock = new();
    private int failedPairings;
    private DateTime lockUntilUtc;

    public string Key { get; }
    public string Pin { get; private set; } = CreatePin();

    private sealed record PairRequest(string Pin);
    private sealed record PluginActionRequest(string PanelId, string Action, string? Value);
    private sealed record Request(string Method, string Path, Dictionary<string, string> Headers, byte[] Body);

    public RemotePanelServer(
        Func<RemotePanelSnapshot> getSnapshot,
        Func<TodoChange, bool> applyTodoChange,
        Func<RemoteObsChange, Task<bool>> applyObsChange,
        Func<bool, Task<RemotePreview>> getPreview,
        Dispatcher dispatcher,
        string key,
        int port,
        Action<Exception> onError)
    {
        this.getSnapshot = getSnapshot;
        this.applyTodoChange = applyTodoChange;
        this.applyObsChange = applyObsChange;
        this.getPreview = getPreview;
        this.dispatcher = dispatcher;
        webRtc = new RemoteWebRtcPreview(async isSub =>
            await (await dispatcher.InvokeAsync(() => getPreview(isSub), DispatcherPriority.Background).Task));
        this.onError = onError;
        Key = key;
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("JTSA.RemotePanel.html")
            ?? throw new FileNotFoundException("スマホ画面が見つかりません。");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        page = buffer.ToArray();
        noSleepScript = ReadResource("JTSA.NoSleep.min.js");
        noSleepLicense = ReadResource("JTSA.NoSleep.LICENSE.txt");
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        serving = ServeAsync();
    }

    private async Task ServeAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                if (!connectionSlots.Wait(0)) { client.Dispose(); continue; }
                _ = HandleAsync(client);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (lifetime.IsCancellationRequested) { }
        catch (SocketException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested) onError(ex); }
    }

    private static byte[] ReadResource(string name)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new FileNotFoundException(name);
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var token = timeout.Token;
                var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, token);
                if (request == null)
                {
                    await ReplyAsync(stream, 400, "text/plain; charset=utf-8", "Invalid request"u8.ToArray(), token);
                    return;
                }
                if (request.Method == "GET" && request.Path is "/controls" or "/controls/")
                {
                    await ReplyAsync(stream, 200, "text/html; charset=utf-8", page, token);
                    return;
                }
                if (request.Method == "GET" && request.Path == "/controls/NoSleep.min.js")
                {
                    await ReplyAsync(stream, 200, "application/javascript; charset=utf-8", noSleepScript, token);
                    return;
                }
                if (request.Method == "GET" && request.Path == "/controls/NoSleep.LICENSE.txt")
                {
                    await ReplyAsync(stream, 200, "text/plain; charset=utf-8", noSleepLicense, token);
                    return;
                }
                if (request.Method == "POST" && request.Path == "/controls/api/pair")
                {
                    PairRequest? pairRequest;
                    try { pairRequest = JsonSerializer.Deserialize<PairRequest>(request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
                    catch (JsonException) { pairRequest = null; }
                    var status = CheckPin(pairRequest?.Pin);
                    if (status != 200)
                    {
                        await ReplyAsync(stream, status, "text/plain; charset=utf-8",
                            status == 429 ? "Too many attempts"u8.ToArray() : "Invalid code"u8.ToArray(), token);
                        return;
                    }
                    await ReplyAsync(stream, 200, "application/json; charset=utf-8",
                        JsonSerializer.SerializeToUtf8Bytes(new { key = Key }), token);
                    return;
                }
                if (!request.Headers.TryGetValue("X-JTSA-Key", out var accessKey) || !SecureEquals(accessKey, Key))
                {
                    await ReplyAsync(stream, 403, "text/plain; charset=utf-8", "Forbidden"u8.ToArray(), token);
                    return;
                }
                if (request.Method == "GET" && request.Path == "/controls/api/snapshot")
                {
                    var snapshot = await dispatcher.InvokeAsync(getSnapshot,
                        DispatcherPriority.Background, token).Task;
                    await ReplyAsync(stream, 200, "application/json; charset=utf-8",
                        JsonSerializer.SerializeToUtf8Bytes(snapshot), token);
                    return;
                }
                if (request.Method == "POST" && request.Path == "/controls/api/plugin/action")
                {
                    PluginActionRequest? change;
                    try { change = JsonSerializer.Deserialize<PluginActionRequest>(request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
                    catch (JsonException) { change = null; }
                    var applied = change is { PanelId: { Length: > 0 and <= 128 }, Action: { Length: > 0 and <= 64 }, Value: null or { Length: <= 1000 } } &&
                        await dispatcher.InvokeAsync(() => RemotePanelRegistry.ApplyAction(
                            change.PanelId, change.Action, change.Value), DispatcherPriority.Background, token).Task;
                    await ReplyAsync(stream, applied ? 200 : 400, "application/json; charset=utf-8",
                        applied ? "{}"u8.ToArray() : "{\"Error\":\"Invalid action\"}"u8.ToArray(), token);
                    return;
                }
                if (request.Method == "GET" && request.Path is "/controls/api/preview/main" or "/controls/api/preview/sub")
                {
                    var preview = await (await dispatcher.InvokeAsync(
                        () => getPreview(request.Path.EndsWith("/sub", StringComparison.Ordinal)),
                        DispatcherPriority.Background, token).Task);
                    await ReplyAsync(stream, 200, "application/json; charset=utf-8",
                        JsonSerializer.SerializeToUtf8Bytes(preview), token);
                    return;
                }
                if (request.Method == "POST" && request.Path == "/controls/api/preview/webrtc")
                {
                    RemoteWebRtcOffer? offer;
                    try { offer = JsonSerializer.Deserialize<RemoteWebRtcOffer>(request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
                    catch (JsonException) { offer = null; }
                    if (offer == null)
                    {
                        await ReplyAsync(stream, 400, "application/json; charset=utf-8",
                            JsonSerializer.SerializeToUtf8Bytes(new { Error = "接続情報が不正です。" }), token);
                        return;
                    }
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    var answer = await webRtc.CreateAsync(offer, token);
                    await ReplyAsync(stream, answer.Error == null ? 200 : 503, "application/json; charset=utf-8",
                        JsonSerializer.SerializeToUtf8Bytes(answer), token);
                    return;
                }
                if (request.Path.StartsWith("/controls/api/preview/webrtc/", StringComparison.Ordinal))
                {
                    var id = request.Path["/controls/api/preview/webrtc/".Length..];
                    if (id.Length != 32 || !id.All(Uri.IsHexDigit))
                    {
                        await ReplyAsync(stream, 400, "text/plain; charset=utf-8", "Invalid session"u8.ToArray(), token);
                        return;
                    }
                    if (request.Method == "DELETE")
                    {
                        await webRtc.CloseAsync(id);
                        await ReplyAsync(stream, 200, "application/json; charset=utf-8", "{}"u8.ToArray(), token);
                        return;
                    }
                    if (request.Method == "POST")
                    {
                        var active = await webRtc.TouchAsync(id);
                        await ReplyAsync(stream, active ? 200 : 404, "application/json; charset=utf-8", "{}"u8.ToArray(), token);
                        return;
                    }
                }
                if (request.Method == "POST" && request.Path == "/controls/api/todo")
                {
                    TodoChange? change;
                    try { change = JsonSerializer.Deserialize<TodoChange>(request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
                    catch (JsonException) { change = null; }
                    if (change == null || !await dispatcher.InvokeAsync(() => applyTodoChange(change),
                            DispatcherPriority.Background, token).Task)
                    {
                        await ReplyAsync(stream, 400, "text/plain; charset=utf-8", "Invalid change"u8.ToArray(), token);
                        return;
                    }
                    await ReplyAsync(stream, 200, "application/json; charset=utf-8", "{}"u8.ToArray(), token);
                    return;
                }
                if (request.Method == "POST" && request.Path == "/controls/api/obs")
                {
                    RemoteObsChange? change;
                    try { change = JsonSerializer.Deserialize<RemoteObsChange>(request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
                    catch (JsonException) { change = null; }
                    var applied = change != null && await (await dispatcher.InvokeAsync(
                        () => applyObsChange(change), DispatcherPriority.Background, token).Task);
                    if (!applied)
                    {
                        await ReplyAsync(stream, 400, "text/plain; charset=utf-8", "Invalid change"u8.ToArray(), token);
                        return;
                    }
                    await ReplyAsync(stream, 200, "application/json; charset=utf-8", "{}"u8.ToArray(), token);
                    return;
                }
                await ReplyAsync(stream, 404, "text/plain; charset=utf-8", "Not found"u8.ToArray(), token);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or ObjectDisposedException) { }
        catch (Exception ex) { if (!lifetime.IsCancellationRequested) onError(ex); }
        finally { connectionSlots.Release(); }
    }

    private static async Task<Request?> ReadRequestAsync(NetworkStream stream, CancellationToken token)
    {
        using var header = new MemoryStream();
        var marker = 0;
        var single = new byte[1];
        while (header.Length < 8192)
        {
            if (await stream.ReadAsync(single, token) == 0) return null;
            header.WriteByte(single[0]);
            marker = (marker << 8) | single[0];
            if (marker == 0x0D0A0D0A) break;
        }
        if (marker != 0x0D0A0D0A) return null;
        var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n", StringSplitOptions.None);
        var first = lines[0].Split(' ');
        if (first.Length != 3 || first[0] is not ("GET" or "POST" or "DELETE") || !first[1].StartsWith('/')) return null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0) break;
            var colon = line.IndexOf(':');
            if (colon <= 0 || !headers.TryAdd(line[..colon], line[(colon + 1)..].Trim())) return null;
        }
        if (headers.ContainsKey("Transfer-Encoding")) return null;
        var length = 0;
        var maxLength = first[1] == "/controls/api/preview/webrtc" ? 262144 : 4096;
        if (headers.TryGetValue("Content-Length", out var value)
            && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0 || length > maxLength)) return null;
        if (first[0] == "POST" && length == 0) return null;
        var body = new byte[length];
        if (length > 0) await stream.ReadExactlyAsync(body, token);
        return new Request(first[0], first[1].Split('?')[0], headers, body);
    }

    private int CheckPin(string? supplied)
    {
        lock (pairingLock)
        {
            if (DateTime.UtcNow < lockUntilUtc) return 429;
            if (supplied != null && SecureEquals(supplied, Pin)) { failedPairings = 0; return 200; }
            if (++failedPairings >= 5) { failedPairings = 0; lockUntilUtc = DateTime.UtcNow.AddMinutes(1); }
            return 403;
        }
    }

    public void RegeneratePin()
    {
        lock (pairingLock)
        {
            Pin = CreatePin();
            failedPairings = 0;
            lockUntilUtc = default;
        }
    }

    private static string CreatePin() =>
        RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private static bool SecureEquals(string supplied, string expected)
    {
        if (supplied.Length != expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(expected));
    }

    private static async Task ReplyAsync(NetworkStream stream, int status, string contentType, byte[] body, CancellationToken token)
    {
        var reason = status switch { 200 => "OK", 400 => "Bad Request", 403 => "Forbidden", 404 => "Not Found", 429 => "Too Many Requests", 503 => "Service Unavailable", _ => "Error" };
        var text = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; media-src 'self' data: blob:; img-src https: http: data: blob:; form-action 'none'\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text), token);
        await stream.WriteAsync(body, token);
    }

    public void Dispose()
    {
        lifetime.Cancel();
        listener.Stop();
        _ = webRtc.DisposeAsync().AsTask();
        _ = serving.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
    }
}

internal sealed record RemotePreview(string? SceneName, string? ImageData, string? Error);
internal sealed record RemoteChatInfo(string Id, string User, string Message, string Color, DateTime Time, string ProfileImageUrl);
internal sealed record RemoteTodoInfo(Guid Id, string Text, bool IsCurrent, bool IsCompleted);
internal sealed record RemoteObsSceneInfo(long AccountId, bool IsSub, string SceneName, string DisplayName, bool IsCurrent);
internal sealed record RemoteObsSourceInfo(long AccountId, bool IsSub, string SceneName, string SourceName, string ContainerName, string DisplayName, string DetailText, bool IsVisible);
internal sealed record RemotePanelSnapshot(
    string Category,
    IReadOnlyList<RemoteChatInfo> Chat,
    IReadOnlyList<RemoteTodoInfo> Todos,
    IReadOnlyList<RemoteObsSceneInfo> Scenes,
    IReadOnlyList<RemoteObsSourceInfo> Sources,
    IReadOnlyList<RemotePanelInfo>? Panels = null);
internal sealed record TodoChange(string Action, Guid? Id = null, string? Text = null, bool? Value = null);
internal sealed record RemoteObsChange(string Action, long AccountId, bool IsSub, string SceneName, string? SourceName = null, string? ContainerName = null);
