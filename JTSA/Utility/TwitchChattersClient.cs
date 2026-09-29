using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JTSA.TwitchIF;

namespace JTSA.Utility;

internal sealed class TwitchChattersClient(HttpClient httpClient)
{
    public async Task<TwitchApiResult<List<TwitchChatterIF>>> GetAsync(
        string broadcasterId, string accessToken, string clientId)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId) || string.IsNullOrWhiteSpace(accessToken))
            return TwitchApiResult<List<TwitchChatterIF>>.Failure(
                TwitchApiErrorKind.NotConfigured, "配信者IDまたはアクセストークンがありません。");

        var chatters = new List<TwitchChatterIF>();
        string? cursor = null;
        try
        {
            do
            {
                var url = "https://api.twitch.tv/helix/chat/chatters" +
                    $"?broadcaster_id={Uri.EscapeDataString(broadcasterId)}" +
                    $"&moderator_id={Uri.EscapeDataString(broadcasterId)}&first=1000" +
                    (cursor is null ? string.Empty : $"&after={Uri.EscapeDataString(cursor)}");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("Client-Id", clientId);
                using var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var kind = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => TwitchApiErrorKind.Unauthorized,
                        HttpStatusCode.TooManyRequests => TwitchApiErrorKind.RateLimited,
                        _ => TwitchApiErrorKind.Unknown
                    };
                    return TwitchApiResult<List<TwitchChatterIF>>.Failure(
                        kind, $"チャット接続者の取得に失敗しました ({(int)response.StatusCode})。");
                }

                var page = await response.Content.ReadFromJsonAsync<ChattersResponse>();
                if (page?.Data is null)
                    return TwitchApiResult<List<TwitchChatterIF>>.Failure(
                        TwitchApiErrorKind.Unknown, "チャット接続者の応答が不正です。");

                chatters.AddRange(page.Data.Select(user => new TwitchChatterIF
                {
                    UserId = user.UserId,
                    UserLogin = user.UserLogin,
                    UserName = user.UserName
                }));
                cursor = page.Pagination?.Cursor;
            } while (!string.IsNullOrWhiteSpace(cursor));

            return TwitchApiResult<List<TwitchChatterIF>>.Success(chatters);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return TwitchApiResult<List<TwitchChatterIF>>.Failure(
                TwitchApiErrorKind.Unknown, $"チャット接続者の取得に失敗しました：{ex.Message}");
        }
    }

    private sealed class ChattersResponse
    {
        [JsonPropertyName("data")]
        public List<Chatter>? Data { get; set; }

        [JsonPropertyName("pagination")]
        public Pagination? Pagination { get; set; }
    }

    private sealed class Chatter
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; } = string.Empty;

        [JsonPropertyName("user_name")]
        public string UserName { get; set; } = string.Empty;
    }

    private sealed class Pagination
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }
}
