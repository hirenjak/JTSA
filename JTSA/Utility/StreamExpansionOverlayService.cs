using System.IO;
using System.Text.Json;
using JTSA.Plugin.Abstractions;

namespace JTSA.Utility;

internal static class StreamExpansionOverlayService
{
    private static readonly object StateLock = new();
    private static long version;
    private static string? cachedJson;
    private static readonly List<OverlayImage> Images = [];
    private static readonly Dictionary<string, ExpansionOverlayContent> PluginOverlays = [];

    private sealed record OverlayImage(
        long Id,
        string Path,
        DateTime VisibleUntilUtc,
        int Width,
        int Height,
        int X,
        int Y);

    public static void ShowImage(string path, TimeSpan? duration = null)
        => ShowImage(new StreamExpansionImageSettings(path), duration);

    public static void ShowImage(StreamExpansionImageSettings settings, TimeSpan? duration = null)
    {
        settings = settings.Normalize();
        if (!File.Exists(settings.Path)) return;

        lock (StateLock)
        {
            var x = settings.RandomPosition
                ? Random.Shared.Next(0, Math.Max(1, 1920 - settings.Width + 1))
                : settings.X;
            var y = settings.RandomPosition
                ? Random.Shared.Next(0, Math.Max(1, 1080 - settings.Height + 1))
                : settings.Y;
            var id = ++version;
            Images.Add(new OverlayImage(
                id,
                Path.GetFullPath(settings.Path),
                DateTime.UtcNow.Add(duration ?? TimeSpan.FromSeconds(5)),
                settings.Width,
                settings.Height,
                x,
                y));
            cachedJson = null;
        }
    }

    public static string CreateJson()
    {
        lock (StateLock)
        {
            RemoveExpiredImages();
            return cachedJson ??= JsonSerializer.Serialize(new
            {
                images = Images.Select(image => new
                {
                    id = image.Id,
                    imageUrl = $"/expansion-image?id={image.Id}",
                    width = image.Width,
                    height = image.Height,
                    x = image.X,
                    y = image.Y
                }),
                extensions = PluginOverlays.Select(item => new
                {
                    id = item.Key,
                    html = item.Value.Html,
                    x = item.Value.X,
                    y = item.Value.Y,
                    width = item.Value.Width,
                    height = item.Value.Height
                })
            });
        }
    }

    public static void SetPluginOverlay(string pluginId, ExpansionOverlayContent content)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(content.Id)) return;
        var key = $"{pluginId}:{content.Id}";
        var normalized = content with
        {
            X = Math.Clamp(content.X, 0, 1920),
            Y = Math.Clamp(content.Y, 0, 1080),
            Width = Math.Clamp(content.Width, 1, 1920),
            Height = Math.Clamp(content.Height, 1, 1080)
        };
        lock (StateLock)
        {
            if (PluginOverlays.TryGetValue(key, out var existing) && existing == normalized) return;
            PluginOverlays[key] = normalized;
            cachedJson = null;
        }
    }

    public static void RemovePluginOverlay(string pluginId, string id)
    {
        lock (StateLock)
        {
            if (PluginOverlays.Remove($"{pluginId}:{id}")) cachedJson = null;
        }
    }

    public static (byte[] Data, string ContentType)? GetImage(long id)
    {
        lock (StateLock)
        {
            RemoveExpiredImages();
            var image = Images.FirstOrDefault(item => item.Id == id);
            if (image is null || !File.Exists(image.Path)) return null;

            var contentType = Path.GetExtension(image.Path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            return (File.ReadAllBytes(image.Path), contentType);
        }
    }

    private static void RemoveExpiredImages()
    {
        var now = DateTime.UtcNow;
        if (Images.RemoveAll(image => now >= image.VisibleUntilUtc || !File.Exists(image.Path)) > 0)
            cachedJson = null;
    }

    public static string CreateHtml() => """
        <!DOCTYPE html>
        <html lang="ja">
        <head>
            <meta charset="UTF-8">
            <title>JTSA 配信拡張</title>
            <style>
                html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: transparent; }
                #viewport { position: absolute; left: 50%; top: 50%; width: 1920px; height: 1080px; transform-origin: center center; }
                .expansion-image { position: absolute; object-fit: contain; }
                .extension-overlay { position: absolute; overflow: hidden; }
            </style>
        </head>
        <body>
            <div id="viewport"></div>
            <script>
                const viewport = document.getElementById("viewport");
                let previousPayload = null;
                const renderedHtml = new WeakMap();

                function resizeCanvas() {
                    const scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
                    viewport.style.transform = `translate(-50%, -50%) scale(${scale})`;
                }

                async function refresh() {
                    try {
                        const response = await fetch("/expansion-data?t=" + Date.now(), { cache: "no-store" });
                        if (!response.ok) return;
                        const payload = await response.text();
                        if (payload === previousPayload) return;
                        const data = JSON.parse(payload);
                        const activeIds = new Set(data.images.map(item => String(item.id)));
                        viewport.querySelectorAll(".expansion-image").forEach(image => {
                            if (!activeIds.has(image.dataset.id)) image.remove();
                        });

                        for (const item of data.images) {
                            const id = String(item.id);
                            let image = viewport.querySelector(`[data-id="${id}"]`);
                            if (!image) {
                                image = document.createElement("img");
                                image.className = "expansion-image";
                                image.dataset.id = id;
                                image.alt = "";
                                image.src = item.imageUrl;
                                viewport.appendChild(image);
                            }
                            image.style.width = item.width + "px";
                            image.style.height = item.height + "px";
                            image.style.left = item.x + "px";
                            image.style.top = item.y + "px";
                        }

                        const extensions = data.extensions || [];
                        const activeExtensionIds = new Set(extensions.map(item => String(item.id)));
                        viewport.querySelectorAll(".extension-overlay").forEach(element => {
                            if (!activeExtensionIds.has(element.dataset.id)) element.remove();
                        });

                        for (const item of extensions) {
                            const id = String(item.id);
                            let element = viewport.querySelector(`.extension-overlay[data-id="${CSS.escape(id)}"]`);
                            if (!element) {
                                element = document.createElement("div");
                                element.className = "extension-overlay";
                                element.dataset.id = id;
                                viewport.appendChild(element);
                            }
                            if (renderedHtml.get(element) !== item.html) {
                                element.innerHTML = item.html;
                                renderedHtml.set(element, item.html);
                                element.querySelectorAll("[data-jtsa-animation-start]").forEach(target => {
                                    const start = Number(target.dataset.jtsaAnimationStart);
                                    if (!Number.isFinite(start)) return;
                                    target.style.animationDelay = `${Math.min(0, start - Date.now())}ms`;
                                });
                            }
                            element.style.left = item.x + "px";
                            element.style.top = item.y + "px";
                            element.style.width = item.width + "px";
                            element.style.height = item.height + "px";
                        }
                        previousPayload = payload;
                    }
                    catch {
                        // 一時的な通信失敗では表示中の画像を維持する
                    }
                    finally {
                        setTimeout(refresh, 100);
                    }
                }

                resizeCanvas();
                window.addEventListener("resize", resizeCanvas);
                refresh();
            </script>
        </body>
        </html>
        """;
}
