using JTSA.Plugin.Abstractions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JTSA.MultiPlatformPlugin;

internal sealed class YouTubeService(HttpClient httpClient)
{
    private const string ApiRoot = "https://www.googleapis.com/youtube/v3";

    public async Task<string> UpdateMetadataAsync(
        string accessToken, string broadcastId, string title, string categoryId, CancellationToken cancellationToken)
    {
        Require(accessToken, "YouTubeアクセストークン");
        Require(title, "タイトル");
        broadcastId = string.IsNullOrWhiteSpace(broadcastId)
            ? await FindBroadcastIdAsync(accessToken, cancellationToken)
            : broadcastId.Trim();

        using var get = CreateRequest(HttpMethod.Get,
            $"{ApiRoot}/liveBroadcasts?part=snippet&id={Uri.EscapeDataString(broadcastId)}", accessToken);
        using var getResponse = await httpClient.SendAsync(get, cancellationToken);
        var root = await ReadSuccessAsync(getResponse, cancellationToken);
        var item = root["items"]?.AsArray().FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("指定したYouTube配信が見つかりません。");
        var snippet = item["snippet"]?.DeepClone().AsObject()
            ?? throw new InvalidOperationException("YouTube配信情報を取得できませんでした。");
        snippet["title"] = title.Trim();
        if (!string.IsNullOrWhiteSpace(categoryId)) snippet["categoryId"] = categoryId.Trim();

        var body = new JsonObject { ["id"] = broadcastId, ["snippet"] = snippet };
        using var put = CreateRequest(HttpMethod.Put, $"{ApiRoot}/liveBroadcasts?part=snippet", accessToken);
        put.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var putResponse = await httpClient.SendAsync(put, cancellationToken);
        await ReadSuccessAsync(putResponse, cancellationToken);
        return broadcastId;
    }

    public async Task PollCommentsAsync(
        string accessToken,
        string liveChatId,
        Action<ExternalChatMessageInfo> onMessage,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        Require(accessToken, "YouTubeアクセストークン");
        liveChatId = string.IsNullOrWhiteSpace(liveChatId)
            ? await FindLiveChatIdAsync(accessToken, cancellationToken)
            : liveChatId.Trim();
        onStatus($"YouTubeコメント接続中（Live Chat: {liveChatId}）");
        string? pageToken = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            var url = $"{ApiRoot}/liveChat/messages?part=snippet,authorDetails&liveChatId={Uri.EscapeDataString(liveChatId)}";
            if (!string.IsNullOrEmpty(pageToken)) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            using var request = CreateRequest(HttpMethod.Get, url, accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var root = await ReadSuccessAsync(response, cancellationToken);
            foreach (var node in root["items"]?.AsArray() ?? [])
            {
                var item = node?.AsObject();
                var id = item?["id"]?.GetValue<string>() ?? string.Empty;
                if (id.Length == 0 || !seen.Add(id)) continue;
                var snippet = item?["snippet"]?.AsObject();
                var author = item?["authorDetails"]?.AsObject();
                var message = snippet?["displayMessage"]?.GetValue<string>() ?? string.Empty;
                if (message.Length == 0) continue;
                onMessage(new ExternalChatMessageInfo(
                    "YouTube", id,
                    author?["channelId"]?.GetValue<string>() ?? string.Empty,
                    author?["displayName"]?.GetValue<string>() ?? string.Empty,
                    author?["displayName"]?.GetValue<string>() ?? "YouTube",
                    message,
                    author?["profileImageUrl"]?.GetValue<string>() ?? string.Empty,
                    "#FF5A5A"));
            }
            pageToken = root["nextPageToken"]?.GetValue<string>();
            var delay = root["pollingIntervalMillis"]?.GetValue<int>() ?? 5000;
            await Task.Delay(Math.Max(1000, delay), cancellationToken);
        }
    }

    private async Task<string> FindBroadcastIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"{ApiRoot}/liveBroadcasts?part=id&broadcastStatus=active&mine=true", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var root = await ReadSuccessAsync(response, cancellationToken);
        return root["items"]?.AsArray().FirstOrDefault()?["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("配信中のYouTubeライブが見つかりません。配信IDを指定してください。");
    }

    private async Task<string> FindLiveChatIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"{ApiRoot}/liveBroadcasts?part=snippet&broadcastStatus=active&mine=true", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var root = await ReadSuccessAsync(response, cancellationToken);
        return root["items"]?.AsArray().FirstOrDefault()?["snippet"]?["liveChatId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("配信中のYouTubeライブチャットが見つかりません。Live Chat IDを指定してください。");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        return request;
    }

    private static async Task<JsonObject> ReadSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"YouTube APIエラー ({(int)response.StatusCode}): {text}");
        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{name}を入力してください。");
    }
}
