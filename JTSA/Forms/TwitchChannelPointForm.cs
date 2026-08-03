using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Forms
{
    public class ChannelPointForm
    {
        public string RedemptionId { get; set; } = "";

        public string BroadcasterUserId { get; set; } = "";

        public string UserId { get; set; } = "";

        public string UserLogin { get; set; } = "";

        public string UserName { get; set; } = "";

        public string RewardId { get; set; } = "";

        public string RewardTitle { get; set; } = "";

        public int RewardCost { get; set; }

        public string RewardPrompt { get; set; } = "";

        public string UserInput { get; set; } = "";

        public string Status { get; set; } = "";

        public DateTime RedeemedAt { get; set; }
    }
}