using System.IO;
using System.Text.Json;

namespace JTSA.Utility;

internal static class StreamExpansionOverlayService
{
    private static readonly object StateLock = new();
    private static string imagePath = string.Empty;
    private static DateTime visibleUntilUtc;
    private static long version;

    public static void ShowImage(string path, TimeSpan? duration = null)
    {
        if (!File.Exists(path)) return;

        lock (StateLock)
        {
            imagePath = Path.GetFullPath(path);
            visibleUntilUtc = DateTime.UtcNow.Add(duration ?? TimeSpan.FromSeconds(5));
            version++;
        }
    }

    public static string CreateJson()
    {
        lock (StateLock)
        {
            var visible = DateTime.UtcNow < visibleUntilUtc && File.Exists(imagePath);
            return JsonSerializer.Serialize(new
            {
                visible,
                imageUrl = visible ? $"/expansion-image?v={version}" : string.Empty
            });
        }
    }

    public static (byte[] Data, string ContentType)? GetImage()
    {
        lock (StateLock)
        {
            if (!File.Exists(imagePath)) return null;

            var contentType = Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            return (File.ReadAllBytes(imagePath), contentType);
        }
    }

    public static string CreateHtml() => """
        <!DOCTYPE html>
        <html lang="ja">
        <head>
            <meta charset="UTF-8">
            <title>JTSA 配信拡張</title>
            <style>
                html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: transparent; }
                body { display: flex; align-items: center; justify-content: center; }
                #expansionImage { display: none; max-width: 100%; max-height: 100%; object-fit: contain; }
            </style>
        </head>
        <body>
            <img id="expansionImage" alt="">
            <script>
                const image = document.getElementById("expansionImage");
                let currentUrl = "";

                async function refresh() {
                    try {
                        const response = await fetch("/expansion-data?t=" + Date.now(), { cache: "no-store" });
                        const data = await response.json();
                        if (!data.visible) {
                            image.style.display = "none";
                            return;
                        }
                        if (currentUrl !== data.imageUrl) {
                            currentUrl = data.imageUrl;
                            image.src = currentUrl;
                        }
                        image.style.display = "block";
                    }
                    catch {
                        image.style.display = "none";
                    }
                }

                refresh();
                setInterval(refresh, 100);
            </script>
        </body>
        </html>
        """;
}
