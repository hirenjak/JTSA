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

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RaidPanel()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += RaidPanel_Loaded;
        }

        private async void RaidPanel_Loaded(object sender, RoutedEventArgs e)
        {
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


        /// <summary>
        /// 配信視聴ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StreamShowButton_Click(object sender, RoutedEventArgs e)
        {

        }


        /// <summary>
        /// レイド開始ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RaidButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RaidUserForm item)
            {
                 await TwitchHelper.StreamRaid(item.UserId);
            }
        }


        /// <summary>
        /// レイドユーザーリストボックスクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RaidUserListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }


        /// <summary>
        /// レイドユーザーリストボックス選択変更時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RaidUserListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SubscribeUserListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void SubscribeUserListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
