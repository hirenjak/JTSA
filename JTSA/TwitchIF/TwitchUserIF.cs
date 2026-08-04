using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.TwitchIF
{
    class TwitchUserIF
    {
        /// <summary>ユーザーID</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>ログイン名（小文字）</summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>表示名</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>ユーザー種別</summary>
        public string UserType { get; set; } = string.Empty;

        /// <summary>配信者種別（partner / affiliate など）</summary>
        public string BroadcasterType { get; set; } = string.Empty;

        /// <summary>自己紹介</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>プロフィール画像URL</summary>
        public string ProfileImageUrl { get; set; } = string.Empty;

        /// <summary>オフライン画像URL</summary>
        public string OfflineImageUrl { get; set; } = string.Empty;

        /// <summary>アカウント作成日時</summary>
        public DateTime CreatedAt { get; set; }
    }
}
