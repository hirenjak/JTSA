using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace JTSA.Forms
{
    public class FriendForm
    {
        public required string BroadcastId { get; set; }
        public required string UserId { get; set; }
        public required string DisplayName { get; set; }
        public required string LastUsedDate { get; set; }
        public ImageSource? ProfileImage { get; set; }
    }
}
