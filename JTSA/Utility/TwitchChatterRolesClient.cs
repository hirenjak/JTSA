using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace JTSA.Utility;

internal sealed class TwitchChatterRolesClient(HttpClient httpClient)
{
    public async Task<TwitchApiResult<ChatterRoles>> GetAsync(
        string broadcasterId, string accessToken, string clientId)
    {
        var moderators = await GetRoleIdsAsync(
            "moderation/moderators", broadcasterId, accessToken, clientId);
        if (!moderators.IsSuccess || moderators.Data is null)
            return TwitchApiResult<ChatterRoles>.Failure(moderators.ErrorKind, moderators.ErrorMessage);

        var vips = await GetRoleIdsAsync(
            "channels/vips", broadcasterId, accessToken, clientId);
        if (!vips.IsSuccess || vips.Data is null)
            return TwitchApiResult<ChatterRoles>.Failure(vips.ErrorKind, vips.ErrorMessage);

        return TwitchApiResult<ChatterRoles>.Success(new ChatterRoles(moderators.Data, vips.Data));
    }

    private async Task<TwitchApiResult<HashSet<string>>> GetRoleIdsAsync(
        string endpoint, string broadcasterId, string accessToken, string clientId)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        try
        {
            do
            {
                var url = $"https://api.twitch.tv/helix/{endpoint}" +
                    $"?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&first=100" +
                    (cursor is null ? string.Empty : $"&after={Uri.EscapeDataString(cursor)}");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("Client-Id", clientId);
                using var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var kind = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TwitchApiErrorKind.Unauthorized,
                        HttpStatusCode.TooManyRequests => TwitchApiErrorKind.RateLimited,
                        _ => TwitchApiErrorKind.Unknown
                    };
                    return TwitchApiResult<HashSet<string>>.Failure(
                        kind, $"チャットユーザーの役割取得に失敗しました ({(int)response.StatusCode})。");
                }

                var page = await response.Content.ReadFromJsonAsync<RoleResponse>();
                if (page?.Data is null)
                    return TwitchApiResult<HashSet<string>>.Failure(
                        TwitchApiErrorKind.Unknown, "チャットユーザーの役割応答が不正です。");

                foreach (var user in page.Data)
                    if (!string.IsNullOrWhiteSpace(user.UserId)) ids.Add(user.UserId);
                cursor = page.Pagination?.Cursor;
            } while (!string.IsNullOrWhiteSpace(cursor));

            return TwitchApiResult<HashSet<string>>.Success(ids);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return TwitchApiResult<HashSet<string>>.Failure(
                TwitchApiErrorKind.Unknown, $"チャットユーザーの役割取得に失敗しました：{ex.Message}");
        }
    }

    private sealed class RoleResponse
    {
        [JsonPropertyName("data")]
        public List<RoleUser>? Data { get; set; }

        [JsonPropertyName("pagination")]
        public Pagination? Pagination { get; set; }
    }

    private sealed class RoleUser
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
    }

    private sealed class Pagination
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }
}

internal sealed record ChatterRoles(
    IReadOnlySet<string> ModeratorIds,
    IReadOnlySet<string> VipIds);
