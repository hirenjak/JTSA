using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTSA.Utility
{
    public class UserInformation()
    {
        public required string BroadcastId { get; set; }
        public required string UserId { get; set; }
        public required string DisplayName { get; set; }
    }

    static class JTSAHelper
    {
        public static string LoginName { get; set; } = "";
        public static string RedirectUri = "http://localhost:8080/";


        public static string BitmapToBase64(BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var ms = new MemoryStream();
            encoder.Save(ms);

            return Convert.ToBase64String(ms.ToArray());
        }

        public static BitmapImage Base64ToBitmap(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);

            using var ms = new MemoryStream(bytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private static readonly HttpClient HttpClient = new();

        public static async Task<BitmapImage> LoadBitmapAsync(string url)
        {
            byte[] imageData = await HttpClient.GetByteArrayAsync(url);

            using var stream = new MemoryStream(imageData);

            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze(); // 別スレッドでも使える

            return bitmap;
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

        /// <summary>
        /// 現在の日時をUNIXタイムスタンプ形式（1970年1月1日からの経過ミリ秒）として取得
        /// </summary>
        /// <returns>
        /// UNIXエポック（1970年1月1日 00:00:00 UTC）から現在までの経過時間をミリ秒単位で表す長整数値
        /// </returns>
        public static long GetCurrentUnixTimestampMillis()
        {
            // 現在の日時を取得
            DateTime now = DateTime.UtcNow;

            // UNIX時間の基準点 (1970年1月1日 00:00:00 UTC)
            DateTime unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // 経過ミリ秒を計算
            return (long)(now - unixEpoch).TotalMilliseconds;
        }


        /// <summary>
        /// 自配信を開く
        /// </summary>
        /// <param name="BroadcasterLogin"></param>
        public static void OpenMyTwitchChannel()
        {
            OpenTwitchChannel(LoginName);
        }

        /// <summary>
        /// 指定したユーザーのTwitchチャンネルを既定のブラウザで開く
        /// </summary>
        public static void OpenTwitchChannel(string loginName)
        {
            if (string.IsNullOrWhiteSpace(loginName)) return;

            var url = $"https://www.twitch.tv/{Uri.EscapeDataString(loginName.Trim())}";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}
