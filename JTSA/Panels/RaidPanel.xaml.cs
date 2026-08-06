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
            DataContext = this;

            InitializeComponent();

            Loaded += RaidPanel_Loaded;
        }

        private async void RaidPanel_Loaded(object sender, RoutedEventArgs e)
        {
            RaidUserList.Clear();
            var apiResluts = await TwitchHelper.GetStreamingFollowUserAsync();

            var nowTime = DateTime.Now;

            foreach (var data in apiResluts)
            {
                var timeSpan = (nowTime - data.StartedAt);

                RaidUserList.Add(new RaidUserForm
                {
                    UserId = data.UserId,
                    UserName = data.UserName,
                    UserLogin = data.UserLogin,
                    StreamTitle = data.Title,
                    StreamGameId = data.GameId,
                    StreamingTime = timeSpan.Hours + "時間" + timeSpan.Minutes + "分" + +timeSpan.Seconds + "秒",
                    ThumbnailUrl = data.ThumbnailUrl
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
        private void RaidButton_Click(object sender, RoutedEventArgs e)
        {

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
