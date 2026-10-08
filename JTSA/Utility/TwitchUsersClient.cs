using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JTSA.TwitchIF;

namespace JTSA.Utility;

internal sealed class TwitchUsersClient(HttpClient httpClient)
{
    public async Task<TwitchApiResult<List<TwitchUserIF>>> GetByIdsAsync(
        IEnumerable<string> userIds, string accessToken, string clientId)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return TwitchApiResult<List<TwitchUserIF>>.Failure(
                TwitchApiErrorKind.NotConfigured, "アクセストークンがありません。");

        var users = new List<TwitchUserIF>();
        try
        {
            foreach (var ids in userIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Chunk(100))
            {
                var query = string.Join("&", ids.Select(x => $"id={Uri.EscapeDataString(x)}"));
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"https://api.twitch.tv/helix/users?{query}");
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
                    return TwitchApiResult<List<TwitchUserIF>>.Failure(
                        kind, $"ユーザーアイコンの取得に失敗しました ({(int)response.StatusCode})。");
                }

                var page = await response.Content.ReadFromJsonAsync<UsersResponse>();
                if (page?.Data is null)
                    return TwitchApiResult<List<TwitchUserIF>>.Failure(
                        TwitchApiErrorKind.Unknown, "ユーザー情報の応答が不正です。");

                users.AddRange(page.Data.Select(x => new TwitchUserIF
                {
                    UserId = x.Id,
                    Login = x.Login,
                    DisplayName = x.DisplayName,
                    ProfileImageUrl = x.ProfileImageUrl
                }));
            }

            return TwitchApiResult<List<TwitchUserIF>>.Success(users);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return TwitchApiResult<List<TwitchUserIF>>.Failure(
                TwitchApiErrorKind.Unknown, $"ユーザーアイコンの取得に失敗しました：{ex.Message}");
        }
    }

    private sealed class UsersResponse
    {
        [JsonPropertyName("data")]
        public List<User>? Data { get; set; }
    }

    private sealed class User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; set; } = string.Empty;
    }
}
