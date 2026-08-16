namespace JTSA.Models
{
    /// <summary>日付・チャットユーザー単位の発言数。</summary>
    public class T_DailyChatUserCount
    {
        public DateTime ChatDate { get; set; }
        public required string UserId { get; set; }
        public required string LoginId { get; set; }
        public required string DisplayName { get; set; }
        public int ChatCount { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime UpdatedDateTime { get; set; }
    }
}
