using System.ComponentModel.DataAnnotations;

namespace JTSA.Models
{
    public class M_TwitchAccount : DBBase
    {
        [Key]
        public long Id { get; set; }
        public required string UserName { get; set; }
        public required string BroadcasterId { get; set; }
        public required string RefreshToken { get; set; }
        public bool IsPrimary { get; set; }
    }
}
