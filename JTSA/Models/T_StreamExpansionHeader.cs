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

        public bool DoShoutout { get; set; }

        public int DelaySeconds { get; set; }

        public string TriggerComment { get; set; } = string.Empty;

        public string TriggerChannelPointId { get; set; } = string.Empty;
    }
}
