using System.Text.Json;
using System.Text.Json.Nodes;

namespace JTSA.NizimaLivePlugin;

public static class NizimaProtocol
{
    public const string ApiVersion = "1.0.0";
    public const string PluginName = "JTSA";
    public const string PluginDeveloper = "JakTwtchStreamerAssistant";
    public const string DefaultWebSocketUrl = "ws://127.0.0.1:22022/";

    public static JsonObject CreateRequest(string method, JsonNode? data = null) => new()
    {
        ["nLPlugin"] = ApiVersion,
        ["Type"] = "Request",
        ["Method"] = method,
        ["Data"] = data ?? new JsonObject()
    };

    public static bool ShouldReregister(string? errorType) =>
        string.Equals(errorType, "InvalidToken", StringComparison.OrdinalIgnoreCase);

    public static bool IsPluginDisabled(string? errorType) =>
        string.Equals(errorType, "PluginDisabled", StringComparison.OrdinalIgnoreCase);

    public static bool IsNonLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return true;
        if (uri.IsLoopback)
            return false;
        return !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class NizimaApiException : Exception
{
    public NizimaApiException(string method, string? errorType, string? message = null)
        : base(message ?? $"{method}: {errorType ?? "Error"}")
    {
        Method = method;
        ErrorType = errorType;
    }

    public string Method { get; }
    public string? ErrorType { get; }
}

public static class NizimaJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
