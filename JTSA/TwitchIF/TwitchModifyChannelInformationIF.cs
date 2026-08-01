using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Forms.Twitch
{
    class TwitchModifyChannelInformationIF
    {
        public required string gameId { get; set; }
        public required string broadcasterLanguage { get; set; }
        public required string title { get; set; }
        public required int delay { get; set; }
    }
}
