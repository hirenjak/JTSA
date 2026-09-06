using JTSA.Dao;
using JTSA.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace JTSA.Panels;

public sealed class CalendarScheduleDayForm : INotifyPropertyChanged
{
    public required DateTime Date { get; init; }
    public required int DisplayMonth { get; init; }
    public string ContentPreview { get; init; } = string.Empty;
    public string StartTimeDisplay { get; init; } = string.Empty;
    public string CategoryBoxArtUrl { get; init; } = string.Empty;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    private bool isSelected;
    public int Day => Date.Day;
    public bool IsCurrentMonth => Date.Month == DisplayMonth;
    public bool IsToday => Date == DateTime.Today;
    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
    public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
    public bool HasEntry => !string.IsNullOrWhiteSpace(ContentPreview);

    public event PropertyChangedEventHandler? PropertyChanged;
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
    public ObservableCollection<T_CalendarEntry> DayPopupEntries { get; } = [];
    public event Action? AddRequested;
    public event Action<long>? EditRequested;
    public event Action<long>? DuplicateRequested;
    public DateTime SelectedDate => selectedDate;
    private readonly DispatcherTimer dayPopupCloseTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };

    public event RoutedEventHandler CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public CalendarPanel()
    {
        InitializeComponent();
        dayPopupCloseTimer.Tick += DayPopupCloseTimer_Tick;
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
            foreach (var day in CalendarDays)
                day.IsSelected = day.Date == selectedDate;
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
        BuildCalendarDays();
        SelectDate(DateTime.Today);
    }

    private void DayCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: CalendarScheduleDayForm day }) return;

        var isCurrentMonth = day.IsCurrentMonth;
        if (!day.IsCurrentMonth)
        {
            displayedCalendarMonth = new DateTime(day.Date.Year, day.Date.Month, 1);
            BuildCalendarDays();
        }
        SelectDate(day.Date);
        ShowDaySchedulePopup(day.Date, isCurrentMonth ? sender as UIElement : this);
        e.Handled = true;
    }

    private void ShowDaySchedulePopup(DateTime date, UIElement? placementTarget)
    {
        dayPopupCloseTimer.Stop();
        DayPopupEntries.Clear();
        foreach (var entry in Entries
                     .Where(entry => entry.CalendarDate.Date == date.Date)
                     .OrderBy(entry => entry.StartTime)
                     .ThenBy(entry => entry.Id))
            DayPopupEntries.Add(entry);

        DaySchedulePopupTitle.Text = date.ToString("M月d日（ddd）の予定", CultureInfo.GetCultureInfo("ja-JP"));
        DaySchedulePopupEmptyText.Visibility = DayPopupEntries.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        DaySchedulePopupList.Visibility = DayPopupEntries.Count == 0
            ? Visibility.Collapsed : Visibility.Visible;
        DaySchedulePopup.PlacementTarget = placementTarget ?? this;
        DaySchedulePopup.IsOpen = true;
    }

    private void DayCell_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DaySchedulePopup.IsOpen)
            dayPopupCloseTimer.Start();
    }

    private void DayCell_MouseEnter(object sender, MouseEventArgs e)
        => dayPopupCloseTimer.Stop();

    private void DaySchedulePopup_MouseEnter(object sender, MouseEventArgs e)
        => dayPopupCloseTimer.Stop();

    private void DaySchedulePopup_MouseLeave(object sender, MouseEventArgs e)
    {
        dayPopupCloseTimer.Stop();
        DaySchedulePopup.IsOpen = false;
    }

    private void DayPopupCloseTimer_Tick(object? sender, EventArgs e)
    {
        dayPopupCloseTimer.Stop();
        if (!DaySchedulePopup.IsMouseOver)
            DaySchedulePopup.IsOpen = false;
    }

    private void BuildCalendarDays()
    {
        if (CalendarMonthTextBlock == null) return;

        CalendarMonthTextBlock.Text = displayedCalendarMonth.ToString("yyyy年 M月");
        var calendarStart = displayedCalendarMonth.AddDays(-(int)displayedCalendarMonth.DayOfWeek);
        var now = DateTime.Now;
        var entriesByDate = Entries
            .GroupBy(entry => entry.CalendarDate.Date)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var orderedEntries = group.OrderBy(entry => entry.StartTime).ToList();
                    if (group.Key != now.Date) return orderedEntries[0];

                    // 当日は、これから始まる予定のうち現在時刻に最も近いものを優先する。
                    // 全予定が開始済みなら、直前（最後に開始した）の予定を表示する。
                    return orderedEntries.FirstOrDefault(entry => entry.StartTime >= now.TimeOfDay)
                           ?? orderedEntries[^1];
                });

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
                StartTimeDisplay = entry?.StartTimeDisplay ?? string.Empty,
                CategoryBoxArtUrl = entry?.CategoryBoxArtUrl ?? string.Empty,
                IsSelected = date == selectedDate
            });
        }

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

    private void EditEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not T_CalendarEntry entry) return;
        CalendarEntryListBox.SelectedItem = entry;
        EditRequested?.Invoke(entry.Id);
        e.Handled = true;
    }

    private void DuplicateEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not T_CalendarEntry entry) return;
        DuplicateRequested?.Invoke(entry.Id);
        e.Handled = true;
    }

    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
        => AddRequested?.Invoke();

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
