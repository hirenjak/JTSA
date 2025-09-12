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
