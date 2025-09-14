using JTSA.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>
    /// FriendSidePanel.xaml の相互作用ロジック
    /// </summary>
    public partial class FriendSidePanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        public FriendSidePanel()
        {
            InitializeComponent();
        }


        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FriendSerchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO：フレンド検索処理追加
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FriendDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is FriendTagForm item)
            {
                M_Friend.Delete(item.BroadcastId);
            }

            ReloadFriend();
        }


        /// <summary>
        /// 追加ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FriendAddButton_Click(object sender, RoutedEventArgs e)
        {
            String userId = FriendAddTextBox.Text;
            AddFriendAsync(userId);
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FriendListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FriendListBox.SelectedItem is FriendTagForm selectedItem)
            {
                mainWindow.InsertTextAtCaret(" @" + selectedItem.UserId + " ");
            }

            // 選択状態を解除
            FriendListBox.SelectedIndex = -1;
        }


        /// <summary>
        /// フレンドDB追加処理
        /// </summary>
        /// <param name="title"></param>
        private async void AddFriendAsync(String userId)
        {
            // 配信者情報取得
            var streamerInfo = await TwitchHelper.GetBroadcasterIdAsync(userId);

            // データチェック
            if (streamerInfo == null) return;
            if (string.IsNullOrWhiteSpace(streamerInfo.BroadcastId)) return;

            using var db = new AppDbContext();

            // データ作成
            var isnertData = new M_Friend
            {
                BroadcastId = streamerInfo.BroadcastId,
                UserId = streamerInfo.UserId,
                DisplayName = streamerInfo.DisplayName,
                CountSelected = 0,
                SortNumber = 0,
                IsDeleted = false,
                LastUseDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdateDateTime = DateTime.Now
            };

            // 挿入処理
            M_Friend.Insert(db, isnertData);

            // 再読み込み処理
            ReloadFriend();
        }


        /// <summary>
        /// 読み込み処理：フレンド
        /// </summary>
        public void ReloadFriend()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            mainWindow.FriendFormList.Clear();

            // データの取得
            var records = M_Friend.SelectAllOrderbyLastUser(db);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                mainWindow.FriendFormList.Add(new()
                {
                    BroadcastId = item.BroadcastId,
                    UserId = item.UserId,
                    DisplayName = item.DisplayName,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }

            mainWindow.StatusTextBlock.Text = "フレンドリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}
