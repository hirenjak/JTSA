using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JTSA
{
    public class FriendTagForm
    {
        public required String BroadcastId { get; set; }
        public required String UserId { get; set; }
        public required String DisplayName { get; set; }
        public required String LastUsedDate { get; set; }
    }
}
