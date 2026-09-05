using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JTSA;

public partial class MainWindow
{
    private sealed record Notice(string Key, string Title, string Message, string ActionLabel, Func<Task>? Action);
    private readonly ObservableCollection<Notice> notices = new();
    private Window? notificationWindow;
    private StackPanel? notificationRows;
    private bool updateCheckStarted;

    private void InitializeNotifications()
    {
        notices.CollectionChanged += (_, _) => RefreshNotifications();
        Loaded += async (_, _) =>
        {
            if (updateCheckStarted) return;
            updateCheckStarted = true;
            await App.UpdateCheck(this);
        };
    }

    public void ShowNotification(string key, string title, string message, string actionLabel = "", Func<Task>? action = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => ShowNotification(key, title, message, actionLabel, action)));
            return;
        }
        var existing = notices.FirstOrDefault(x => x.Key == key);
        if (existing != null) notices[notices.IndexOf(existing)] = new(key, title, message, actionLabel, action);
        else notices.Add(new(key, title, message, actionLabel, action));
    }

    public void RemoveNotification(string key)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => RemoveNotification(key))); return; }
        var notice = notices.FirstOrDefault(x => x.Key == key);
        if (notice != null) notices.Remove(notice);
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (notificationWindow != null) { notificationWindow.Activate(); return; }
        notificationRows = new StackPanel { Margin = new Thickness(16) };
        notificationWindow = new Window
        {
            Title = "通知一覧", Owner = this, Width = 480, Height = 420, MinWidth = 340, MinHeight = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(48, 48, 48)), Foreground = Brushes.White,
            Content = new ScrollViewer { Content = notificationRows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        notificationWindow.Closed += (_, _) => { notificationWindow = null; notificationRows = null; };
        RefreshNotifications();
        notificationWindow.Show();
    }

    private void RefreshNotifications()
    {
        NotificationButton.Visibility = notices.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        NotificationButton.Content = $"通知 ({notices.Count})";
        if (notificationRows == null) return;
        notificationRows.Children.Clear();
        if (notices.Count == 0) notificationRows.Children.Add(new TextBlock { Text = "通知はありません", Foreground = Brushes.White });
        foreach (var notice in notices)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            row.Children.Add(new TextBlock { Text = notice.Title, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGoldenrodYellow, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = notice.Message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            if (notice.Action != null)
            {
                var action = new Button { Content = notice.ActionLabel, Padding = new Thickness(10, 4, 10, 4) };
                action.Click += async (_, _) =>
                {
                    action.IsEnabled = false;
                    try { await notice.Action(); }
                    catch (Exception ex) { ShowNotification(notice.Key, notice.Title, "処理に失敗しました。再試行してください。\n" + ex.Message, notice.ActionLabel, notice.Action); }
                    finally { action.IsEnabled = true; }
                };
                buttons.Children.Add(action);
            }
            var dismiss = new Button { Content = "通知を消す", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4) };
            dismiss.Click += (_, _) => RemoveNotification(notice.Key);
            buttons.Children.Add(dismiss);
            row.Children.Add(buttons);
            notificationRows.Children.Add(row);
        }
    }
}
