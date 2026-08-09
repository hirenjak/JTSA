using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JTSA.Forms
{
    public class CategoryForm
    {
        public required String CategoryId { get; set; }
        public required String DisplayName { get; set; }
        public required String LastUsedDate { get; set; }
        public required String BoxArtUrl { get; set; }
        public required String SteamUrl { get; set; }

        /// <summary>
        /// 紐づくチャンネルポイントプリセットID。
        /// ComboBoxのSelectedValueに使うため、未紐づけはnullではなく0で表す。
        /// </summary>
        public long ChannelPointPresetId { get; set; }
    }
}
