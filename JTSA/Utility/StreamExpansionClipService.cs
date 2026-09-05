using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JTSA.Utility;

internal static class StreamExpansionClipService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    // Twitch Webで公開クリップの再生に使われているpersisted query。
    // 非公開APIのため、Twitch側の変更時は更新が必要になる。
    private const string GqlClientId = "ue6666qo983tsx6so1t0vnawi233wa";
    private const string ShareClipRenderStatusHash =
        "2db6a3b20eabf510bd3cf465ae2408834b59eb6b8af89ca73ab1486cacecfb63";

    internal static string NormalizeLogin(string login)
    {
        login = login.Trim().TrimStart('@');
        if (!Regex.IsMatch(login, "^[a-zA-Z0-9_]{1,25}$"))
            throw new ArgumentException("クリップ対象にはTwitchのログイン名（英数字・_）を指定してください。");
        return login.ToLowerInvariant();
    }

    public static async Task PlayAsync(string login, string accessToken)
    {
        login = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Twitchアカウントを選択してログインしてください。");

        using var users = await GetAsync("users?login=" + Uri.EscapeDataString(login), accessToken);
        var userData = users.RootElement.GetProperty("data");
        if (userData.GetArrayLength() == 0)
            throw new InvalidOperationException($"Twitchユーザーが見つかりません：{login}");
        var broadcasterId = userData[0].GetProperty("id").GetString()!;
        using var clips = await GetAsync("clips?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) + "&first=100", accessToken);
        var candidates = clips.RootElement.GetProperty("data").EnumerateArray()
            .Where(clip => !string.IsNullOrWhiteSpace(clip.GetProperty("id").GetString())
                && clip.GetProperty("duration").GetDouble() > 0).ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException($"再生できるクリップがありません：{login}");
        var selected = candidates[Random.Shared.Next(candidates.Length)];
        var slug = selected.GetProperty("id").GetString()!;
        using var playback = await GetClipPlaybackAsync(slug);
        var videoUrl = ResolveVideoUrl(playback.RootElement);
        await StreamExpansionClipAudioPlayer.PrepareAsync(videoUrl);
        StreamExpansionClipOverlay.ShowClip(videoUrl, selected.GetProperty("duration").GetDouble());
    }

    private static async Task<JsonDocument> GetClipPlaybackAsync(string slug)
    {
        var payload = new[]
        {
            new
            {
                operationName = "ShareClipRenderStatus",
                variables = new { slug },
                extensions = new
                {
                    persistedQuery = new { version = 1, sha256Hash = ShareClipRenderStatusHash }
                }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gql.twitch.tv/gql")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("Client-ID", GqlClientId);
        using var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Twitchクリップの再生情報を取得できませんでした（HTTP {(int)response.StatusCode}）。");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    internal static string ResolveVideoUrl(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Array || response.GetArrayLength() == 0
            || !response[0].TryGetProperty("data", out var data)
            || !data.TryGetProperty("clip", out var clip)
            || clip.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new InvalidOperationException("このクリップは再生できないか、公開が終了しています。");

        if (!clip.TryGetProperty("playbackAccessToken", out var token)
            || !token.TryGetProperty("signature", out var signatureElement)
            || !token.TryGetProperty("value", out var valueElement))
            throw new InvalidOperationException("Twitchクリップの再生トークンを取得できませんでした。");

        var signature = signatureElement.GetString();
        var tokenValue = valueElement.GetString();
        var candidates = new List<(string Url, int Height, bool Landscape)>();
        if (clip.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var landscape = !asset.TryGetProperty("aspectRatio", out var ratio)
                    || !ratio.TryGetDouble(out var aspectRatio) || aspectRatio >= 1;
                if (!asset.TryGetProperty("videoQualities", out var qualities)
                    || qualities.ValueKind != JsonValueKind.Array) continue;
                foreach (var quality in qualities.EnumerateArray())
                {
                    var sourceUrl = quality.TryGetProperty("sourceURL", out var source)
                        ? source.GetString() : null;
                    if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)
                        || uri.Scheme != Uri.UriSchemeHttps) continue;
                    var height = quality.TryGetProperty("quality", out var heightElement)
                        && int.TryParse(heightElement.ToString(), out var parsedHeight) ? parsedHeight : 0;
                    candidates.Add((uri.AbsoluteUri, height, landscape));
                }
            }
        }

        var selected = candidates.OrderByDescending(x => x.Landscape).ThenByDescending(x => x.Height).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(selected.Url) || string.IsNullOrWhiteSpace(signature)
            || string.IsNullOrWhiteSpace(tokenValue))
            throw new InvalidOperationException("Twitchクリップの動画URLを取得できませんでした。");

        var separator = selected.Url.Contains('?') ? '&' : '?';
        return selected.Url + separator + "sig=" + Uri.EscapeDataString(signature)
            + "&token=" + Uri.EscapeDataString(tokenValue);
    }

    private static async Task<JsonDocument> GetAsync(string path, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", TwitchHelper.ClientID);
        using var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Twitchクリップ取得失敗（HTTP {(int)response.StatusCode}）。ログイン状態を確認してください。");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
