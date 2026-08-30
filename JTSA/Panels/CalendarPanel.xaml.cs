using JTSA.Dao;
using JTSA.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTSA.Panels;

public sealed class CalendarScheduleDayForm
{
    public required DateTime Date { get; init; }
    public required int DisplayMonth { get; init; }
    public string ContentPreview { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public int Day => Date.Day;
    public bool IsCurrentMonth => Date.Month == DisplayMonth;
    public bool IsToday => Date == DateTime.Today;
    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
    public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
    public bool HasEntry => !string.IsNullOrWhiteSpace(ContentPreview);
}

public partial class CalendarPanel : UserControl
{
    public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(CloseRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CalendarPanel));

    private const string DateFormat = "yyyy-MM-dd";
    private bool isSynchronizingSelection;
    private DateTime selectedDate = DateTime.Today;
    private DateTime displayedCalendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public ObservableCollection<T_CalendarEntry> Entries { get; } = [];
    public ObservableCollection<CalendarScheduleDayForm> CalendarDays { get; } = [];
    public event Action<long>? EditRequested;
    public DateTime SelectedDate => selectedDate;

    public event RoutedEventHandler CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public CalendarPanel()
    {
        InitializeComponent();
        MigrateLegacyMemos();
        ReloadEntries();
        EntryDatePicker.SelectedDate = DateTime.Today;
        RefreshSelectedDate();
    }

    public void RefreshSelectedDate()
    {
        ReloadEntries();
        SelectDate(selectedDate);
    }

    private void ReloadEntries(DateTime? selectedDate = null)
    {
        Entries.Clear();
        foreach (var entry in DAO_Calendar.SelectAll()
                     .OrderBy(entry => entry.CalendarDate < DateTime.Today)
                     .ThenBy(entry => entry.CalendarDate))
        {
            Entries.Add(entry);
        }

        EntryCountTextBlock.Text = $"{Entries.Count:N0}件";
        BuildCalendarDays();
        if (selectedDate.HasValue)
            CalendarEntryListBox.SelectedItem = Entries.FirstOrDefault(x => x.CalendarDate.Date == selectedDate.Value.Date);
    }

    private static void MigrateLegacyMemos()
    {
        var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.CalendarMemos)?.Value;
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var existingDates = DAO_Calendar.SelectAll()
                .Select(entry => entry.CalendarDate.Date)
                .ToHashSet();
            var legacyMemos = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            foreach (var (dateText, memo) in legacyMemos)
            {
                if (DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date) && !existingDates.Contains(date.Date))
                    DAO_Calendar.InsertUpdate(date, memo);
            }

            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.CalendarMemos, string.Empty);
        }
        catch (JsonException)
        {
            // 壊れた旧設定は無視し、専用テーブルの内容だけを使用する。
        }
    }

    private void SelectDate(DateTime date)
    {
        if (isSynchronizingSelection) return;

        isSynchronizingSelection = true;
        try
        {
            var selectedDate = date.Date;
            this.selectedDate = selectedDate;
            EntryDatePicker.SelectedDate = selectedDate;
            SelectedDateTextBlock.Text = selectedDate.ToString(
                "yyyy年M月d日（ddd）", CultureInfo.GetCultureInfo("ja-JP"));

            var entry = Entries.FirstOrDefault(x => x.CalendarDate.Date == selectedDate);
            CalendarEntryListBox.SelectedItem = entry;
            MemoTextBox.Text = entry?.Content ?? string.Empty;
            BuildCalendarDays();
        }
        finally
        {
            isSynchronizingSelection = false;
        }
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
        SelectDate(DateTime.Today);
    }

    private void DayCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: CalendarScheduleDayForm day }) return;

        if (!day.IsCurrentMonth)
            displayedCalendarMonth = new DateTime(day.Date.Year, day.Date.Month, 1);
        SelectDate(day.Date);
        e.Handled = true;
    }

    private void BuildCalendarDays()
    {
        if (CalendarMonthTextBlock == null) return;

        CalendarMonthTextBlock.Text = displayedCalendarMonth.ToString("yyyy年 M月");
        var calendarStart = displayedCalendarMonth.AddDays(-(int)displayedCalendarMonth.DayOfWeek);
        var entriesByDate = Entries
            .GroupBy(entry => entry.CalendarDate.Date)
            .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.StartTime).First());

        CalendarDays.Clear();
        for (var index = 0; index < 42; index++)
        {
            var date = calendarStart.AddDays(index);
            entriesByDate.TryGetValue(date, out var entry);
            CalendarDays.Add(new CalendarScheduleDayForm
            {
                Date = date,
                DisplayMonth = displayedCalendarMonth.Month,
                ContentPreview = entry?.Content ?? string.Empty,
                IsSelected = date == selectedDate
            });
        }

        var monthEnd = displayedCalendarMonth.AddMonths(1);
        var count = Entries.Count(entry => entry.CalendarDate >= displayedCalendarMonth && entry.CalendarDate < monthEnd);
        CalendarMonthSummaryTextBlock.Text = $"予定 {count:N0}件";
    }

    private void EntryDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EntryDatePicker.SelectedDate is DateTime date)
            SelectDate(date);
    }

    private void CalendarEntryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isSynchronizingSelection && CalendarEntryListBox.SelectedItem is T_CalendarEntry entry)
            SelectDate(entry.CalendarDate);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var date = EntryDatePicker.SelectedDate?.Date ?? DateTime.Today;
        var memo = MemoTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(memo))
        {
            HeaderStatusTextBlock.Text = "予定内容を入力してください。";
            return;
        }

        var existing = Entries.FirstOrDefault(x => x.CalendarDate.Date == date);
        DAO_Calendar.InsertUpdate(
            date,
            memo,
            existing?.TitlePlaceholder ?? string.Empty,
            existing?.CategoryId ?? string.Empty,
            existing?.CategoryName ?? string.Empty,
            existing?.CategoryBoxArtUrl ?? string.Empty,
            existing?.SelectedFriendIds ?? string.Empty,
            existing?.StartTime,
            existing?.Id);
        ReloadEntries(date);
        SelectDate(date);
        HeaderStatusTextBlock.Text = "予定を保存しました。";
    }

    private void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not T_CalendarEntry entry) return;

        DAO_Calendar.Delete(entry.Id);
        ReloadEntries();
        SelectDate(entry.CalendarDate);
        HeaderStatusTextBlock.Text = "予定を削除しました。";
        e.Handled = true;
    }

    private void ApplyEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not T_CalendarEntry entry ||
            Window.GetWindow(this) is not MainWindow mainWindow)
            return;

        mainWindow.ApplyCalendarEntryToOverview(entry);
        HeaderStatusTextBlock.Text = "送信予定の情報へ反映しました。";
        e.Handled = true;
    }

    private void EditEntryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Tag is not T_CalendarEntry entry) return;
        CalendarEntryListBox.SelectedItem = entry;
        EditRequested?.Invoke(entry.Id);
        e.Handled = true;
    }

    private void EditDayEntryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Tag is not CalendarScheduleDayForm day) return;
        var entry = Entries
            .Where(item => item.CalendarDate.Date == day.Date.Date)
            .OrderBy(item => item.StartTime)
            .FirstOrDefault();
        if (entry is null) return;

        CalendarEntryListBox.SelectedItem = entry;
        CalendarEntryListBox.ScrollIntoView(entry);
        EditRequested?.Invoke(entry.Id);
        e.Handled = true;
    }

    private void ClearInputButton_Click(object sender, RoutedEventArgs e)
    {
        CalendarEntryListBox.SelectedItem = null;
        EntryDatePicker.SelectedDate = DateTime.Today;
        MemoTextBox.Clear();
        MemoTextBox.Focus();
        HeaderStatusTextBlock.Text = "新しい予定を入力できます。";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
}
