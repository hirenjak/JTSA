using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JTSA.Panels
{
    /// <summary>
    /// 登録フレンド管理パネル
    /// </summary>
    public partial class FriendPanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<FriendForm> FriendFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<FriendForm> SelectedFriendFormList { get; } = new();

        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FriendPanel()
        {
            DataContext = this;

            InitializeComponent();

            Loaded += FriendPanel_Loaded;
        }

        private void FriendPanel_Loaded(object sender, RoutedEventArgs e)
        {
            var settingPrefixWord = DAO_Setting.SelectOneById(DAO_Setting.SettingName.FriendPrefixWord);
            if (settingPrefixWord != null)
            {
                FriendPrefixWordTextBox.Text = settingPrefixWord.Value;
            }

            ReloadFriend();
        }

        public void AddFriend(
            string? twitchUserId,
            string loginId,
            string displayName,
            string? profileImageUrl,
            string streamingPlatform,
            string streamingUrl,
            string? existingUserId = null)
        {
            DAO_User.InsertUpdateFriend(
                twitchUserId,
                loginId.Trim(),
                displayName.Trim(),
                string.IsNullOrWhiteSpace(profileImageUrl) ? null : profileImageUrl.Trim(),
                streamingPlatform.Trim(),
                streamingUrl.Trim(),
                existingUserId);
            ReloadFriend();
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
            if ((sender as Button)?.DataContext is FriendForm item)
            {
                DAO_User.Delete(item.BroadcastId);
            }

            ReloadFriend();
            e.Handled = true;
        }


        /// <summary>
        /// 追加ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FriendAddButton_Click(object sender, RoutedEventArgs e)
        {
            string userId = FriendAddTextBox.Text;
            await AddFriendAsync(userId);
        }

        public void UseSeparateFriendRegistrationWindow()
        {
            InlineFriendRegistrationPanel.Visibility = Visibility.Collapsed;
            OpenFriendRegistrationButton.Visibility = Visibility.Visible;
        }

        private void OpenFriendRegistrationButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new FriendRegistrationWindow(this)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
            e.Handled = true;
        }

        private void FriendEditButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not FriendForm friend) return;

            var window = new FriendRegistrationWindow(this, friend.BroadcastId)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
            e.Handled = true;
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FriendListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FriendListBox.SelectedItem is FriendForm selectedItem)
            {
                if (!SelectedFriendFormList.Any(x => x.BroadcastId == selectedItem.BroadcastId))
                {
                    SelectedFriendFormList.Add(selectedItem);
                }
            }

            UpdateTitlePreview();

            // 選択状態を解除
            FriendListBox.SelectedIndex = -1;
        }


        /// <summary>
        /// フレンドDB追加処理
        /// </summary>
        /// <param name="title"></param>
        public async Task AddFriendAsync(string userId)
        {
            // 配信者情報取得
            var streamerInfo = await TwitchHelper.GetBroadcasterIdAsync(userId);

            // データチェック
            if (streamerInfo == null) return;
            if (string.IsNullOrWhiteSpace(streamerInfo.UserId)) return;

            var profielImage = await JTSAHelper.LoadBitmapAsync(streamerInfo.ProfileImageUrl);

            // データ作成
            var isnertData = new M_User
            {
                UserId = streamerInfo.UserId,
                LoginId = streamerInfo.Login,
                DisplayName = streamerInfo.DisplayName,
                ProfielImageUrl = JTSAHelper.BitmapToBase64(profielImage),
                StreamingPlatform = "Twitch",
                StreamingUrl = $"https://www.twitch.tv/{streamerInfo.Login}",
                IsFriend = true,
                LastUsedDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            };

            // 挿入処理
            if (!DAO_User.Insert(isnertData))
            {
                // チャットのプロフィールキャッシュとして登録済みの場合はフレンド化する。
                DAO_User.MarkAsFriend(streamerInfo.UserId);
            }

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
            FriendFormList.Clear();

            // プロフィールキャッシュを除き、明示的に登録されたフレンドのみ取得
            var records = DAO_User.SelectAllOrderbyLastUser();

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                FriendFormList.Add(new()
                {
                    BroadcastId = item.UserId,
                    UserId = item.LoginId,
                    DisplayName = item.DisplayName,
                    StreamingPlatform = item.StreamingPlatform,
                    StreamingUrl = item.StreamingUrl,
                    LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm"),
                    ProfileImage = CreateProfileImage(item.ProfielImageUrl)
                });
            }

            mainWindow.StatusTextBlock.Text = "フレンドリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }

        /// <summary>カレンダー予定に保存されたフレンド選択を復元する。</summary>
        public void SelectFriends(IEnumerable<string> broadcastIds)
        {
            ReloadFriend();
            var ids = broadcastIds.ToHashSet(StringComparer.Ordinal);
            SelectedFriendFormList.Clear();
            foreach (var friend in FriendFormList.Where(friend => ids.Contains(friend.BroadcastId)))
            {
                SelectedFriendFormList.Add(friend);
            }

            UpdateTitlePreview();
        }

        internal static ImageSource? CreateProfileImage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return CachedImageConverter.GetImage(value, 96);
            }

            try
            {
                return JTSAHelper.Base64ToBitmap(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectedFriendButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FriendForm item)
            {
                SelectedFriendFormList.Remove(item);
            }

            UpdateTitlePreview();
            e.Handled = true;
        }


        /// <summary>
        /// 
        /// </summary>
        public void UpdateTitlePreview()
        {
            mainWindow.CurrentTitleTextUpdate();
        }

        private void FriendPrefixWordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.FriendPrefixWord,
                FriendPrefixWordTextBox.Text
            );
        }
    }
}
