using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Forms.Twitch
{
    public class DeviceCodeResponseIF
    {
        public required string device_code { get; set; }
        public required string user_code { get; set; }
        public required string verification_uri { get; set; }
        public int expires_in { get; set; }
        public int interval { get; set; }
        public string verification_uri_complete { get; set; }
    }
}
