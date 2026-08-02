using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

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
        public static string UserName { get; set; } = "";
        public static string RedirectUri = "http://localhost:8080/";


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
    }
}
