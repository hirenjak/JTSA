using JTSA.Forms;
using JTSA.Utility;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JTSA.Panels
{
    /// <summary>
    /// 配信時アプリ配置パネル
    /// </summary>
    public partial class RaidPanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;


        /// <summary> 登録アプリリスト </summary>
        public ObservableCollection<RaidUserForm> RaidUserList { get; set; } = new();
        public ObservableCollection<BitsUserForm> BitsUserList { get; } = new();
        public ObservableCollection<SubscribeUserForm> SubscribeUserList { get; } = new();
        public ObservableCollection<RaidedUserForm> RaidedUserList { get; } = new();
        public ObservableCollection<FollowUserForm> FollowUserList { get; } = new();
        private readonly StreamExpansionService streamExpansionService = new();
        private Task? refreshRaidUsersTask;

        private const string TestBitsUserPrefix = "テストBitsユーザー";
        private const string TestSubscribeUserPrefix = "テストサブスクユーザー";
        private const string TestGiftUserPrefix = "テストサブギフユーザー";
        private const string TestRaidUserPrefix = "テストレイドユーザー";
        private const string TestFollowUserPrefix = "テストフォローユーザー";
        private int testBitsUserNumber;
        private int testSubscribeUserNumber;
        private int testGiftUserNumber;
        private int testRaidUserNumber;
        private int testFollowUserNumber;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RaidPanel()
        {
            InitializeComponent();
            DataContext = this;


            #region イベントハンドラ

            Loaded += RaidPanel_Loaded;
            Unloaded += RaidPanel_Unloaded;

            RaidUserListBox.MouseDoubleClick += RaidUserListBox_MouseDoubleClick;
            StreamSupportTracker.Changed += StreamSupportTracker_Changed;

            #endregion
        }


        /// <summary>
        /// パネル読み込み時イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RaidPanel_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshRaidUsersAsync();
        }

        public Task RefreshRaidUsersAsync()
        {
            // タブのLoadedとアカウント切替が重なった場合は、同じ取得処理を共有する。
            if (refreshRaidUsersTask is { IsCompleted: false })
                return refreshRaidUsersTask;

            refreshRaidUsersTask = RefreshRaidUsersCoreAsync();
            return refreshRaidUsersTask;
        }

        private async Task RefreshRaidUsersCoreAsync()
        {
            StreamSupportTracker.Changed -= StreamSupportTracker_Changed;
            StreamSupportTracker.Changed += StreamSupportTracker_Changed;
            RefreshSupportLists();
            var target = await mainWindow.GetSelectedTargetAccountAsync();
            if (target is null)
                return;
            var apiResluts = await TwitchHelper.GetStreamingFollowUserAsync(
                target.Value.Account.BroadcasterId,
                target.Value.AccessToken);
            if (apiResluts is null)
                return;

            var nowTime = DateTime.Now;

            apiResluts = apiResluts
                .DistinctBy(x => x.UserId)
                .OrderByDescending(x => x.StartedAt)
                .ToList();

            var refreshedRaidUsers = new List<RaidUserForm>();

            foreach (var data in apiResluts)
            {
                
                var timeSpan =  nowTime.ToUniversalTime() - data.StartedAt.ToUniversalTime();

                var ThumbnailUrl = data.ThumbnailUrl.Replace("{width}", "320").Replace("{height}", "180");

                var categoryData = await TwitchHelper.GetCategoryByGameId(data.GameId);
                var StreamGameBoxArtUrl = categoryData?.BoxArtUrl?.Replace("{width}", "32").Replace("{height}", "48");

                // TotalHoursの小数点以下を切り捨てて合計時間（Time部分）を取得
                int totalHours = (int)Math.Floor(timeSpan.TotalHours);

                refreshedRaidUsers.Add(new RaidUserForm
                {
                    UserId = data.UserId,
                    UserName = data.UserName,
                    UserLogin = data.UserLogin,
                    StreamTitle = data.Title,
                    GameBoxArtUrl = StreamGameBoxArtUrl ?? string.Empty,
                    StreamingTime = $"{totalHours}:{timeSpan:mm\\:ss}",
                    ThumbnailUrl = ThumbnailUrl
                });
            }

            ReplaceItems(RaidUserList, refreshedRaidUsers);
        }

        private void StreamSupportTracker_Changed()
        {
            Dispatcher.InvokeAsync(RefreshSupportLists);
        }

        private void RefreshSupportLists()
        {
            ReplaceItems(BitsUserList, StreamSupportTracker.BitsUsers);
            ReplaceItems(SubscribeUserList, StreamSupportTracker.SubscribeUsers);
            ReplaceItems(RaidedUserList, StreamSupportTracker.RaidedUsers);
            ReplaceItems(FollowUserList, StreamSupportTracker.FollowUsers);
        }

        private void TestSupportPanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var isOpening = TestSupportButtonBorder.Visibility != Visibility.Visible;
            TestSupportButtonBorder.Visibility = isOpening
                ? Visibility.Visible
                : Visibility.Collapsed;
            TestSupportPanelToggleButton.Background = new System.Windows.Media.SolidColorBrush(
                isOpening
                    ? System.Windows.Media.Color.FromRgb(70, 70, 70)
                    : System.Windows.Media.Color.FromRgb(86, 86, 86));
            TestSupportPanelToggleButton.BorderBrush = new System.Windows.Media.SolidColorBrush(
                isOpening
                    ? System.Windows.Media.Color.FromRgb(85, 85, 85)
                    : System.Windows.Media.Color.FromRgb(119, 119, 119));
            TestSupportPanelToggleButton.BorderThickness = isOpening
                ? new Thickness(1, 1, 1, 0)
                : new Thickness(1);
        }

        private async void AddTestBitsButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = $"{TestBitsUserPrefix}{++testBitsUserNumber}";
            StreamSupportTracker.AddBits(userName, 100);
            await streamExpansionService.HandleAsync(StreamExpansionTriggerType.Bits, "100");
        }

        private async void AddTestSubscribeButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = $"{TestSubscribeUserPrefix}{++testSubscribeUserNumber}";
            StreamSupportTracker.AddSubscription(userName, 1, "1");
            await streamExpansionService.HandleAsync(StreamExpansionTriggerType.Subscribe, string.Empty);
        }

        private async void AddTestGiftButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = $"{TestGiftUserPrefix}{++testGiftUserNumber}";
            StreamSupportTracker.AddGiftSubscription(userName, "1");
            await streamExpansionService.HandleAsync(StreamExpansionTriggerType.Subscribe, string.Empty);
        }

        private async void AddTestRaidButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = $"{TestRaidUserPrefix}{++testRaidUserNumber}";
            StreamSupportTracker.AddRaid(userName, 10);
            await streamExpansionService.HandleAsync(StreamExpansionTriggerType.Raid, userName);
        }

        private async void AddTestFollowButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = $"{TestFollowUserPrefix}{++testFollowUserNumber}";
            StreamSupportTracker.AddFollow(userName);
            await streamExpansionService.HandleAsync(StreamExpansionTriggerType.Follow, userName);
        }

        private void ClearTestSupportButton_Click(object sender, RoutedEventArgs e)
        {
            StreamSupportTracker.RemoveUsersByPrefixes(
                TestBitsUserPrefix,
                TestSubscribeUserPrefix,
                TestGiftUserPrefix,
                TestRaidUserPrefix,
                TestFollowUserPrefix);
            testBitsUserNumber = 0;
            testSubscribeUserNumber = 0;
            testGiftUserNumber = 0;
            testRaidUserNumber = 0;
            testFollowUserNumber = 0;
        }

        private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (var item in source) target.Add(item);
        }

        private void RaidPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            StreamSupportTracker.Changed -= StreamSupportTracker_Changed;
        }

        #region ==================== レイド関連イベントハンドラ ====================

        private async void ExecuteRaidButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not RaidUserForm item)
                return;

            await ExecuteRaidAsync(item);
        }

        private async void RaidUserListBox_MouseDoubleClick(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            for (var element = e.OriginalSource as DependencyObject; element is not null;
                 element = System.Windows.Media.VisualTreeHelper.GetParent(element))
            {
                if (element is Button)
                    return;
            }

            if ((sender as ListBox)?.SelectedItem is RaidUserForm item)
                await ExecuteRaidAsync(item);
        }

        private async Task ExecuteRaidAsync(RaidUserForm item)
        {
            if (MessageBox.Show(
                    $"{item.UserName} へレイドを実行しますか？",
                    "レイド実行",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var target = await mainWindow.GetSelectedTargetAccountAsync();
            if (target is null)
            {
                MessageBox.Show("レイド元のTwitchアカウントを取得できませんでした。", "レイド実行");
                return;
            }

            var createdAt = await TwitchHelper.StreamRaid(
                target.Value.Account.BroadcasterId,
                item.UserId,
                target.Value.AccessToken);
            if (!createdAt.HasValue)
            {
                MessageBox.Show(
                    "レイドを開始できませんでした。AppLogで詳細を確認してください。\n" +
                    "このエラーは、相手側がレイドを拒否している場合にも表示されます。",
                    "レイド実行",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void OpenTwitchChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is RaidUserForm item)
                JTSAHelper.OpenTwitchChannel(item.UserLogin);
        }

        #endregion
    }
}
