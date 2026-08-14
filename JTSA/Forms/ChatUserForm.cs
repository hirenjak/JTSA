namespace JTSA.Forms
{
    /// <summary>チャットパネルの参加ユーザー一覧用表示データ。</summary>
    public class ChatUserForm
    {
        public required string UserId { get; set; }
        public required string UserName { get; set; }
        public required string DisplayName { get; set; }
        public required string ProfileImageUrl { get; set; }
        public required DateTime LastChatDateTime { get; set; }
        public int MessageCount { get; set; }
    }
}
