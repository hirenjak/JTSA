using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace JTSA.Panels;

public partial class CalendarRegistrationPanel : UserControl
{
    public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(CloseRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CalendarRegistrationPanel));

    private string categoryId = string.Empty;
    private string categoryName = string.Empty;
    private string categoryBoxArtUrl = string.Empty;
    private long? editingEntryId;

    public ObservableCollection<T_CalendarEntry> Entries { get; } = [];
    public ObservableCollection<CategoryForm> Categories { get; } = [];
    public ObservableCollection<FriendForm> SelectedFriends { get; } = [];

    public event RoutedEventHandler CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public CalendarRegistrationPanel()
    {
        InitializeComponent();
        StartHourComboBox.ItemsSource = Enumerable.Range(0, 24).Select(value => value.ToString("00"));
        StartMinuteComboBox.ItemsSource = Enumerable.Range(0, 12).Select(value => (value * 5).ToString("00"));
        StartHourComboBox.SelectedIndex = 0;
        StartMinuteComboBox.SelectedIndex = 0;
        RegistrationTitleTagPanel.InsertRequested += InsertTitleTag;
        Loaded += CalendarRegistrationPanel_Loaded;
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        Reload();
    }

    private void CalendarRegistrationPanel_Loaded(object sender, RoutedEventArgs e)
    {
        RegistrationTitleTagPanel.ReloadTitleTag();
        RestoreSelectedFriends(string.Join(',', SelectedFriends.Select(friend => friend.BroadcastId)));
    }

    public void SetInitialPlaceholder(string placeholder)
    {
        TitlePlaceholderTextBox.Text = placeholder;
    }

    public void SelectEntryForEditing(long entryId)
    {
        Reload();
        var entry = Entries.FirstOrDefault(item => item.Id == entryId);
        if (entry is null) return;
        LoadEntry(entry);
    }

    public void SetScheduleDateFromCalendar(DateTime date)
    {
        editingEntryId = null;
        ScheduleDatePicker.SelectedDate = date.Date;
    }

    public void Reload()
    {
        Entries.Clear();
        foreach (var entry in DAO_Calendar.SelectAll()
                     .OrderByDescending(x => x.CalendarDate)
                     .ThenBy(x => x.StartTime))
            Entries.Add(entry);

        Categories.Clear();
        foreach (var item in DAO_Category.SelectAllOrderbyLastUser())
        {
            Categories.Add(new CategoryForm
            {
                CategoryId = item.CategoryId,
                DisplayName = item.DisplayName,
                JapaneseDisplayName = item.JapaneseDisplayName,
                BoxArtUrl = item.BoxArtUrl,
                SteamUrl = item.SteamUrl ?? string.Empty,
                ChannelPointPresetId = item.ChannelPointPresetId ?? 0,
                LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd HH:mm")
            });
        }


        ReloadFriends();
    }

    private void CategoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CategoryListBox.SelectedItem is not CategoryForm category) return;
        categoryId = category.CategoryId;
        categoryName = category.DisplayName;
        categoryBoxArtUrl = category.BoxArtUrl;
        ShowSelectedCategory();
        StatusTextBlock.Text = $"カテゴリ：{category.DisplayName}";
        CategoryListBox.SelectedItem = null;
    }

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new CategorySearchWindow
        {
            Owner = Window.GetWindow(this)
        };

        window.ShowDialog();
        Reload();
    }

    private void LoadEntry(T_CalendarEntry entry)
    {
        editingEntryId = entry.Id;
        ScheduleDatePicker.SelectedDate = entry.CalendarDate;
        ContentTextBox.Text = entry.Content;
        TitlePlaceholderTextBox.Text = entry.TitlePlaceholder;
        StartHourComboBox.SelectedIndex = entry.StartTime.Hours;
        StartMinuteComboBox.SelectedIndex = entry.StartTime.Minutes / 5;
        categoryId = entry.CategoryId;
        categoryName = entry.CategoryName;
        categoryBoxArtUrl = entry.CategoryBoxArtUrl;
        RestoreSelectedFriends(entry.SelectedFriendIds);
        ShowSelectedCategory();
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(categoryName) ? "予定を編集中" : $"カテゴリ：{categoryName}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ContentTextBox.Text))
        {
            StatusTextBlock.Text = "タイトル本文を入力してください。";
            return;
        }

        if (StartHourComboBox.SelectedIndex < 0 || StartMinuteComboBox.SelectedIndex < 0)
        {
            StatusTextBlock.Text = "開始時間を選択してください。";
            return;
        }

        var startTime = new TimeSpan(StartHourComboBox.SelectedIndex, StartMinuteComboBox.SelectedIndex * 5, 0);

        DAO_Calendar.InsertUpdate(
            ScheduleDatePicker.SelectedDate ?? DateTime.Today,
            ContentTextBox.Text.Trim(),
            TitlePlaceholderTextBox.Text.Trim(),
            categoryId,
            categoryName,
            categoryBoxArtUrl,
            string.Join(',', SelectedFriends.Select(friend => friend.BroadcastId)),
            startTime,
            editingEntryId);
        editingEntryId = null;
        Reload();
        StatusTextBlock.Text = "予定を保存しました。";
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));

    private void InsertTitleTag(string placeholder)
    {
        var index = Math.Max(0, TitlePlaceholderTextBox.CaretIndex);
        TitlePlaceholderTextBox.Text = TitlePlaceholderTextBox.Text.Insert(index, placeholder);
        TitlePlaceholderTextBox.CaretIndex = index + placeholder.Length;
        TitlePlaceholderTextBox.Focus();
    }

    private void ShowSelectedCategory()
    {
        SelectedCategoryTextBlock.Text = string.IsNullOrWhiteSpace(categoryName) ? "未選択" : categoryName;
        try
        {
            SelectedCategoryBoxArt.Source = string.IsNullOrWhiteSpace(categoryBoxArtUrl)
                ? null
                : new BitmapImage(new Uri(categoryBoxArtUrl));
        }
        catch
        {
            SelectedCategoryBoxArt.Source = null;
        }
    }


    private void SelectFriendsButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedIds = SelectedFriends.Select(friend => friend.BroadcastId);

        var dialog = new FriendSelectionWindow(selectedIds)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
            RestoreSelectedFriends(string.Join(',', dialog.SelectedBroadcastIds));
    }

    private void RemoveSelectedFriendButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is FriendForm friend)
            SelectedFriends.Remove(friend);
    }

    private void ReloadFriends()
    {
        var selectedIds = SelectedFriends.Select(friend => friend.BroadcastId).ToHashSet(StringComparer.Ordinal);
        ReplaceSelectedFriends(selectedIds);
    }

    private void RestoreSelectedFriends(string idsText)
    {
        var ids = idsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        ReplaceSelectedFriends(ids);
    }

    private void ReplaceSelectedFriends(HashSet<string> selectedIds)
    {
        SelectedFriends.Clear();
        foreach (var item in DAO_User.SelectAllOrderbyLastUser())
        {
            if (!selectedIds.Contains(item.UserId)) continue;
            SelectedFriends.Add(new FriendForm
            {
                BroadcastId = item.UserId,
                UserId = item.LoginId,
                DisplayName = item.DisplayName,
                LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd HH:mm"),
                ProfileImage = FriendPanel.CreateProfileImage(item.ProfielImageUrl)
            });
        }
    }
}
