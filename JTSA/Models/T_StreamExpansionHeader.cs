using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Models
{
    internal class T_StreamExpansionHeader : DBBaseTransaction
    {
        [Key]
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public bool IsRaid { get; set; }

        public bool IsSubscribe { get; set; }

        public bool IsBits { get; set; }

        public bool IsFirstChat { get; set; }

        public bool IsFollow { get; set; }
        public bool IsHourly { get; set; }
        public bool IsAdStart { get; set; }
        public bool IsAdEnd { get; set; }
        public bool IsAdUpcoming { get; set; }
        public int AdAdvanceMinutes { get; set; } = 1;
        public bool IsScheduledTime { get; set; }
        public int ScheduledHour { get; set; }
        public int ScheduledMinute { get; set; }

        public bool IsObsStreamStart { get; set; }

        public bool IsObsStreamStartMain { get; set; }

        public bool IsObsStreamStartSub { get; set; }

        public bool DoShoutout { get; set; }

        public int DelaySeconds { get; set; }

        public string TriggerComment { get; set; } = string.Empty;

        public bool ChatPermissionEveryone { get; set; }

        public bool ChatPermissionModerator { get; set; }

        public bool ChatPermissionVip { get; set; }

        public bool ChatPermissionSubscriber { get; set; }

        public string TriggerChannelPointId { get; set; } = string.Empty;
    }
}
