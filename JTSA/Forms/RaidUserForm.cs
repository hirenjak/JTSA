using JTSA.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JTSA.Forms
{
    // アプリ情報用クラス
    public class RaidUserForm
    {
        public string UserId { get; set; }

        public string UserName { get; set; }
        
        public string UserLogin { get; set; }
        
        public string ThumbnailUrl { get; set; }
        
        public string StreamTitle { get; set; }
        
        public string GameBoxArtUrl { get; set; }
        
        public string StreamingTime { get; set; }
    }
}
