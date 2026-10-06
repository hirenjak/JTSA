using Microsoft.Playwright;
using System.IO;

namespace JTSA.Utility;

internal sealed record RemoteWebRtcOffer(bool IsSub, string Sdp, string? ClientId = null);
internal sealed record RemoteWebRtcAnswer(string? SessionId, string? Sdp, string? Error);

/// <summary>Encodes OBS snapshots as a video-only WebRTC track in a private headless browser.</summary>
internal sealed class RemoteWebRtcPreview : IAsyncDisposable
{
    private readonly Func<bool, Task<RemotePreview>> getFrame;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, Session> sessions = new();
    private IPlaywright? playwright;
    private IBrowser? browser;
    private bool disposed;

    private sealed class Session(string id, string? clientId, bool isSub, IPage page, CancellationTokenSource stop)
    {
        public string Id { get; } = id;
        public string? ClientId { get; } = clientId;
        public bool IsSub { get; } = isSub;
        public IPage Page { get; } = page;
        public CancellationTokenSource Stop { get; } = stop;
        public long LastSeen = Environment.TickCount64;
        public Task Pump { get; set; } = Task.CompletedTask;
    }

    internal const string SenderHtml = """
        <!doctype html><html><body><canvas id="frame"></canvas><script>
        let peer, track;
        const canvas = document.getElementById('frame');
        const context = canvas.getContext('2d', {alpha:false});
        window.pushFrame = async data => {
          const image = new Image(); image.src = data;
          await image.decode();
          if (canvas.width !== image.width || canvas.height !== image.height) {
            canvas.width = image.width; canvas.height = image.height;
          }
          context.drawImage(image, 0, 0);
          if (track?.requestFrame) track.requestFrame();
        };
        window.answerOffer = async sdp => {
          peer = new RTCPeerConnection({iceServers:[]});
          const stream = canvas.captureStream(30);
          track = stream.getVideoTracks()[0];
          track.contentHint = 'motion';
          peer.addTrack(track, stream);
          await peer.setRemoteDescription({type:'offer', sdp});
          const sender = peer.getSenders().find(sender => sender.track?.kind === 'video');
          const parameters = sender.getParameters();
          if (parameters.encodings?.length) {
            parameters.encodings[0].maxBitrate = 1500000;
            parameters.encodings[0].maxFramerate = 30;
            await sender.setParameters(parameters);
          }
          await peer.setLocalDescription(await peer.createAnswer());
          await new Promise((resolve, reject) => {
            if (peer.iceGatheringState === 'complete') { resolve(); return; }
            const timeout = setTimeout(() => reject(new Error('ICE gathering timed out')), 7000);
            peer.addEventListener('icegatheringstatechange', () => {
              if (peer.iceGatheringState === 'complete') { clearTimeout(timeout); resolve(); }
            });
          });
          return peer.localDescription.sdp;
        };
        window.peerState = () => peer?.connectionState || 'new';
        </script></body></html>
        """;

    public RemoteWebRtcPreview(Func<bool, Task<RemotePreview>> getFrame) => this.getFrame = getFrame;

    public async Task<RemoteWebRtcAnswer> CreateAsync(RemoteWebRtcOffer offer, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(offer.Sdp) || offer.Sdp.Length > 200_000)
            return new(null, null, "接続情報が不正です。");
        if (offer.ClientId is { Length: > 64 })
            return new(null, null, "接続情報が不正です。");
        await gate.WaitAsync(token);
        IPage? page = null;
        var stage = "OBS画像の取得";
        try
        {
            if (disposed) return new(null, null, "サーバーは停止しています。");
            // Reconnects can arrive before the previous sender pump has shut down.
            foreach (var old in sessions.Values.Where(s => s.ClientId != null && s.ClientId == offer.ClientId))
                old.Stop.Cancel();
            if (sessions.Values.Count(s => !s.Stop.IsCancellationRequested) >= 2)
                return new(null, null, "リアルタイム接続は最大2台です。");
            var first = await getFrame(offer.IsSub);
            if (first.ImageData == null) return new(null, null, first.Error);
            if (browser?.IsConnected != true)
            {
                stage = "ブラウザの起動";
                // Costura embeds Playwright, so its assembly location is not the app directory.
                // Point it at the driver copied beside JTSA, as the existing browser service does.
                var driverRoot = AppContext.BaseDirectory;
                if (!Directory.Exists(Path.Combine(driverRoot, ".playwright")))
                    return new(null, null, "WebRTC用ファイルが見つかりません。JTSAを再ビルドしてください。");
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", driverRoot,
                    EnvironmentVariableTarget.Process);
                playwright?.Dispose();
                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Channel = "msedge", Headless = true, Timeout = 15000,
                    Args = ["--disable-background-timer-throttling", "--disable-renderer-backgrounding",
                        "--force-webrtc-ip-handling-policy=default_public_and_private_interfaces"]
                });
            }
            page = await browser.NewPageAsync();
            stage = "映像トラックの準備";
            // This private sender has no external resources or navigation.
            await page.RouteAsync("**/*", route => route.AbortAsync());
            await page.SetContentAsync(SenderHtml);
            await page.EvaluateAsync("data => window.pushFrame(data)", first.ImageData);
            stage = "WebRTC接続情報の作成";
            var sdp = await page.EvaluateAsync<string>("sdp => window.answerOffer(sdp)", offer.Sdp);
            token.ThrowIfCancellationRequested();
            var session = new Session(Guid.NewGuid().ToString("N"), offer.ClientId, offer.IsSub, page,
                CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token));
            sessions.Add(session.Id, session);
            session.Pump = PumpAsync(session);
            page = null;
            return new(session.Id, sdp, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new(null, null, $"WebRTC送信を開始できませんでした：{stage}（{ex.GetType().Name}）");
        }
        finally
        {
            if (page != null) await page.CloseAsync().ContinueWith(_ => { });
            gate.Release();
        }
    }

    public async Task<bool> TouchAsync(string id)
    {
        await gate.WaitAsync();
        try
        {
            if (!sessions.TryGetValue(id, out var session) || session.Stop.IsCancellationRequested) return false;
            Interlocked.Exchange(ref session.LastSeen, Environment.TickCount64);
            return true;
        }
        finally { gate.Release(); }
    }

    public async Task CloseAsync(string id)
    {
        await gate.WaitAsync();
        try { if (sessions.TryGetValue(id, out var session)) session.Stop.Cancel(); }
        finally { gate.Release(); }
    }

    private async Task PumpAsync(Session session)
    {
        try
        {
            var disconnectedAt = 0L;
            var missingFrames = 0;
            while (!session.Stop.IsCancellationRequested &&
                   Environment.TickCount64 - Interlocked.Read(ref session.LastSeen) < 20000)
            {
                session.Stop.Token.ThrowIfCancellationRequested();
                var frameStarted = Environment.TickCount64;
                var state = await session.Page.EvaluateAsync<string>("window.peerState()");
                if (state is "failed" or "closed") break;
                if (state != "connected")
                {
                    if (disconnectedAt == 0) disconnectedAt = Environment.TickCount64;
                    if (Environment.TickCount64 - disconnectedAt > 15000) break;
                    await Task.Delay(100, session.Stop.Token);
                    continue;
                }
                disconnectedAt = 0;
                var frame = await getFrame(session.IsSub);
                if (frame.ImageData != null)
                {
                    missingFrames = 0;
                    await session.Page.EvaluateAsync("data => window.pushFrame(data)", frame.ImageData);
                }
                else if (++missingFrames >= 90) break;
                // Include screenshot and transfer time in the 30fps budget instead of adding
                // a fixed delay after every frame; avoid capturing faster than we can send.
                var delay = Math.Max(1, 33 - (Environment.TickCount64 - frameStarted));
                await Task.Delay((int)delay, session.Stop.Token);
            }
        }
        catch (Exception) { /* Disconnection and shutdown are expected here. */ }
        finally
        {
            await gate.WaitAsync();
            try { sessions.Remove(session.Id); }
            finally { gate.Release(); }
            await session.Page.CloseAsync().ContinueWith(_ => { });
            session.Stop.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        Task[] pumps;
        try
        {
            disposed = true;
            lifetime.Cancel();
            pumps = sessions.Values.Select(session => session.Pump).ToArray();
        }
        finally { gate.Release(); }
        await Task.WhenAll(pumps);
        if (browser != null) await browser.CloseAsync().ContinueWith(_ => { });
        playwright?.Dispose();
        lifetime.Dispose();
    }
}
