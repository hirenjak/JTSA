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
using System.Windows.Media;

namespace JTSA.Forms
{
    /// <summary>
    /// アプリログForm
    /// </summary>
    public class AppLogForm
    {
        public required DateTime LogDateTime { get; set; }
        public required string Content { get; set; }
        public required SolidColorBrush Color { get; set; }
    }
}
