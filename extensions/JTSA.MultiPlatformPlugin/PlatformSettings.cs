using System.IO;
using System.Text.Json;

namespace JTSA.MultiPlatformPlugin;

internal sealed class PlatformSettings
{
    public string YouTubeBroadcastId { get; set; } = string.Empty;
    public string YouTubeCategoryId { get; set; } = "20";
    public string YouTubeLiveChatId { get; set; } = string.Empty;
    public string KickCategoryId { get; set; } = string.Empty;
    public string KickChannelSlug { get; set; } = string.Empty;

    public static PlatformSettings Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<PlatformSettings>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
