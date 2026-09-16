using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JTSA.Dao;
using JTSA.Models;

namespace JTSA;

public partial class MainWindow
{
    private const string ObsBrowserRefreshNotificationKey =
        "stream-expansion-obs-overlay-refresh-2026-09-14";
    private sealed record Notice(
        string Key,
        string Title,
        string Message,
        string ActionLabel,
        Func<Task>? Action,
        Action? Dismissed = null);
    private readonly ObservableCollection<Notice> notices = new();
    private readonly Dictionary<string, string> oauthReauthenticationMessages = new();
    private Window? notificationWindow;
    private StackPanel? notificationRows;
    private bool updateCheckStarted;

    private void InitializeNotifications()
    {
        notices.CollectionChanged += (_, _) => RefreshNotifications();
        ShowTodaysCalendarNotification(DateTime.Today);
        ShowObsBrowserRefreshNotification();
        Loaded += async (_, _) =>
        {
            if (updateCheckStarted) return;
            updateCheckStarted = true;
            await App.UpdateCheck(this);
        };
    }

    private void ShowObsBrowserRefreshNotification()
    {
        if (DAO_AppNotificationReceipt.IsAcknowledged(ObsBrowserRefreshNotificationKey)) return;

        ShowNotification(
            ObsBrowserRefreshNotificationKey,
            "配信拡張用OBSオーバーレイの更新が必要です",
            "今回のアップデート内容を反映するため、OBSに登録している配信拡張用ブラウザソースをリフレッシュしてください。この案内は確認後、再表示されません。",
            dismissed: () => DAO_AppNotificationReceipt.Acknowledge(ObsBrowserRefreshNotificationKey));
    }

    private void ShowTodaysCalendarNotification(DateTime today)
    {
        var entries = DAO_Calendar.SelectByDate(today);
        if (entries.Count == 0) return;

        var message = string.Join("\n", entries.Select(FormatCalendarEntryForNotification));
        ShowNotification(
            "calendar-today",
            $"本日の予定（{entries.Count}件）",
            message,
            "カレンダーを開く",
            () =>
            {
                CalendarPanel.RefreshSelectedDate();
                MainTabControl.SelectedItem = CalendarTabItem;
                notificationWindow?.Close();
                return Task.CompletedTask;
            });
    }

    private static string FormatCalendarEntryForNotification(T_CalendarEntry entry)
    {
        var time = entry.StartTime == TimeSpan.Zero
            ? "時刻未設定"
            : entry.StartTime.ToString(@"hh\:mm");
        var content = string.IsNullOrWhiteSpace(entry.Content) ? "（予定内容なし）" : entry.Content.Trim();
        return $"{time}  {content}";
    }

    public void ShowNotification(
        string key,
        string title,
        string message,
        string actionLabel = "",
        Func<Task>? action = null,
        Action? dismissed = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => ShowNotification(key, title, message, actionLabel, action, dismissed)));
            return;
        }
        var existing = notices.FirstOrDefault(x => x.Key == key);
        if (existing != null) notices[notices.IndexOf(existing)] = new(key, title, message, actionLabel, action, dismissed);
        else notices.Add(new(key, title, message, actionLabel, action, dismissed));
    }

    public void RemoveNotification(string key)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => RemoveNotification(key))); return; }
        var notice = notices.FirstOrDefault(x => x.Key == key);
        if (notice != null) notices.Remove(notice);
    }

    public void ShowOAuthReauthenticationNotification(string sourceKey, string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
                ShowOAuthReauthenticationNotification(sourceKey, message)));
            return;
        }

        oauthReauthenticationMessages[sourceKey] = message;
        RefreshOAuthReauthenticationNotification();
    }

    public void RemoveOAuthReauthenticationNotification(string sourceKey)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
                RemoveOAuthReauthenticationNotification(sourceKey)));
            return;
        }

        oauthReauthenticationMessages.Remove(sourceKey);
        RefreshOAuthReauthenticationNotification();
    }

    private void RefreshOAuthReauthenticationNotification()
    {
        const string notificationKey = "oauth-reauthentication";
        if (oauthReauthenticationMessages.Count == 0)
        {
            RemoveNotification(notificationKey);
            return;
        }

        var messages = oauthReauthenticationMessages.Values.Distinct().ToList();
        ShowNotification(
            notificationKey,
            "Twitchの再認証が必要です",
            string.Join("\n", messages),
            "設定を開く",
            () =>
            {
                OpenToolPanelWindow(SettingsPanelHost, SettingPanel, "設定");
                notificationWindow?.Close();
                return Task.CompletedTask;
            });
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (notificationWindow != null) { notificationWindow.Activate(); return; }
        notificationRows = new StackPanel { Margin = new Thickness(16) };
        notificationWindow = new ToolPanelWindow(
            "通知一覧",
            new ScrollViewer
            {
                Content = notificationRows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(48, 48, 48))
            })
        {
            Owner = this, Width = 480, Height = 420, MinWidth = 340, MinHeight = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Foreground = Brushes.White
        };
        notificationWindow.Closed += (_, _) => { notificationWindow = null; notificationRows = null; };
        RefreshNotifications();
        notificationWindow.Show();
    }

    private void RefreshNotifications()
    {
        NotificationButton.Visibility = notices.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        NotificationButton.Content = notices.Count > 99 ? "99+" : notices.Count.ToString();
        if (notificationRows == null) return;
        notificationRows.Children.Clear();
        if (notices.Count == 0) notificationRows.Children.Add(new TextBlock { Text = "通知はありません", Foreground = Brushes.White });
        foreach (var notice in notices)
        {
            var row = new StackPanel();
            row.Children.Add(new TextBlock { Text = notice.Title, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGoldenrodYellow, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = notice.Message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            if (notice.Action != null)
            {
                var action = new Button
                {
                    Content = notice.ActionLabel,
                    Padding = new Thickness(12, 5, 12, 5),
                    Background = new SolidColorBrush(Color.FromRgb(40, 86, 83)),
                    BorderBrush = Brushes.LightSeaGreen,
                    Foreground = Brushes.White
                };
                action.Click += async (_, _) =>
                {
                    action.IsEnabled = false;
                    try { await notice.Action(); }
                    catch (Exception ex) { ShowNotification(notice.Key, notice.Title, "処理に失敗しました。再試行してください。\n" + ex.Message, notice.ActionLabel, notice.Action); }
                    finally { action.IsEnabled = true; }
                };
                buttons.Children.Add(action);
            }
            var dismiss = new Button
            {
                Content = "通知を消す",
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 5, 12, 5),
                Background = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(119, 119, 119)),
                Foreground = Brushes.White
            };
            dismiss.Click += (_, _) =>
            {
                notice.Dismissed?.Invoke();
                RemoveNotification(notice.Key);
            };
            buttons.Children.Add(dismiss);
            row.Children.Add(buttons);
            notificationRows.Children.Add(new Border
            {
                Child = row,
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            });
        }
    }
}
