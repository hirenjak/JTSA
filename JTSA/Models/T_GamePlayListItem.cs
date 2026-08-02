using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Models
{
    public class T_GamePlaylistItem : DBBaseTransaction
    {
        /// <summary> テーブルID [複合キー] </summary>
        public required long GamePlayListId { get; set; }

        /// <summary> カテゴリーID [複合キー] </summary>
        public required string CategoryId { get; set; }

        /// <summary> 保持ステータス </summary>
        public int Status { get; set; }
    }
}
