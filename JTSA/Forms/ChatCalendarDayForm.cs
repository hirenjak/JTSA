namespace JTSA.Forms
{
    /// <summary>チャット統計のカスタムカレンダーに表示する1日分のデータ。</summary>
    public class ChatCalendarDayForm
    {
        public required DateTime Date { get; init; }
        public required int DisplayMonth { get; init; }
        public int ChatCount { get; init; }
        public int Day => Date.Day;
        public bool IsCurrentMonth => Date.Month == DisplayMonth;
        public bool IsToday => Date == DateTime.Today;
        public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
        public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
        public bool HasActivity => ChatCount > 0;
        public string ChatCountText => $"{ChatCount:N0}件";
        public string ToolTipText => HasActivity
            ? $"{Date:yyyy/MM/dd}  {ChatCount:N0} 発言"
            : $"{Date:yyyy/MM/dd}";
    }
}
