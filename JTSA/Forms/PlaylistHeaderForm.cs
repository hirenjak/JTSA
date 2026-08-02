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
    public class PlaylistHeaderForm
    {
        public required long GamePlayListId { get; set; }
        public required string GamePlayListName { get; set; }
        public required string LastUsedDate { get; set; }
        public required string ImageUrl { get; set; }
        public required bool IsLoaded { get; set; }
    }
}
