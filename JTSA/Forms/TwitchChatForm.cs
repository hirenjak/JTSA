using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Forms
{
    public class TwitchChatForm
    {
        public string Channel { get; set; } = "";
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Message { get; set; } = "";
        public string ColorHex { get; set; } = "";
        public string MessageId { get; set; } = "";

        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
    }
}
