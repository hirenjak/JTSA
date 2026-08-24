using JTSA.Dao;
using JTSA.Forms;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>保存済みのチャット発言数を全期間で集計して表示するパネル。</summary>
    public partial class ChatStatisticsPanel : UserControl
    {
        public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloseRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ChatStatisticsPanel));

        public event RoutedEventHandler CloseRequested
        {
            add => AddHandler(CloseRequestedEvent, value);
            remove => RemoveHandler(CloseRequestedEvent, value);
        }

        public ObservableCollection<ChatUserStatisticsForm> UserStatistics { get; } = new();
        public ObservableCollection<ChatCalendarDayForm> CalendarDays { get; } = new();
        private Dictionary<DateTime, int> selectedUserChatCountsByDate = new();
        private Dictionary<string, int> selectedUserChatCountsByStream = new();
        private Dictionary<DateTime, List<Models.T_StreamHistory>> streamsByDate = new();
        private DateTime displayedCalendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

        public ChatStatisticsPanel()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadStatistics();
        }

        public async Task SyncArchivedStreamsAsync()
        {
            try
            {
                var archives = await TwitchHelper.GetArchivedStreamHistoryAsync();
                foreach (var archive in archives)
                    DAO_StreamHistory.Upsert(archive);
            }
            catch (Exception ex)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.AppLogPanel.Error(GetType().Name, $"配信アーカイブ同期失敗：{ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
        }

        private void UserStatisticsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserStatisticsDataGrid.SelectedItem is not ChatUserStatisticsForm user)
            {
                ClearActivityCalendar();
                return;
            }

            var streamCounts = DAO_StreamChatUserCount.SelectByUserId(user.UserId);
            selectedUserChatCountsByStream = streamCounts
                .GroupBy(x => x.StreamId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.ChatCount));
            var streamDates = DAO_StreamHistory.SelectAll().ToDictionary(x => x.StreamId, x => x.StartedAt.Date);
            selectedUserChatCountsByDate = streamCounts
                .GroupBy(x => streamDates.TryGetValue(x.StreamId, out var date)
                    ? date
                    : x.FirstChatDateTime.Date)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.ChatCount));
            CalendarUserNameTextBlock.Text = user.DisplayName;

            if (selectedUserChatCountsByDate.Count > 0)
            {
                var latestDate = selectedUserChatCountsByDate.Keys.Max();
                displayedCalendarMonth = new DateTime(latestDate.Year, latestDate.Month, 1);
            }

            BuildCalendarDays();
        }

        private void PreviousMonthButton_Click(object sender, RoutedEventArgs e)
        {
            displayedCalendarMonth = displayedCalendarMonth.AddMonths(-1);
            BuildCalendarDays();
        }

        private void NextMonthButton_Click(object sender, RoutedEventArgs e)
        {
            displayedCalendarMonth = displayedCalendarMonth.AddMonths(1);
            BuildCalendarDays();
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            displayedCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildCalendarDays();
        }

        private void BuildCalendarDays()
        {
            CalendarMonthTextBlock.Text = displayedCalendarMonth.ToString("yyyy年 M月");
            var daysFromSunday = (int)displayedCalendarMonth.DayOfWeek;
            var calendarStart = displayedCalendarMonth.AddDays(-daysFromSunday);

            CalendarDays.Clear();
            for (var index = 0; index < 42; index++)
            {
                var date = calendarStart.AddDays(index);
                selectedUserChatCountsByDate.TryGetValue(date, out var chatCount);
                streamsByDate.TryGetValue(date, out var streams);
                var streamSummary = streams is null
                    ? string.Empty
                    : string.Join("\n\n", streams.Select(stream =>
                    {
                        selectedUserChatCountsByStream.TryGetValue(stream.StreamId, out var chatCountForStream);
                        return FormatStreamSummary(stream, chatCountForStream);
                    }));
                CalendarDays.Add(new ChatCalendarDayForm
                {
                    Date = date,
                    DisplayMonth = displayedCalendarMonth.Month,
                    ChatCount = chatCount,
                    StreamSummaryText = streamSummary,
                    StreamCountText = streams is null ? string.Empty : $"配信×{streams.Count}"
                });
            }

            var monthEnd = displayedCalendarMonth.AddMonths(1);
            var monthEntries = selectedUserChatCountsByDate
                .Where(x => x.Key >= displayedCalendarMonth && x.Key < monthEnd)
                .ToList();
            CalendarMonthSummaryTextBlock.Text = $"{monthEntries.Count:N0} 日 / {monthEntries.Sum(x => x.Value):N0} 発言";
        }

        private static string FormatStreamSummary(Models.T_StreamHistory stream, int chatCountForStream)
        {
            var end = stream.EndedAt;
            var timeText = end is null
                ? $"{stream.StartedAt:HH:mm} ～ 配信中"
                : $"{stream.StartedAt:HH:mm} ～ {end:HH:mm}  ({FormatDuration(end.Value - stream.StartedAt)})";
            var category = string.IsNullOrWhiteSpace(stream.CategoryName) ? string.Empty : $"\nカテゴリ: {stream.CategoryName}";
            var chatCount = $"\nこのユーザーの発言: {chatCountForStream:N0}件";
            var archive = string.IsNullOrWhiteSpace(stream.ArchiveUrl) ? string.Empty : $"\n{stream.ArchiveUrl}";
            return $"{timeText}\n{stream.Title}{category}{chatCount}{archive}";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}時間{duration.Minutes}分"
                : $"{duration.Minutes}分";
        }

        private void ClearActivityCalendar()
        {
            selectedUserChatCountsByDate.Clear();
            selectedUserChatCountsByStream.Clear();
            CalendarUserNameTextBlock.Text = "ユーザーを選択";
            displayedCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildCalendarDays();
        }

        public void ReloadStatistics()
        {
            var streamCounts = DAO_StreamChatUserCount.SelectAll();
            streamsByDate = DAO_StreamHistory.SelectAll()
                .GroupBy(x => x.StartedAt.Date)
                .ToDictionary(x => x.Key, x => x.OrderBy(y => y.StartedAt).ToList());
            var statistics = streamCounts
                .GroupBy(x => x.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Latest = group.OrderByDescending(x => x.LastChatDateTime).First(),
                    ChatCount = group.Sum(x => x.ChatCount),
                    ActiveDays = group.Select(x => x.StreamId).Distinct().Count(),
                    FirstChatDate = group.Min(x => x.FirstChatDateTime),
                    LastChatDate = group.Max(x => x.LastChatDateTime)
                })
                .OrderByDescending(x => x.ChatCount)
                .ThenBy(x => x.Latest.DisplayName)
                .ToList();

            UserStatistics.Clear();
            for (var index = 0; index < statistics.Count; index++)
            {
                var item = statistics[index];
                UserStatistics.Add(new ChatUserStatisticsForm
                {
                    Rank = index + 1,
                    UserId = item.UserId,
                    LoginId = item.Latest.LoginId,
                    DisplayName = item.Latest.DisplayName,
                    ChatCount = item.ChatCount,
                    ActiveDays = item.ActiveDays,
                    FirstChatDate = item.FirstChatDate,
                    LastChatDate = item.LastChatDate
                });
            }

            UserStatisticsDataGrid.SelectedItem = UserStatistics.FirstOrDefault();

            if (streamCounts.Count == 0)
            {
                PeriodTextBlock.Text = "保存済みのチャット統計はまだありません";
                return;
            }

            var firstDate = streamCounts.Min(x => x.FirstChatDateTime);
            var lastDate = streamCounts.Max(x => x.LastChatDateTime);
            PeriodTextBlock.Text = $"集計期間  {firstDate:yyyy/MM/dd} ～ {lastDate:yyyy/MM/dd}";
        }
    }
}
