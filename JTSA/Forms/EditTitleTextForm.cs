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
    public class EditTitleTextForm
    {
        public required String Content { get; set; }
        public required String CategoryId { get; set; }
        public required String CategoryName { get; set; }
        public required String CategoryBoxArtUrl { get; set; }
    }
}
