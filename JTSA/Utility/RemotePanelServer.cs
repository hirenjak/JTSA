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
    private readonly Dispatcher dispatcher;
    private readonly Action<Exception> onError;
    private readonly Task serving;
    private readonly byte[] page;
    private readonly object pairingLock = new();
    private int failedPairings;
    private DateTime lockUntilUtc;

    public string Key { get; }
    public string Pin { get; private set; } = CreatePin();

    private sealed record PairRequest(string Pin);
    private sealed record Request(string Method, string Path, Dictionary<string, string> Headers, byte[] Body);

    public RemotePanelServer(
        Func<RemotePanelSnapshot> getSnapshot,
        Func<TodoChange, bool> applyTodoChange,
        Dispatcher dispatcher,
        string key,
        int port,
        Action<Exception> onError)
    {
        this.getSnapshot = getSnapshot;
        this.applyTodoChange = applyTodoChange;
        this.dispatcher = dispatcher;
        this.onError = onError;
        Key = key;
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("JTSA.RemotePanel.html")
            ?? throw new FileNotFoundException("スマホ画面が見つかりません。");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        page = buffer.ToArray();
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
        if (first.Length != 3 || first[0] is not ("GET" or "POST") || !first[1].StartsWith('/')) return null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0) break;
            var colon = line.IndexOf(':');
            if (colon <= 0 || !headers.TryAdd(line[..colon], line[(colon + 1)..].Trim())) return null;
        }
        if (headers.ContainsKey("Transfer-Encoding")) return null;
        var length = 0;
        if (headers.TryGetValue("Content-Length", out var value)
            && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0 || length > 4096)) return null;
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
        var reason = status switch { 200 => "OK", 400 => "Bad Request", 403 => "Forbidden", 404 => "Not Found", 429 => "Too Many Requests", _ => "Error" };
        var text = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; img-src https: http: data:; form-action 'none'\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text), token);
        await stream.WriteAsync(body, token);
    }

    public void Dispose()
    {
        lifetime.Cancel();
        listener.Stop();
        _ = serving.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
    }
}

internal sealed record RemoteChatInfo(string Id, string User, string Message, string Color, DateTime Time, string ProfileImageUrl);
internal sealed record RemoteTodoInfo(Guid Id, string Text, bool IsCurrent, bool IsCompleted);
internal sealed record RemotePanelSnapshot(string Category, IReadOnlyList<RemoteChatInfo> Chat, IReadOnlyList<RemoteTodoInfo> Todos);
internal sealed record TodoChange(string Action, Guid? Id = null, string? Text = null, bool? Value = null);
