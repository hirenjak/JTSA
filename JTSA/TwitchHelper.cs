using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JTSA
{

    static class TwitchHelper
    {
        public static string ClientID { get; } = "tbpy1q9lh9pkyrqhde6o4f4dkq9rj0";

        public static string RedirectUri = @"http://localhost:8080/";
        public static string AccessToken = "";
        public static string BroadcasterId = "";


        public class SearchCategories
        {
            public required string Id { get; set; }
            public required string Name { get; set; }
            public required string BoxArtUrl { get; set; }
        }


        public class ModifyChannelInformation
        {
            public required string game_id { get; set; }
            public required string broadcaster_language { get; set; }
            public required string title { get; set; }
            public required int delay { get; set; }
        }


        public class Games
        {
            public required string Id { get; set; }
            public required string Name { get; set; }
            public required string BoxArtUrl { get; set; }
            public required string IGDBId { get; set; }
        }


        public class DeviceCodeResponse
        {
            public required string device_code { get; set; }
            public required string user_code { get; set; }
            public required string verification_uri { get; set; }
            public int expires_in { get; set; }
            public int interval { get; set; }
            public required string verification_uri_complete { get; set; }
        }


        public class AccessTokenResponse
        {
            public int expiresIn { get; set; }
            public int interval { get; set; }
            public required string refreshToken { get; set; }
            public required string accessToken { get; set; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static async Task<DeviceCodeResponse> RequestDeviceCodeAsync()
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientID),
                new KeyValuePair<string, string>("scope", "user:edit:broadcast user:read:broadcast")
            });
            var response = await client.PostAsync("https://id.twitch.tv/oauth2/device", content);
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<DeviceCodeResponse>(json) ?? new()
            {
                device_code = "",
                user_code = "",
                verification_uri = "",
                expires_in = 0,
                interval = 0,
                verification_uri_complete = ""
            };
        }


        /// <summary>
        /// 配信者情報取得処理
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public static async Task<UserInformation?> GetBroadcasterIdAsync()
        {
            return await GetBroadcasterIdAsync(Utility.UserName);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceCode"></param>
        /// <param name="interval"></param>
        /// <param name="expiresIn"></param>
        /// <returns></returns>
        public static async Task<AccessTokenResponse> PollDeviceTokenAsync(string deviceCode, int interval, int expiresIn)
        {
            using var client = new HttpClient();
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < expiresIn)
            {
                await Task.Delay(interval * 1000);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientID),
                    new KeyValuePair<string, string>("device_code", deviceCode),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                });
                var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("access_token", out var tokenElem))
                {
                    AccessTokenResponse elem = new()
                    {
                        accessToken = tokenElem.GetString() ?? "",
                        refreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? "",
                        expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32()
                    };

                    return elem;
                }
                else if (doc.RootElement.TryGetProperty("error", out var errorElem))
                {
                    var error = errorElem.GetString();
                    if (error == "authorization_pending" || error == "slow_down")
                    {
                        // 認証待ち or ポーリング間隔を守る
                        continue; 
                    }
                    else
                    {
                        // その他のエラー
                        break; 
                    }
                }
            }
            return new() {
                accessToken = "",
                refreshToken = "",
                expiresIn = 0
            };
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        public static async Task<AccessTokenResponse> RefreshAccessTokenAsync(string refreshToken)
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientID),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            });

            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);
            if (!response.IsSuccessStatusCode) return new()
            {
                accessToken = "",
                refreshToken = "",
                expiresIn = 0
            };

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("access_token", out var tokenElem))
            {
                return new AccessTokenResponse
                {
                    accessToken = tokenElem.GetString() ?? "",
                    refreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? "",
                    expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32()
                };
            }
            return new()
            {
                accessToken = "",
                refreshToken = "",
                expiresIn = 0
            };
        }


        /// <summary>
        /// 配信者情報取得処理
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public static async Task<UserInformation?> GetBroadcasterIdAsync(string userName)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", TwitchHelper.ClientID);

            var response = await client.GetAsync($"https://api.twitch.tv/helix/users?login={userName}");

            // レスポンスの処理
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;

            var broadcastId = data[0].GetProperty("id").GetString();
            if (broadcastId == null) return null;

            var userId = data[0].GetProperty("login").GetString();
            if (userId == null) return null;

            var displayName = data[0].GetProperty("display_name").GetString();
            if (displayName == null) return null;


            return new UserInformation()
            {
                BroadcastId = broadcastId,
                UserId = userId,
                DisplayName = displayName
            };
        }


        /// <summary>
        /// カテゴリの取得
        /// 
        /// API：Modify Channel Information
        /// https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="broadcasterId"></param>
        /// <returns></returns>
        public static Task<String?> GetTwitchCategoryByBroadcast()
        {
            return GetTwitchCategoryByBroadcast(BroadcasterId);
        }


        /// <summary>
        /// カテゴリの取得
        /// 
        /// API：Modify Channel Information
        /// https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="broadcasterId"></param>
        /// <returns></returns>
        public static async Task<String?> GetTwitchCategoryByBroadcast(String broadcasterId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var response = await client.GetAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={broadcasterId}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;

            return data[0].GetProperty("game_id").GetString();
        }


        /// <summary>
        /// カテゴリの取得
        /// 
        /// API：Get Games
        /// https://api.twitch.tv/helix/games?id=
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        public static async Task<Games> GetGamesByGameId(String gameId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var response = await client.GetAsync($"https://api.twitch.tv/helix/games?id={gameId}");
            if (!response.IsSuccessStatusCode) return new()
            {
                Id = "",
                Name = "",
                BoxArtUrl = "",
                IGDBId = ""
            };

            var json = await response.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(json);
            var data2 = doc2.RootElement.GetProperty("data");
            if (data2.GetArrayLength() == 0) return new()
            {
                Id = "",
                Name = "",
                BoxArtUrl = "",
                IGDBId = ""
            };

            var result = new Games
            {
                Id = data2[0].GetProperty("id").GetString() ?? "",
                Name = data2[0].GetProperty("name").GetString() ?? "",
                BoxArtUrl = data2[0].GetProperty("box_art_url").GetString() ?? "",
                IGDBId = data2[0].GetProperty("igdb_id").GetString() ?? ""
            };

            return result;
        }


        /// <summary>
        /// 配カテゴリの設定
        /// API：https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="broadcasterId"></param>
        /// <param name="accessToken"></param>
        /// <param name="clientId"></param>
        /// <param name="gameId"></param>
        /// <returns></returns>
        public static async Task<bool> SetCategoryAsync(string broadcasterId, string accessToken, string gameId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var content = new StringContent(
                JsonSerializer.Serialize(new { game_id = gameId }),
                Encoding.UTF8, "application/json");

            var response = await client.PatchAsync(
                $"https://api.twitch.tv/helix/channels?broadcaster_id={broadcasterId}",
                content);

            return response.IsSuccessStatusCode;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public static async Task<List<SearchCategories>> SearchCategoriesByGameNameAsync(string categoryName)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var query = Uri.EscapeDataString(categoryName);
            var response = await client.GetAsync($"https://api.twitch.tv/helix/search/categories?query={query}");
            if (!response.IsSuccessStatusCode) return new List<SearchCategories>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return new List<SearchCategories>();

            var list = new List<SearchCategories>();
            foreach (var item in data.EnumerateArray())
            {
                list.Add(new SearchCategories
                {
                    Id = item.GetProperty("id").GetString() ?? "",
                    Name = item.GetProperty("name").GetString() ?? "",
                    BoxArtUrl = item.GetProperty("box_art_url").GetString() ?? ""
                });
            }
            return list;
        }


        /// <summary>
        /// タイトルの取得
        /// API：https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static async Task<string?> GetTwitchTitle()
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TwitchHelper.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", TwitchHelper.ClientID);

            // TwitchAPIから配信タイトルを取得
            var response = await client.GetAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={TwitchHelper.BroadcasterId}");

            // レスポンスの処理
            if (response.IsSuccessStatusCode)
            { // 200 OK

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var title = doc.RootElement.GetProperty("data")[0].GetProperty("title").GetString();

                return title;
            }

            return null;
        }
    }
}
