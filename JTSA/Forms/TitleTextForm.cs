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
    public class TitleTextForm
    {
        public long Id { get; set; }
        public required string Content { get; set; }
        public required string CategoryId { get; set; }
        public required string CategoryName { get; set; }
        public required string CategoryBoxArtUrl { get; set; }
        public required string LastUsedDate { get; set; }
    }
}
