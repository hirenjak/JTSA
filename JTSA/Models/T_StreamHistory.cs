using System.ComponentModel.DataAnnotations;

namespace JTSA.Models
{
    /// <summary>Twitch配信とアーカイブから収集した配信履歴。</summary>
    public class T_StreamHistory
    {
        [Key]
        public string StreamId { get; set; } = string.Empty;
        public string BroadcasterId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string ArchiveVideoId { get; set; } = string.Empty;
        public string ArchiveUrl { get; set; } = string.Empty;
        public DateTime CreatedDateTime { get; set; }
        public DateTime UpdatedDateTime { get; set; }
    }
}
