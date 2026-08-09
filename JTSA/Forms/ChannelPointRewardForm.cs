using System.ComponentModel;
using System.Windows;

namespace JTSA.Forms
{
    /// <summary>
    /// チャンネルポイント報酬（カスタムリワード）の表示用DTO
    /// EventSub の交換イベント用 <see cref="ChannelPointForm"/> とは別物なので注意
    /// </summary>
    public class ChannelPointRewardForm : INotifyPropertyChanged
    {
        /// <summary> 報酬ID </summary>
        public string RewardId { get; set; } = "";

        /// <summary> 報酬名 </summary>
        public string Title { get; set; } = "";

        /// <summary> 説明文 </summary>
        public string Prompt { get; set; } = "";

        /// <summary> コスト </summary>
        public int Cost { get; set; }

        /// <summary> アイコンURL（1x） </summary>
        public string ImageUrl { get; set; } = "";

        /// <summary> 背景色（#RRGGBB） </summary>
        public string BackgroundColor { get; set; } = "";

        /// <summary> ユーザー入力を要求するか </summary>
        public bool IsUserInputRequired { get; set; }

        /// <summary> 配信毎の上限（0＝無効） </summary>
        public int MaxPerStream { get; set; }

        /// <summary> 1人あたり配信毎の上限（0＝無効） </summary>
        public int MaxPerUserPerStream { get; set; }

        /// <summary> グローバルクールダウン秒（0＝無効） </summary>
        public int GlobalCooldownSeconds { get; set; }

        /// <summary> 承認キューをスキップするか </summary>
        public bool ShouldRedemptionsSkipQueue { get; set; }

        /// <summary>
        /// このアプリ（同一client_id）から操作できるか
        /// Twitch の Web 画面や他アプリから作成された報酬は false になる
        /// </summary>
        public bool IsManageable
        {
            get => isManageable;
            set
            {
                isManageable = value;
                OnPropertyChanged(nameof(IsManageable));
                OnPropertyChanged(nameof(ManageableMark));
                OnPropertyChanged(nameof(ManageableToolTip));
                OnPropertyChanged(nameof(CanCopy));
                OnPropertyChanged(nameof(CopyButtonVisibility));
                OnPropertyChanged(nameof(DeleteButtonVisibility));
            }
        }
        private bool isManageable;

        /// <summary> 有効／無効（無効にすると視聴者の報酬一覧から消える） </summary>
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

        /// <summary> 一時停止（表示されるが交換できない） </summary>
        public bool IsPaused
        {
            get => isPaused;
            set
            {
                isPaused = value;
                OnPropertyChanged(nameof(IsPaused));
            }
        }
        private bool isPaused;

        /// <summary> 一括操作用のチェック状態（画面上のみ・APIとは無関係） </summary>
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        private bool isSelected;


        // ============ 表示用の派生プロパティ ============

        /// <summary> 操作可能列に出す記号 </summary>
        public string ManageableMark => IsManageable ? "✔" : "🔒";

        /// <summary> 操作可能列のツールチップ </summary>
        public string ManageableToolTip => IsManageable
            ? "このアプリから操作できます。"
            : "Twitch の Web 画面（または他アプリ）から作成されたため、このアプリからは操作できません。コピーを作成してください。";

        /// <summary> コピーできるのは操作不可の報酬だけ（操作可能なものは既にアプリ管理下） </summary>
        public bool CanCopy => !IsManageable;

        /// <summary> コピーボタンは操作不可の報酬にだけ出す </summary>
        public Visibility CopyButtonVisibility =>
            IsManageable ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// 削除ボタンは操作可能な報酬にだけ出す。
        /// Twitchの仕様上、Web画面から作成された報酬はこのアプリからは削除できないため。
        /// </summary>
        public Visibility DeleteButtonVisibility =>
            IsManageable ? Visibility.Visible : Visibility.Collapsed;


        // ============ INotifyPropertyChanged ============

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
