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

        /// <summary>カテゴリに紐づかない手動追加ゲームの表示名</summary>
        public string CustomGameName { get; set; } = string.Empty;

        /// <summary>カテゴリに紐づかない手動追加ゲームのSteamストアURL</summary>
        [Column("CustomImageUrl")]
        public string CustomSteamUrl { get; set; } = string.Empty;

        public bool IsCustomGame { get; set; }

        /// <summary> 保持ステータス </summary>
        public int Status { get; set; }
    }
}
