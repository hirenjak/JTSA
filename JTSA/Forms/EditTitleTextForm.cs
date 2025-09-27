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
        public string Content { get; set; }
        public string CategoryId { get; private set; }
        public string CategoryName { get; private set; }
        public string CategoryBoxArtUrl { get; private set; }


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public EditTitleTextForm()
        {
            Content = string.Empty;
            CategoryId = string.Empty;
            CategoryName = string.Empty;
            CategoryBoxArtUrl = string.Empty;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="boxArtUlr"></param>
        public void SetCategory(string id, string name, string boxArtUlr)
        {
            this.CategoryId = id;
            this.CategoryName = name;
            this.CategoryBoxArtUrl = boxArtUlr;
        }
    }
}
