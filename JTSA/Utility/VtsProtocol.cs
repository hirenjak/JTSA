using Newtonsoft.Json.Linq;

namespace JTSA.Utility;

/// <summary>VTube Studio Public API の JSON 組み立て。接続とは独立して検証できる。</summary>
public static class VtsProtocol
{
    public const string ApiName = "VTubeStudioPublicAPI";
    public const string ApiVersion = "1.0";
    public const string DefaultWebSocketUrl = "ws://127.0.0.1:8001";
    public const string PluginName = "JTSA";
    public const string PluginDeveloper = "JakTwtchStreamerAssistant";

    public static JObject CreateEnvelope(string messageType, object? data = null, string? requestId = null)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("messageType は必須です。", nameof(messageType));

        var envelope = new JObject
        {
            ["apiName"] = ApiName,
            ["apiVersion"] = ApiVersion,
            ["requestID"] = string.IsNullOrWhiteSpace(requestId) ? NewRequestId() : requestId,
            ["messageType"] = messageType
        };

        if (data is JToken token)
            envelope["data"] = token;
        else if (data is not null)
            envelope["data"] = JObject.FromObject(data);

        return envelope;
    }

    public static JObject CompleteRawJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON が空です。", nameof(json));

        JObject envelope;
        try
        {
            envelope = JObject.Parse(json);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("JSON として解釈できません。", nameof(json), ex);
        }

        if (envelope["messageType"] is null || string.IsNullOrWhiteSpace(envelope.Value<string>("messageType")))
            throw new ArgumentException("messageType は必須です。", nameof(json));

        envelope["apiName"] ??= ApiName;
        envelope["apiVersion"] ??= ApiVersion;
        if (envelope["requestID"] is null || string.IsNullOrWhiteSpace(envelope.Value<string>("requestID")))
            envelope["requestID"] = NewRequestId();

        return envelope;
    }

    public static bool IsAuthenticationSuccess(JObject response)
    {
        if (!string.Equals(response.Value<string>("messageType"), "AuthenticationResponse", StringComparison.Ordinal))
            return false;

        return response["data"]?.Value<bool?>("authenticated") == true;
    }

    public static bool ShouldRequestNewToken(JObject response, bool hadToken)
    {
        var messageType = response.Value<string>("messageType");
        if (string.Equals(messageType, "AuthenticationResponse", StringComparison.Ordinal)
            && response["data"]?.Value<bool?>("authenticated") == false)
        {
            return hadToken;
        }

        if (string.Equals(messageType, "APIError", StringComparison.Ordinal))
        {
            var errorId = response["data"]?.Value<int?>("errorID");
            return errorId is 50 or 51;
        }

        return false;
    }

    public static string NewRequestId() => Guid.NewGuid().ToString("N")[..32];
}
