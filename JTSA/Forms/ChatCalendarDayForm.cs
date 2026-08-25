namespace JTSA.Forms
{
    public class ChatCalendarStreamCountForm
    {
        public required string StreamId { get; init; }
        public int ChatCount { get; init; }
        public string ChatCountText => $"{ChatCount:N0}件";
    }

    /// <summary>チャット統計のカスタムカレンダーに表示する1日分のデータ。</summary>
    public class ChatCalendarDayForm
    {
        public required DateTime Date { get; init; }
        public required int DisplayMonth { get; init; }
        public int ChatCount { get; init; }
        public IReadOnlyList<ChatCalendarStreamCountForm> StreamChatCounts { get; init; } = [];
        public string StreamSummaryText { get; init; } = string.Empty;
        public int Day => Date.Day;
        public bool IsCurrentMonth => Date.Month == DisplayMonth;
        public bool IsToday => Date == DateTime.Today;
        public bool IsInAggregationPeriod { get; init; }
        public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
        public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
        public bool HasActivity => StreamChatCounts.Count > 0;
        public bool HasStreams => !string.IsNullOrWhiteSpace(StreamSummaryText);
        public string StreamCountText { get; init; } = string.Empty;
        public string ChatCountText => $"{ChatCount:N0}件";
        public string ToolTipText
        {
            get
            {
                var text = HasActivity
                    ? $"{Date:yyyy/MM/dd}  {ChatCount:N0} 発言"
                    : $"{Date:yyyy/MM/dd}";
                return HasStreams ? $"{text}\n\n配信履歴\n{StreamSummaryText}" : text;
            }
        }
    }
}
