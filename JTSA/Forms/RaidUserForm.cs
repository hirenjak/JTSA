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
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
        
        public string UserLogin { get; set; } = string.Empty;
        
        public string ThumbnailUrl { get; set; } = string.Empty;
        
        public string StreamTitle { get; set; } = string.Empty;
        
        public string GameBoxArtUrl { get; set; } = string.Empty;
        
        public string StreamingTime { get; set; } = string.Empty;
    }
}
