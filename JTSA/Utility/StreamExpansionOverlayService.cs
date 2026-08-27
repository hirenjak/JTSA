using System.IO;
using System.Text.Json;

namespace JTSA.Utility;

internal static class StreamExpansionOverlayService
{
    private static readonly object StateLock = new();
    private static long version;
    private static readonly List<OverlayImage> Images = [];

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
        }
    }

    public static string CreateJson()
    {
        lock (StateLock)
        {
            RemoveExpiredImages();
            return JsonSerializer.Serialize(new
            {
                images = Images.Select(image => new
                {
                    id = image.Id,
                    imageUrl = $"/expansion-image?id={image.Id}",
                    width = image.Width,
                    height = image.Height,
                    x = image.X,
                    y = image.Y
                })
            });
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
        Images.RemoveAll(image => now >= image.VisibleUntilUtc || !File.Exists(image.Path));
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
            </style>
        </head>
        <body>
            <div id="viewport"></div>
            <script>
                const viewport = document.getElementById("viewport");

                function resizeCanvas() {
                    const scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
                    viewport.style.transform = `translate(-50%, -50%) scale(${scale})`;
                }

                async function refresh() {
                    try {
                        const response = await fetch("/expansion-data?t=" + Date.now(), { cache: "no-store" });
                        const data = await response.json();
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
                    }
                    catch {
                        // 一時的な通信失敗では表示中の画像を維持する
                    }
                }

                resizeCanvas();
                window.addEventListener("resize", resizeCanvas);
                refresh();
                setInterval(refresh, 100);
            </script>
        </body>
        </html>
        """;
}
