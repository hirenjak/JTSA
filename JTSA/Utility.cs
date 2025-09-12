using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace JTSA
{
    public class UserInformation()
    {
        public required String BroadcastId { get; set; }
        public required String UserId { get; set; }
        public required String DisplayName { get; set; }
    }

    static class Utility
    {
        public static String RedirectUri = "http://localhost:8080/";

        public static String broadcasterId;

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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppConfig.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", AppConfig.ClientID);

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
        /// クリップボードにコピー
        /// </summary>
        /// <param name="targetSentence"></param>
        /// <returns></returns>
        public static bool CopyClipBoad(string targetSentence)
        {
            // TextBlockのテキストをクリップボードにコピー（リトライ付き）
            bool copied = false;
            for (int i = 0; i < 3 && !copied; i++)
            {
                try
                {
                    Clipboard.SetDataObject(targetSentence);
                    return true;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    Thread.Sleep(100); // 少し待ってリトライ
                }
            }

            return false;
        }
    }
}
