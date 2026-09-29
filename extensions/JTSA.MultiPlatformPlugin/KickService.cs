using JTSA.Plugin.Abstractions;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace JTSA.MultiPlatformPlugin;

internal sealed class KickService(HttpClient httpClient)
{
    public async Task<IReadOnlyList<PlatformCategory>> SearchCategoriesAsync(
        string accessToken, string searchText, CancellationToken cancellationToken)
    {
        Require(accessToken, "Kickアクセストークン");
        if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim().Length < 3)
            throw new InvalidOperationException("Kickカテゴリは3文字以上で検索してください。");

        var url = "https://api.kick.com/public/v2/categories?limit=100&name=" +
                  Uri.EscapeDataString(searchText.Trim());
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Kickカテゴリ取得エラー ({(int)response.StatusCode}): {text}");
        var root = JsonNode.Parse(text)?.AsObject();
        return (root?["data"]?.AsArray() ?? [])
            .Select(node => new PlatformCategory(
                node?["id"]?.ToString() ?? string.Empty,
                node?["name"]?.GetValue<string>() ?? string.Empty))
            .Where(item => item.Id.Length > 0 && item.Name.Length > 0)
            .ToArray();
    }

    public async Task UpdateMetadataAsync(
        string accessToken, string title, string categoryId, CancellationToken cancellationToken)
    {
        Require(accessToken, "Kickアクセストークン");
        Require(title, "タイトル");
        if (!long.TryParse(categoryId, out var parsedCategory) || parsedCategory < 1)
            throw new InvalidOperationException("KickカテゴリIDは1以上の数値で入力してください。");

        var body = new JsonObject { ["stream_title"] = title.Trim(), ["category_id"] = parsedCategory };
        using var request = new HttpRequestMessage(HttpMethod.Patch, "https://api.kick.com/public/v1/channels")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Kick APIエラー ({(int)response.StatusCode}): {text}");
    }

    public async Task ReceiveCommentsAsync(
        string channelSlug,
        Action<ExternalChatMessageInfo> onMessage,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        Require(channelSlug, "Kickチャンネル名");
        var chatroomId = await GetChatroomIdAsync(channelSlug.Trim(), cancellationToken);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new Uri("wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679?protocol=7&client=JTSA&version=1.0&flash=false"),
            cancellationToken);
        await SendAsync(socket, new JsonObject
        {
            ["event"] = "pusher:subscribe",
            ["data"] = new JsonObject { ["auth"] = string.Empty, ["channel"] = $"chatrooms.{chatroomId}.v2" }
        }.ToJsonString(), cancellationToken);
        onStatus($"Kickコメント接続中（{channelSlug}）");

        var buffer = new byte[64 * 1024];
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var text = await ReceiveTextAsync(socket, buffer, cancellationToken);
            if (text is null) break;
            var envelope = JsonNode.Parse(text)?.AsObject();
            var eventName = envelope?["event"]?.GetValue<string>() ?? string.Empty;
            if (eventName == "pusher:ping")
            {
                await SendAsync(socket, "{\"event\":\"pusher:pong\",\"data\":{}}", cancellationToken);
                continue;
            }
            if (!eventName.EndsWith("ChatMessageEvent", StringComparison.OrdinalIgnoreCase)) continue;
            var dataText = envelope?["data"]?.GetValue<string>();
            var data = string.IsNullOrWhiteSpace(dataText) ? null : JsonNode.Parse(dataText)?.AsObject();
            var sender = data?["sender"]?.AsObject();
            var message = data?["content"]?.GetValue<string>() ?? string.Empty;
            if (message.Length == 0) continue;
            onMessage(new ExternalChatMessageInfo(
                "Kick",
                data?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                sender?["id"]?.ToString() ?? string.Empty,
                sender?["slug"]?.GetValue<string>() ?? string.Empty,
                sender?["username"]?.GetValue<string>() ?? "Kick",
                message,
                string.Empty,
                sender?["identity"]?["color"]?.GetValue<string>() ?? "#53FC18"));
        }
    }

    private async Task<long> GetChatroomIdAsync(string slug, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"https://kick.com/api/v2/channels/{Uri.EscapeDataString(slug)}", cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Kickチャンネル取得エラー ({(int)response.StatusCode}): {text}");
        return JsonNode.Parse(text)?["chatroom"]?["id"]?.GetValue<long>()
            ?? throw new InvalidOperationException("KickのチャットルームIDを取得できませんでした。");
    }

    private static async Task SendAsync(ClientWebSocket socket, string text, CancellationToken cancellationToken) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellationToken);

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{name}を入力してください。");
    }
}
