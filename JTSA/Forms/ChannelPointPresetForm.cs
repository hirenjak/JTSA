using System.ComponentModel;

namespace JTSA.Forms
{
    /// <summary>
    /// チャンネルポイントプリセットの表示用DTO
    /// </summary>
    public class ChannelPointPresetForm
    {
        /// <summary> プリセットID </summary>
        public long PresetId { get; set; }

        /// <summary> プリセット名 </summary>
        public string PresetName { get; set; } = "";

        /// <summary> 登録されている報酬の件数 </summary>
        public int ItemCount { get; set; }

        /// <summary> 最終適用日時 </summary>
        public string LastUsedDate { get; set; } = "";

        /// <summary> 一覧に出す表示名 </summary>
        public string DisplayText => $"{PresetName}（{ItemCount}件）";
    }


    /// <summary>
    /// プリセットの中身（報酬1件分）の表示用DTO
    /// </summary>
    public class ChannelPointPresetItemForm : INotifyPropertyChanged
    {
        /// <summary> 報酬ID </summary>
        public string RewardId { get; set; } = "";

        /// <summary> 報酬名（プリセット保存時点のもの） </summary>
        public string RewardTitle { get; set; } = "";

        /// <summary>
        /// 現在の報酬一覧に存在するか。
        /// falseの場合、報酬が削除されているため適用時にスキップされる。
        /// </summary>
        public bool IsExisting { get; set; }

        /// <summary> 適用時に設定する有効／無効 </summary>
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
        private bool isEnabled;

        /// <summary> 存在しない報酬であることを示す注記 </summary>
        public string StatusText => IsExisting ? "" : "（削除済み）";


        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
