using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JTSA.Utility
{
    static class SteamHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="appId"></param>
        /// <returns></returns>
        public static async Task<string?> GetSteamHeaderImageUrlAsync(string appId)
        {
            var apiUrl =
                $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=JP&l=japanese";

            HttpClient httpClient = new();
            var json = await httpClient.GetStringAsync(apiUrl);

            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement.GetProperty(appId);

            if (!root.GetProperty("success").GetBoolean())
                return null;

            var data = root.GetProperty("data");

            if (data.TryGetProperty("header_image", out var headerImage))
                return headerImage.GetString();

            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string? GetSteamAppId(string url)
        {
            var match = Regex.Match(url, @"store\.steampowered\.com/app/(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
