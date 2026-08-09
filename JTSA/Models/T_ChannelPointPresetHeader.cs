using System.ComponentModel.DataAnnotations;

namespace JTSA.Models
{
    /// <summary>
    /// チャンネルポイントのプリセット（報酬ON/OFFの組み合わせ）のヘッダ
    /// </summary>
    public class T_ChannelPointPresetHeader : DBBaseTransaction
    {
        /// <summary> プリセットID（T_GamePlaylistHeaderと同じくUnixミリ秒で採番する） </summary>
        [Key]
        public long PresetId { get; set; }

        /// <summary> プリセット名 </summary>
        public required string PresetName { get; set; }
    }
}
