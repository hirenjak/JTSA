namespace JTSA.Forms
{
    /// <summary>全期間で集計したチャットユーザー統計の表示データ。</summary>
    public class ChatUserStatisticsForm
    {
        public int Rank { get; init; }
        public string UserId { get; init; } = string.Empty;
        public string LoginId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int ChatCount { get; init; }
        public int ActiveDays { get; init; }
        public DateTime FirstChatDate { get; init; }
        public DateTime LastChatDate { get; init; }

        public string RankText => $"#{Rank}";
        public string ChatCountText => $"{ChatCount:N0}";
        public string ActiveDaysText => $"{ActiveDays:N0} 配信";
        public string FirstChatDateText => FirstChatDate.ToString("yyyy/MM/dd");
        public string LastChatDateText => LastChatDate.ToString("yyyy/MM/dd");
    }
}
