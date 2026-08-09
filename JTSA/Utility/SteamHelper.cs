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
            if (appId == null) return null;

            var apiUrl =
                $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=JP&l=japanese";

            // 通信失敗やレスポンス形式の想定外は「画像なし」として扱う。
            // 呼び出し元はasync voidのイベントハンドラなので、ここで握らないとアプリが落ちる
            try
            {
                HttpClient httpClient = new();
                var json = await httpClient.GetStringAsync(apiUrl);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty(appId, out var root))
                    return null;

                if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
                    return null;

                if (!root.TryGetProperty("data", out var data))
                    return null;

                if (data.TryGetProperty("header_image", out var headerImage))
                    return headerImage.GetString();

                return null;
            }
            catch (Exception)
            {
                return null;
            }
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
