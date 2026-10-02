using System.Text;
using System.Text.Json;
using JTSA.Utility;
using Microsoft.Playwright;
using Xunit;

namespace JTSA.Tests;

public sealed class RemoteWebRtcPreviewTests
{
    [Fact]
    [Trait("Category", "BrowserIntegration")]
    public async Task VideoOnlyOffer_DeliversFrames_AndClosesSession()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var receiverBrowser = await playwright.Chromium.LaunchAsync(new()
        {
            Channel = "msedge", Headless = true,
            Args = ["--force-webrtc-ip-handling-policy=default_public_and_private_interfaces"]
        });
        var receiver = await receiverBrowser.NewPageAsync();
        var image = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='320' height='180'><rect width='320' height='180' fill='teal'/></svg>"));
        await using var service = new RemoteWebRtcPreview(_ => Task.FromResult(new RemotePreview("Test", image, null)));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        RemoteWebRtcAnswer? answer = null;
        var assembly = typeof(ObsController).Assembly;
        using var pageResource = assembly.GetManifestResourceStream("JTSA.RemotePanel.html")!;
        using var pageReader = new System.IO.StreamReader(pageResource);
        var html = await pageReader.ReadToEndAsync();
        using var scriptResource = assembly.GetManifestResourceStream("JTSA.NoSleep.min.js")!;
        using var scriptReader = new System.IO.StreamReader(scriptResource);
        var script = await scriptReader.ReadToEndAsync();
        await receiver.AddInitScriptAsync("localStorage.setItem('jtsa-panel-key', 'test')");
        await receiver.RouteAsync("**/*", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            object data;
            if (path == "/controls/")
            {
                await route.FulfillAsync(new() { ContentType = "text/html", Body = html,
                    Headers = new Dictionary<string, string> { ["Content-Security-Policy"] =
                        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; media-src 'self' data: blob:; img-src data: blob:" } });
                return;
            }
            if (path == "/controls/NoSleep.min.js")
            {
                await route.FulfillAsync(new() { ContentType = "application/javascript", Body = script });
                return;
            }
            if (path == "/controls/api/preview/webrtc")
            {
                var offer = JsonSerializer.Deserialize<RemoteWebRtcOffer>(route.Request.PostData!,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                answer = await service.CreateAsync(offer, timeout.Token);
                data = answer;
            }
            else if (path.StartsWith("/controls/api/preview/webrtc/"))
            {
                var id = path.Split('/')[^1];
                if (route.Request.Method == "DELETE") await service.CloseAsync(id);
                else await service.TouchAsync(id);
                data = new { };
            }
            else if (path.StartsWith("/controls/api/preview/")) data = new RemotePreview("Test", image, null);
            else data = new { Category = "Test", Chat = Array.Empty<object>(), Todos = Array.Empty<object>(),
                Scenes = Array.Empty<object>(), Sources = Array.Empty<object>(), Panels = Array.Empty<object>() };
            await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(data) });
        });
        await receiver.GotoAsync("http://jtsa.test/controls/");
        await receiver.WaitForFunctionAsync("document.getElementById('pairScreen').classList.contains('hidden')");
        await receiver.Locator("#previewButton").ClickAsync();
        Assert.Equal(0, await receiver.Locator("#previewRealtimeButton").CountAsync());
        await receiver.WaitForFunctionAsync("""
            () => previewPeer?.connectionState === 'connected' &&
              previewVideo.readyState >= 2 && previewVideo.videoWidth > 0
            """, options: new() { Timeout = 20000 });
        Assert.True(await receiver.EvaluateAsync<bool>("""
            async () => [...(await previewPeer.getStats()).values()].some(stat =>
              stat.type === 'inbound-rtp' && stat.kind === 'video' && stat.framesDecoded > 0)
            """));
        var receivedFps = await receiver.EvaluateAsync<double>("""
            async () => {
              const frames = async () => [...(await previewPeer.getStats()).values()]
                .find(stat => stat.type === 'inbound-rtp' && stat.kind === 'video')?.framesDecoded || 0;
              const before = await frames(), started = performance.now();
              await new Promise(resolve => setTimeout(resolve, 2000));
              return ((await frames()) - before) * 1000 / (performance.now() - started);
            }
            """);
        Assert.True(receivedFps > 10, $"Received only {receivedFps:F1} fps with synthetic frames.");
        Assert.NotNull(answer);
        Assert.Null(answer.Error);
        Assert.NotNull(answer.SessionId);
        Assert.DoesNotContain("m=audio", answer.Sdp!);
        Assert.True(await receiver.Locator("#previewVideo").IsVisibleAsync());
        Assert.False(await receiver.Locator("#sleepControls").IsVisibleAsync());
        Assert.True(await service.TouchAsync(answer.SessionId!));
        await receiver.Locator("#previewButton").ClickAsync();
        await receiver.WaitForFunctionAsync("previewPeer === null && previewVideo.srcObject === null");
        Assert.True(await receiver.Locator("#sleepControls").IsVisibleAsync());
        await service.CloseAsync(answer.SessionId!);
        Assert.False(await service.TouchAsync(answer.SessionId!));
    }

    [Fact]
    public async Task InvalidOffer_IsRejectedBeforeStartingBrowser()
    {
        await using var service = new RemoteWebRtcPreview(_ => throw new InvalidOperationException());
        var answer = await service.CreateAsync(new(false, ""), CancellationToken.None);
        Assert.NotNull(answer.Error);
        Assert.Null(answer.SessionId);
    }
}
