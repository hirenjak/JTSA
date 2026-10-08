using System.ComponentModel;

namespace JTSA.Forms
{
    /// <summary>チャットパネルの参加ユーザー一覧用表示データ。</summary>
    public class ChatUserForm : INotifyPropertyChanged
    {
        public required string UserId { get; set; }
        public required string UserName { get; set; }
        public required string DisplayName { get; set; }
        private string profileImageUrl = string.Empty;
        public required string ProfileImageUrl
        {
            get => profileImageUrl;
            set
            {
                if (profileImageUrl == value) return;
                profileImageUrl = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProfileImageUrl)));
            }
        }
        public required DateTime LastChatDateTime { get; set; }
        public int MessageCount { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        private bool isSpeechMuted;
        public bool IsSpeechMuted
        {
            get => isSpeechMuted;
            set
            {
                if (isSpeechMuted == value) return;
                isSpeechMuted = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSpeechMuted)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
