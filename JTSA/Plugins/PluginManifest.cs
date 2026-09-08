using System.Text.Json.Serialization;

namespace JTSA.Plugins;

internal sealed class PluginManifest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("entryAssembly")]
    public string EntryAssembly { get; init; } = string.Empty;

    [JsonPropertyName("entryType")]
    public string? EntryType { get; init; }

    [JsonPropertyName("apiVersion")]
    public int ApiVersion { get; init; } = 1;
}
