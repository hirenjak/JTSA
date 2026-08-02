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
    public class TitleTagForm
    {
        public long Id { get; set; }
        public required String DisplayName { get; set; }
        public required String LastUsedDate { get; set; }
    }
}
