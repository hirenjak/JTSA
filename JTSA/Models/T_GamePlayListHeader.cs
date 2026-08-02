using JTSA.Panels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Models
{
    public class T_GamePlaylistHeader : DBBaseTransaction
    {
        [Key]
        public long GamePlayListId { get; set; }

        public required string GamePlayListName { get; set; }

        public required string ThumbnailCategoryUrl { get; set; }
    }
}
