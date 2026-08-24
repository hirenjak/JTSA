using JTSA.Dao;
using JTSA.Forms;
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

            var dailyCounts = DAO_DailyChatUserCount.SelectByUserId(user.UserId);
            selectedUserChatCountsByDate = dailyCounts
                .GroupBy(x => x.ChatDate.Date)
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
                CalendarDays.Add(new ChatCalendarDayForm
                {
                    Date = date,
                    DisplayMonth = displayedCalendarMonth.Month,
                    ChatCount = chatCount
                });
            }

            var monthEnd = displayedCalendarMonth.AddMonths(1);
            var monthEntries = selectedUserChatCountsByDate
                .Where(x => x.Key >= displayedCalendarMonth && x.Key < monthEnd)
                .ToList();
            CalendarMonthSummaryTextBlock.Text = $"{monthEntries.Count:N0} 日 / {monthEntries.Sum(x => x.Value):N0} 発言";
        }

        private void ClearActivityCalendar()
        {
            selectedUserChatCountsByDate.Clear();
            CalendarUserNameTextBlock.Text = "ユーザーを選択";
            displayedCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildCalendarDays();
        }

        public void ReloadStatistics()
        {
            var dailyCounts = DAO_DailyChatUserCount.SelectAll();
            var statistics = dailyCounts
                .GroupBy(x => x.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Latest = group.OrderByDescending(x => x.ChatDate).First(),
                    ChatCount = group.Sum(x => x.ChatCount),
                    ActiveDays = group.Select(x => x.ChatDate.Date).Distinct().Count(),
                    FirstChatDate = group.Min(x => x.ChatDate),
                    LastChatDate = group.Max(x => x.ChatDate)
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

            if (dailyCounts.Count == 0)
            {
                PeriodTextBlock.Text = "保存済みのチャット統計はまだありません";
                return;
            }

            var firstDate = dailyCounts.Min(x => x.ChatDate);
            var lastDate = dailyCounts.Max(x => x.ChatDate);
            PeriodTextBlock.Text = $"集計期間  {firstDate:yyyy/MM/dd} ～ {lastDate:yyyy/MM/dd}";
        }
    }
}
