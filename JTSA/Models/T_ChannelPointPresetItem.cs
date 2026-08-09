namespace JTSA.Models
{
    /// <summary>
    /// プリセットの中身。保存時点の「操作可能な全報酬」の有効／無効を1件1レコードで保持する。
    ///
    /// 差分方式ではなく全件スナップショット方式にしているのは、
    /// 適用すれば必ず狙った状態が再現され、指定漏れが起きないようにするため。
    /// </summary>
    public class T_ChannelPointPresetItem : DBBaseTransaction
    {
        /// <summary> プリセットID [複合キー] </summary>
        public required long PresetId { get; set; }

        /// <summary> 報酬ID [複合キー] </summary>
        public required string RewardId { get; set; }

        /// <summary> 適用時に設定する有効／無効 </summary>
        public bool IsEnabled { get; set; }

        /// <summary> 保存時点の報酬名。報酬が削除された後もプリセットの内容を表示できるようにするため </summary>
        public required string RewardTitle { get; set; }
    }
}
