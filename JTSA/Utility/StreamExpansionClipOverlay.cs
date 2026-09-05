using System.Text.Json;

namespace JTSA.Utility;

internal static class StreamExpansionClipOverlay
{
    private static readonly object StateLock = new();
    private static Clip? current;
    private sealed record Clip(string Id, string VideoUrl, double DurationSeconds)
    {
        public bool IsStarted { get; set; }
        public DateTime ExpiresAtUtc { get; set; } =
            DateTime.UtcNow.AddSeconds(DurationSeconds + 5);
    }

    public static void ShowClip(string videoUrl, double durationSeconds)
    {
        if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !double.IsFinite(durationSeconds) || durationSeconds <= 0)
            throw new ArgumentException("クリップの動画URLまたは再生時間が不正です。");
        lock (StateLock)
        {
            // 読み込み失敗やOBS切断時にも表示状態が残らないよう猶予付きで破棄する。
            current = new Clip(Guid.NewGuid().ToString("N"), videoUrl,
                Math.Clamp(durationSeconds, 1, 300));
        }
    }

    public static bool TryStart(string id)
    {
        lock (StateLock)
        {
            if (current is null || !string.Equals(current.Id, id, StringComparison.Ordinal)) return false;
            if (!current.IsStarted)
            {
                current.IsStarted = true;
                current.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(current.DurationSeconds + 5);
            }
            return true;
        }
    }

    public static string CreateJson() => CreateJson(DateTime.UtcNow);

    internal static string CreateJson(DateTime now)
    {
        lock (StateLock)
        {
            if (current is not null && now >= current.ExpiresAtUtc) current = null;
            return JsonSerializer.Serialize(new
            {
                clip = current is null ? null : new
                {
                    id = current.Id,
                    videoUrl = current.VideoUrl,
                    durationMs = current.DurationSeconds * 1000,
                    remainingMs = Math.Max(0, (current.ExpiresAtUtc - now).TotalMilliseconds)
                }
            });
        }
    }

    public static string CreateHtml() => """
        <!DOCTYPE html>
        <html lang="ja">
        <head>
            <meta charset="UTF-8">
            <title>JTSA クリップ再生</title>
            <style>
                html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: transparent; }
                video { width: 100%; height: 100%; border: 0; display: block; object-fit: contain; }
            </style>
        </head>
        <body>
            <script>
                let clipId = null;
                let clipTimer;
                function refreshClip(item) {
                    if (!item) {
                        document.querySelector("video")?.remove();
                        clearTimeout(clipTimer);
                        clipId = null;
                        return;
                    }
                    if (clipId === item.id) return;
                    document.querySelector("video")?.remove();
                    clearTimeout(clipTimer);
                    clipId = item.id;
                    const player = document.createElement("video");
                    player.title = "Twitchクリップ";
                    player.autoplay = false;
                    player.controls = false;
                    player.playsInline = true;
                    // 音声はJTSAから再生する。OBS側との二重再生を防ぐ。
                    player.muted = true;
                    player.volume = 0;
                    player.src = item.videoUrl;
                    player.addEventListener("ended", () => player.remove(), { once: true });
                    player.addEventListener("error", () => player.remove(), { once: true });
                    player.addEventListener("canplay", async () => {
                        try {
                            const response = await fetch("/expansion-clips-ready?id=" + encodeURIComponent(item.id), { method: "POST" });
                            if (!response.ok) throw new Error("sync failed");
                            await player.play();
                            clearTimeout(clipTimer);
                            clipTimer = setTimeout(() => player.remove(), item.durationMs + 5000);
                        }
                        catch { player.remove(); }
                    }, { once: true });
                    document.body.appendChild(player);
                    player.load();
                    // 読み込み通知が来ない場合も表示状態を残さない。
                    clipTimer = setTimeout(() => player.remove(), item.remainingMs);
                }
                async function poll() {
                    try {
                        const response = await fetch("/expansion-clips-data", { cache: "no-store" });
                        if (response.ok) refreshClip((await response.json()).clip);
                    }
                    catch { /* Retry transient connection failures. */ }
                    finally { setTimeout(poll, 200); }
                }
                poll();
            </script>
        </body>
        </html>
        """;
}
