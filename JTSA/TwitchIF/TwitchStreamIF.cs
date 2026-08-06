using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.TwitchIF
{
    class TwitchStreamIF
    {
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserLogin { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.MinValue;
        public string ThumbnailUrl { get; set; } = string.Empty;

    }
}
