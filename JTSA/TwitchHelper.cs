using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace JTSA
{
    public class SearchCategories
    {
        public required String Id { get; set; }
        public required String Name { get; set; }
        public required String BoxArtUrl { get; set; }
    }

    public class ModifyChannelInformation
    {
        public required String game_id { get; set; }
        public required String broadcaster_language { get; set; }
        public required String title { get; set; }
        public required int delay { get; set; }
    }

    public class Games
    {
        public required String Id { get; set; }
        public required String Name { get; set; }
        public required String BoxArtUrl { get; set; }
        public required String IGDBId { get; set; }
    }

    static class TwitchHelper
    {
        /// <summary>
        /// カテゴリの取得
        /// 
        /// API：Modify Channel Information
        /// https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="broadcasterId"></param>
        /// <returns></returns>
        public static async Task<string?> GetTwitchCategoryByBroadcast(String broadcasterId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppConfig.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", AppConfig.ClientID);

            var response = await client.GetAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={Utility.broadcasterId}");
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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppConfig.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", AppConfig.ClientID);

            var response = await client.GetAsync($"https://api.twitch.tv/helix/games?id={gameId}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(json);
            var data2 = doc2.RootElement.GetProperty("data");
            if (data2.GetArrayLength() == 0) return null;

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
        public static async Task<bool> SetCategoryAsync(string broadcasterId, string accessToken, string clientId, string gameId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Add("Client-Id", clientId);

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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppConfig.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", AppConfig.ClientID);

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
    }
}
