using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JTSA.Utility
{
    public static class IgdbService
    {
        private static HttpClient httpClient;
        private static string clientId;
        private static string accessToken;
        private static long? japaneseRegionId;

        public static void Initialize(HttpClient _httpClient, string _clientID, string _accessToken)
        {
            httpClient = _httpClient;
            clientId = _clientID;
            accessToken = _accessToken;
        }

        public sealed class TwitchGameResponse
        {
            [JsonPropertyName("data")]
            public List<TwitchGameData> Data { get; set; } = [];
        }

        public sealed class TwitchGameData
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("igdb_id")]
            public string IgdbId { get; set; } = string.Empty;
        }

        public sealed class IgdbExternalGame
        {
            [JsonPropertyName("game")]
            public long Game { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            /// <summary>
            /// Steam App ID
            /// </summary>
            [JsonPropertyName("uid")]
            public string? Uid { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("external_game_source")]
            public long ExternalGameSource { get; set; }
        }

        private sealed class IgdbRegion
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("identifier")]
            public string Identifier { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private sealed class IgdbGameLocalization
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>Twitchカテゴリに対応するIGDBの日本向けタイトルを取得する。</summary>
        public static async Task<string?> GetJapaneseGameNameAsync(string twitchCategoryId)
        {
            try
            {
                var igdbId = await GetIgdbIdAsync(twitchCategoryId);
                if (string.IsNullOrWhiteSpace(igdbId)) return null;

                japaneseRegionId ??= await GetJapaneseRegionIdAsync();
                if (japaneseRegionId is null) return null;

                using var request = CreateIgdbRequest("game_localizations", $"""
                    fields name;
                    where game = {igdbId} & region = {japaneseRegionId.Value};
                    limit 1;
                    """);
                using var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var localizations = JsonSerializer.Deserialize<List<IgdbGameLocalization>>(
                    await response.Content.ReadAsStringAsync()) ?? [];
                return localizations.FirstOrDefault()?.Name?.Trim();
            }
            catch
            {
                // 日本語名は補助情報なので、取得失敗時は呼び出し元でTwitch名へフォールバックする。
                return null;
            }
        }

        private static async Task<long?> GetJapaneseRegionIdAsync()
        {
            using var request = CreateIgdbRequest("regions", "fields id,identifier,name; limit 500;");
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var regions = JsonSerializer.Deserialize<List<IgdbRegion>>(
                await response.Content.ReadAsStringAsync()) ?? [];
            return regions.FirstOrDefault(region =>
                region.Identifier.Equals("ja-JP", StringComparison.OrdinalIgnoreCase) ||
                region.Identifier.Equals("JP", StringComparison.OrdinalIgnoreCase) ||
                region.Name.Contains("Japan", StringComparison.OrdinalIgnoreCase))?.Id;
        }

        private static HttpRequestMessage CreateIgdbRequest(string endpoint, string query)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint}");
            request.Headers.Add("Client-ID", clientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(query, Encoding.UTF8, "text/plain");
            return request;
        }

        /// <summary>
        /// TwitchカテゴリIDからSteam URLを取得
        /// </summary>
        public static async Task<List<string>> GetSteamUrlsAsync(string twitchCategoryId)
        {
            var igdbId = await GetIgdbIdAsync(twitchCategoryId);

            if (string.IsNullOrWhiteSpace(igdbId))
            {
                return [];
            }

            return await GetSteamUrlsFromIgdbAsync(igdbId);
        }


        /// <summary>
        /// TwitchカテゴリIDからIGDB IDを取得
        /// </summary>
        private static async Task<string?> GetIgdbIdAsync(string twitchCategoryId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/games?id={Uri.EscapeDataString(twitchCategoryId)}");

            request.Headers.Add("Client-Id", clientId);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<TwitchGameResponse>(json);

            return result?.Data.FirstOrDefault()?.IgdbId;
        }

        /// <summary>
        /// IGDB IDからSteam URLを取得
        /// </summary>
        private static async Task<List<string>> GetSteamUrlsFromIgdbAsync(string igdbId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.igdb.com/v4/external_games");

            request.Headers.Add("Client-ID", clientId);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            // external_game_source = 1 はSteam
            var query = $"""
            fields name,uid,url,game,external_game_source;
            where game = {igdbId}
                & external_game_source = 1;
            limit 50;
            """;

            request.Content = new StringContent(
                query,
                Encoding.UTF8,
                "text/plain");

            using var response = await httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            var externalGames =
                JsonSerializer.Deserialize<List<IgdbExternalGame>>(json) ?? [];

            return externalGames
                .Select(x =>
                {
                    // IGDB側にURLが登録されていればそれを優先
                    if (!string.IsNullOrWhiteSpace(x.Url))
                    {
                        return x.Url;
                    }

                    // uidはSteam App ID
                    if (!string.IsNullOrWhiteSpace(x.Uid))
                    {
                        return $"https://store.steampowered.com/app/{x.Uid}";
                    }

                    return null;
                })
                .Where(x => x is not null)
                .Select(x => x!)
                .Distinct()
                .ToList();
        }
    }
}
