using JTSA.Models;

namespace JTSA.Dao
{
    class DAO_Setting
    {
        public const string DefaultXPostTemplate = "{title}\n配信カテゴリ：{category}\n{url}";

        public enum SettingName : int
        {
            UserName = 1,
            RefreshToken = 2,
            ExpiresIn = 3,
            FriendPrefixWord = 4,
            ChatNotificationVolume = 5,
            JoinChatVolume = 6,
            IsChatOverlay = 7,
            ChatOverlayPosX = 8,
            ChatOverlayPosY = 9,
            /// <summary> チャンネルポイントのコピー時に元のタイトルへ付ける接尾辞 </summary>
            ChannelPointCopySuffix = 10,
            /// <summary>X告知で使用する投稿文テンプレート</summary>
            XPostTemplate = 11,
            ChatOverlayWidth = 12,
            ChatOverlayHeight = 13,
            ChatOverlayShowUserIcon = 14,
            ChatOverlayFontSize = 15,
            AutoStartRegisteredApps = 16,
            BouyomiEnabled = 17,
            BouyomiEndpoint = 18,
            SpeechEngine = 19,
            VoiceVoxEndpoint = 20,
            VoiceVoxSpeakerId = 21,
            /// <summary>ヘッダーで選択中のTwitch送信先アカウントID</summary>
            SelectedTwitchAccountId = 22,
            /// <summary>OBS WebSocket接続URL</summary>
            ObsWebSocketUrl = 23,
            /// <summary>OBS WebSocketパスワード</summary>
            ObsWebSocketPassword = 24,
            /// <summary>起動時にOBSへ自動接続するか</summary>
            ObsAutoConnect = 25,
        }


        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static M_Setting? SelectOneById(SettingName id)
        {
            using var db = new AppDbContext();

            return db.M_Setting.SingleOrDefault(x => x.Name == (int)id);
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool InsertUpdate(SettingName name, string value)
        {
            using var db = new AppDbContext();

            var count = db.M_Setting.Count(x => x.Name == (int)name);

            if (count == 0)
            {
                db.M_Setting.AddRange(new M_Setting { 
                    Name = (int)name,
                    Value = value,
                    UpdatedDateTime = DateTime.Now,
                    CreatedDateTime = DateTime.Now,
                    LastUsedDateTime = DateTime.Now
                });
            }
            else
            {
                var existing = db.M_Setting.Single(x => x.Name == (int)name);
                existing.Value = value;
                existing.UpdatedDateTime = DateTime.Now;
                existing.LastUsedDateTime = DateTime.Now;

                db.M_Setting.Update(existing);
            };

            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }
    }
}
