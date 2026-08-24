namespace JTSA.Models
{
    /// <summary>配信・ユーザー単位のチャット発言統計。</summary>
    public class T_StreamChatUserCount
    {
        public required string StreamId { get; set; }
        public required string UserId { get; set; }
        public required string LoginId { get; set; }
        public required string DisplayName { get; set; }
        public int ChatCount { get; set; }
        public DateTime FirstChatDateTime { get; set; }
        public DateTime LastChatDateTime { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime UpdatedDateTime { get; set; }
    }
}
