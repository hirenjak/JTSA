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
            StreamSupportTracker.Changed -= StreamSupportTracker_Changed;
            StreamSupportTracker.Changed += StreamSupportTracker_Changed;
            RefreshSupportLists();
            RaidUserList.Clear();
            var apiResluts = await TwitchHelper.GetStreamingFollowUserAsync();

            var nowTime = DateTime.Now;

            apiResluts = apiResluts.OrderBy(x => x.StartedAt).ToList();
            apiResluts.Reverse();

            foreach (var data in apiResluts)
            {
                
                var timeSpan =  nowTime.ToUniversalTime() - data.StartedAt.ToUniversalTime();

                var ThumbnailUrl = data.ThumbnailUrl.Replace("{width}", "320").Replace("{height}", "180");

                var categoryData = await TwitchHelper.GetCategoryByGameId(data.GameId);
                var StreamGameBoxArtUrl = categoryData?.BoxArtUrl?.Replace("{width}", "32").Replace("{height}", "48");

                // TotalHoursの小数点以下を切り捨てて合計時間（Time部分）を取得
                int totalHours = (int)Math.Floor(timeSpan.TotalHours);

                RaidUserList.Add(new RaidUserForm
                {
                    UserId = data.UserId,
                    UserName = data.UserName,
                    UserLogin = data.UserLogin,
                    StreamTitle = data.Title,
                    GameBoxArtUrl = StreamGameBoxArtUrl,
                    StreamingTime = $"{totalHours}:{timeSpan:mm\\:ss}",
                    ThumbnailUrl = ThumbnailUrl
                });
            }
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

        /// <summary>
        /// レイドユーザーダブルクリック時イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RaidUserListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is RaidUserForm item)
            {
                await TwitchHelper.StreamRaid(item.UserId);
                JTSAHelper.OpenMyTwitchChannel();
            }
        }

        /// <summary>
        /// レイド先ユーザーの配信を既定のブラウザで開く。
        /// </summary>
        private void OpenTwitchChannelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Parent: ContextMenu contextMenu }
                && contextMenu.PlacementTarget is FrameworkElement { DataContext: RaidUserForm item })
            {
                JTSAHelper.OpenTwitchChannel(item.UserLogin);
            }
        }

        #endregion
    }
}
