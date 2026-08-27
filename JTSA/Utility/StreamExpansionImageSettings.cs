using System.Text.Json;

namespace JTSA.Utility;

internal sealed record StreamExpansionImageSettings(
    string Path,
    int Width = 1920,
    int Height = 1080,
    int X = 0,
    int Y = 0,
    bool RandomPosition = false)
{
    private const string Prefix = "jtsa-image:";

    public string Encode() => Prefix + JsonSerializer.Serialize(this);

    public static StreamExpansionImageSettings Decode(string? content)
    {
        content ??= string.Empty;
        if (!content.StartsWith(Prefix, StringComparison.Ordinal))
            return new(content);
        try
        {
            return JsonSerializer.Deserialize<StreamExpansionImageSettings>(content[Prefix.Length..])
                ?? new(string.Empty);
        }
        catch
        {
            return new(string.Empty);
        }
    }

    public StreamExpansionImageSettings Normalize() => this with
    {
        Width = Math.Clamp(Width, 1, 1920),
        Height = Math.Clamp(Height, 1, 1080),
        X = Math.Clamp(X, 0, Math.Max(0, 1920 - Math.Clamp(Width, 1, 1920))),
        Y = Math.Clamp(Y, 0, Math.Max(0, 1080 - Math.Clamp(Height, 1, 1080)))
    };
}
