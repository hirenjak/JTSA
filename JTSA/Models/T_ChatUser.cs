using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Models
{
    public class T_ChatUser : DBBaseTransaction
    {
        [Key]
        public string UserId { get; set; } = string.Empty;

        public string LoginId { get; set; } = string.Empty;
        
        public string DisplayName { get; set; } = string.Empty;
        
        public bool IsSubscribe { get; set; } = false;
        
        public bool IsRaid { get; set; } = false;
        
        public int TakeBits { get; set; } = 0;
    }
}
