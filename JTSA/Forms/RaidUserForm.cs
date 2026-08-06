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
        public string UserId;
        public string UserName;
        public string UserLogin;
        public string ThumbnailUrl;
        public string StreamTitle;
        public string StreamGameId;
        public string StreamingTime;
    }
}
