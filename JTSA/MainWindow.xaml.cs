using JTSA.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace JTSA
{
    public partial class MainWindow : Window
    {
        /// <summary>  </summary>
        public ObservableCollection<TitleTextForm> TitleTextFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<TitleTagForm> TitleTagFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<FriendTagForm> FriendFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> CategoryFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> SearchCategoryFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<TitleTextForm> SaveTitleTextFormList { get; } = new();


        /// <summary> メインウィンドウの基本幅 </summary>
        private const int MainWindowBaseWidth = 654;

        /// <summary> サイドパネルの幅 </summary>
        private const int SidePanelWidth = 320;


        private EditTitleTextForm editTitleTextForm;

        private System.Windows.Threading.DispatcherTimer categorySearchDebounceTimer;
        private string lastCategorySearchText = "";


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            categorySearchDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            categorySearchDebounceTimer.Interval = TimeSpan.FromSeconds(1);
            categorySearchDebounceTimer.Tick += CategorySearchDebounceTimer_Tick;

            DataContext = this;
            
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }

            this.Width = MainWindowBaseWidth;
            AllSidePanelClose();

            // イベント登録
            this.Loaded += MainWindow_LoadedAsync;
        }


        /// <summary>
        /// コンストラクタ終了時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MainWindow_LoadedAsync(object sender, RoutedEventArgs e)
        {
            // Loading画面表示（※MainWindow_Loaded終わりまで表示）
            LoadScreen.Visibility = Visibility.Visible;

            // クライアントID存在チェック
            if (string.IsNullOrEmpty(TwitchHelper.ClientID)) return;

            M_Setting tempSettingObj;

            // ユーザー名取得確認
            tempSettingObj = M_Setting.SelectOneById(M_Setting.SettingName.UserName);
            if (tempSettingObj == null || String.IsNullOrEmpty(tempSettingObj.Value)) return;

            Utility.UserName = tempSettingObj.Value;

            // リフレッシュトークンからアクセストークンを再取得
            var settingUserName = M_Setting.SelectOneById(M_Setting.SettingName.RefreshToken);
            if (settingUserName == null || String.IsNullOrEmpty(settingUserName.Value)) return;


            UserName_TextBox.Text = Utility.UserName;

            // アクセストークン存在チェック
            if (!string.IsNullOrEmpty(TwitchHelper.AccessToken))
            {
                AccessToken_TextBlock.Text = "OK!";
            }
            else
            {
                AccessToken_TextBlock.Text = "NG";
                return;
            }




            await StreamerDataSet();
            
            LoadScreen.Visibility = Visibility.Collapsed;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="userName"></param>
        private async Task StreamerDataSet()
        {
            var streamerInfo = await TwitchHelper.GetBroadcasterIdAsync();

            if (streamerInfo != null && !String.IsNullOrEmpty(streamerInfo.BroadcastId))
            {
                TwitchHelper.BroadcasterId = streamerInfo.BroadcastId;

                StatusTextBlock.Text = $"broadcaster_id取得成功: {TwitchHelper.BroadcasterId}";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                StatusTextBlock.Text = "broadcaster_idの取得に失敗しました敗しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (!String.IsNullOrEmpty(TwitchHelper.AccessToken))
            {
                // タイトル取得処理
                GetTwitchTitle();
                await WaitForTargetStringAsync(CurrentTitleTextBlock.Text);
                TitleEditTextBox.Text = CurrentTitleTextBlock.Text;

                // カテゴリID処理
                var CategoryId = await TwitchHelper.GetTwitchCategoryByBroadcast() ?? "";

                // カテゴリ名取得処理
                var category = await TwitchHelper.GetGamesByGameId(CategoryId);
                editTitleTextForm = new EditTitleTextForm()
                {
                    Content = TitleEditTextBox.Text,
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    BoxArtUrl = category.BoxArtUrl
                };

                ReloadCategory();

                SetEditTitleTextForm();
            }

            // リスト読み込み処理
            ReloadTitleText();
            ReloadTitleTag();
            ReloadFriendTag();
            ReloadSaveTitleText();
        }


        #region =============== Tiwthc：OAuth認証 ===============

        /// <summary>
        /// OAuth認証ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OAuthButton_Click(object sender, RoutedEventArgs e)
        {
            // Loading画面表示
            LoadScreen.Visibility = Visibility.Visible;

            Utility.UserName = UserName_TextBox.Text.Trim();

            var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync() ?? new();

            // 認証URLとユーザーコードをユーザーに表示
            LoadPanelSubTextBox.Text = deviceCodeResponse.user_code;

            LoadSubPanel.Visibility = Visibility.Visible;

            // 認証ページを自動で開く（オプション）
            Process.Start(new ProcessStartInfo(deviceCodeResponse.verification_uri + $"user_code={Utility.UserName}") { UseShellExecute = true });

            // ポーリングでトークン取得
            var accessTokenResponse = await TwitchHelper.PollDeviceTokenAsync(deviceCodeResponse.device_code, deviceCodeResponse.interval, deviceCodeResponse.expires_in);
            
            
            if (!string.IsNullOrEmpty(accessTokenResponse.accessToken))
            {
                TwitchHelper.AccessToken = accessTokenResponse.accessToken;

                StatusTextBlock.Text = "アクセストークンを取得しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
                AccessToken_TextBlock.Text = "OK!";
            }
            else
            {
                StatusTextBlock.Text = "アクセストークンの取得に失敗しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
                AccessToken_TextBlock.Text = "NG";
            }

            // --- 設定情報保存処理 ---
            M_Setting.InsertUpdate(new M_Setting
            {
                Name = (int)M_Setting.SettingName.UserName,
                Value = Utility.UserName,
            });

            M_Setting.InsertUpdate(new M_Setting
            {
                Name = (int)M_Setting.SettingName.RefreshToken,
                Value = accessTokenResponse.refreshToken,
            });

            M_Setting.InsertUpdate(new M_Setting
            {
                Name = (int)M_Setting.SettingName.ExpiresIn,
                Value = accessTokenResponse.expiresIn.ToString(),
            });

            await StreamerDataSet();

            LoadScreen.Visibility = Visibility.Collapsed;
        }

        #endregion


        #region =============== リストデータ更新処理 ===============

        /// <summary>
        /// 読み込み処理：タイトルテキスト
        /// </summary>
        private void ReloadTitleText()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            TitleTextFormList.Clear();

            // データの取得
            var records = M_TitleText.SelectAllOrderbyLastUser(db);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                TitleTextFormList.Add(new()
                {
                    Id = item.Id,
                    Content = item.Content,
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }
        }


        /// <summary>
        /// 読み込み処理：タイトルタグ
        /// </summary>
        private void ReloadTitleTag()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            TitleTagFormList.Clear();

            // データの取得
            var records = M_TitleTag.SelectAllOrderbyLastUser(db);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                TitleTagFormList.Add(new()
                {
                    Id = item.Id,
                    DisplayName = item.DisplayName,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }
        }


        /// <summary>
        /// 読み込み処理：フレンド
        /// </summary>
        private void ReloadFriendTag()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            FriendFormList.Clear();

            // データの取得
            var records = M_Friend.SelectAllOrderbyLastUser(db);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                FriendFormList.Add(new()
                {
                    BroadcastId = item.BroadcastId,
                    UserId = item.UserId,
                    DisplayName = item.DisplayName,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }
        }


        /// <summary>
        /// 読み込み処理：カテゴリ
        /// </summary>
        private void ReloadCategory()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            CategoryFormList.Clear();

            // データの取得
            var records = M_Category.SelectAllOrderbyLastUser(db);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                CategoryFormList.Add(new()
                {
                    CategoryId = item.CategoryId,
                    DisplayName = item.DisplayName,
                    BoxArtUrl = item.BoxArtUrl,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }
        }


        /// <summary>
        /// 読込処理：保存タイトルテキスト
        /// </summary>
        private void ReloadSaveTitleText()
        {
            // DB接続と初期化処理
            SaveTitleTextFormList.Clear();

            // データの取得
            var records = M_TitleText.SelectSaveDataOrderbyLastUser();

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                SaveTitleTextFormList.Add(new()
                {
                    Id = item.Id,
                    Content = item.Content,
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }
        }


        /// <summary>
        /// 読込処理：検索カテゴリ
        /// </summary>
        private async void ReloadSearchCategory(String searchText)
        {
            // 初期化処理
            SearchCategoryFormList.Clear();

            // データの取得
            var results = await TwitchHelper.SearchCategoriesByGameNameAsync(searchText);

            // 画面データ入れ換え処理
            foreach (var item in results)
            {
                SearchCategoryFormList.Add(new()
                {
                    CategoryId = item.Id,
                    DisplayName = item.Name,
                    BoxArtUrl = item.BoxArtUrl,
                    LastUsedDate = ""
                });
            }
        }
        
        #endregion


        #region =============== リストデータ追加処理 ===============

        /// <summary>
        /// タイトルテキスト：追加処理
        /// </summary>
        /// <param name="title"></param>
        private void AddTitleText(String content, String categoryId, String categoryName)
        {
            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (String.IsNullOrWhiteSpace(content)) return;

            // データ作成
            var isnertData = new M_TitleText
            {
                Content = content,
                CategoryId = categoryId,
                CategoryName = categoryName,
                CountSelected = 0,
                SortNumber = 9999,
                IsDeleted = false,
                LastUseDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdateDateTime = DateTime.Now
            };

            // 挿入処理
            DisplayLog(
               M_TitleText.Insert(isnertData),
                "データを追加しました。",
                "既にデータが存在します。"
            );

            // 再読み込み処理
            ReloadTitleText();
        }


        /// <summary>
        /// タイトルタグテーブル：挿入処理
        /// </summary>
        /// <param name="title"></param>
        private void AddTitleTag(string displayName)
        {
            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (string.IsNullOrWhiteSpace(displayName)) return;

            // データ作成
            var isnertData = new M_TitleTag
            {
                DisplayName = displayName,
                CountSelected = 0,
                SortNumber = 0,
                IsDeleted = false,
                LastUseDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdateDateTime = DateTime.Now
            };

            // 挿入処理
            DisplayLog(
                M_TitleTag.Insert(isnertData),
                "データを追加しました。",
                "既にデータが存在します。"
            );

            // 再読み込み処理
            ReloadTitleTag();
        }


        /// <summary>
        /// カテゴリテーブル：挿入更新処理
        /// </summary>
        /// <param name="title"></param>
        private void AddCategory(String gameId, String displayName, String boxArtUrl)
        {
            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (string.IsNullOrWhiteSpace(displayName)) return;

            // データ作成
            var isnertData = new M_Category
            {
                CategoryId = gameId,
                DisplayName = displayName,
                BoxArtUrl = boxArtUrl,
                CountSelected = 0,
                SortNumber = 0,
                IsDeleted = false,
                LastUseDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdateDateTime = DateTime.Now
            };

            // 挿入処理
            DisplayLog(
                M_Category.Insert(db, isnertData),
                "データを追加しました。",
                "既にデータが存在します。"
            );
            
            // 再読み込み処理
            ReloadCategory();
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
            ReloadFriendTag();
        }

        #endregion


        #region =============== TwitchAPI通信 ===============

        /// <summary>
        /// タイトルの取得
        /// API：https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GetTwitchTitle()
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TwitchHelper.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", TwitchHelper.ClientID);

            // TwitchAPIから配信タイトルを取得
            var response = await client.GetAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={TwitchHelper.BroadcasterId}");

            // レスポンスの処理
            if (response.IsSuccessStatusCode)
            { // 200 OK

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var title = doc.RootElement.GetProperty("data")[0].GetProperty("title").GetString();

                CurrentTitleTextBlock.Text = title;

                StatusTextBlock.Text = "配信概要を取得しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            { // エラー

                StatusTextBlock.Text = "配信概要に失敗しました。：" + (int)response.StatusCode + "：" + response.StatusCode;
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        #endregion


        #region =============== メインパネル：編集部分 ===============

        /// <summary>
        /// 読み込み処理：編集部分
        /// </summary>
        private void SetEditTitleTextForm()
        {
            TitleEditTextBox.Text = editTitleTextForm.Content;
            SelectCategpryIdTextBlock.Text = editTitleTextForm.CategoryId;
            SelectCategpryNameTextBlock.Text = editTitleTextForm.CategoryName;
        }

        /// <summary>
        /// 送信ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SetTitleButton_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleEditTextBox.Text;
            var categoryId = SelectCategpryIdTextBlock.Text;
            var categoryName = SelectCategpryNameTextBlock.Text;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TwitchHelper.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", TwitchHelper.ClientID);

            var content = new StringContent(
                JsonSerializer.Serialize(new { title = title }),
                Encoding.UTF8, "application/json");

            // TwitchAPIで配信タイトルを更新
            var response = await client.PatchAsync(
                $"https://api.twitch.tv/helix/channels?broadcaster_id={TwitchHelper.BroadcasterId}",
                content);

            // レスポンスの処理
            if (response.IsSuccessStatusCode)
            {// 200 OK

                StatusTextBlock.Text = "配信概要を更新しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;

                // --- 履歴追加処理 ---
                AddTitleText(title, categoryId, categoryName);
            }
            else
            {// エラー

                StatusTextBlock.Text = "更新に失敗しました。：" + (int)response.StatusCode + "：" + response.StatusCode;
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }


            String gameId = SelectCategpryIdTextBlock.Text.Trim();
            bool result = await TwitchHelper.SetCategoryAsync(TwitchHelper.BroadcasterId, TwitchHelper.AccessToken, gameId.ToString());
            if (result)
            {
                StatusTextBlock.Text = "カテゴリを設定しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                StatusTextBlock.Text = "カテゴリの設定に失敗しました。";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleButton_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleEditTextBox.Text;
            var categoryId = SelectCategpryIdTextBlock.Text;
            var categoryName = SelectCategpryNameTextBlock.Text;

            AddTitleText(title, categoryId, categoryName);

            ReloadSaveTitleText();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentTitleTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DisplayLog(
                Utility.CopyClipBoad(CurrentTitleTextBlock.Text),
                "タイトルをクリップボードにコピーしました。",
                "クリップボードへのコピーに失敗しました。"
            );
        }


        /// <summary>
        /// 取得ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GetTitleButton_Click(object sender, RoutedEventArgs e)
        {
            // カテゴリID処理
            String gameId = await TwitchHelper.GetTwitchCategoryByBroadcast() ?? "";

            // カテゴリ名取得処理
            var category = await TwitchHelper.GetGamesByGameId(gameId);

            editTitleTextForm.CategoryId = category.Id;
            editTitleTextForm.CategoryName = category.Name;
            editTitleTextForm.BoxArtUrl = category.BoxArtUrl;
            SetEditTitleTextForm();

            GetTwitchTitle();
        }


        /// <summary>
        /// テキスト編集のカーソル位置にテキストを挿入
        /// </summary>
        /// <param name="insertText"></param>
        private void InsertTextAtCaret(string insertText)
        {
            // TitleEditTextBoxがnullでないことを確認
            if (TitleEditTextBox == null) return;

            int caretIndex = TitleEditTextBox.SelectionStart;
            string original = TitleEditTextBox.Text ?? "";

            // 挿入処理
            TitleEditTextBox.Text =
                original.Substring(0, caretIndex) +
                insertText +
                original.Substring(caretIndex);

            // 挿入後のカーソル位置を調整
            TitleEditTextBox.SelectionStart = caretIndex + insertText.Length;
            TitleEditTextBox.Focus();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            String categoryId = editTitleTextForm.CategoryId;
            String categoryName = editTitleTextForm.CategoryName;
            String boxArtUrl = editTitleTextForm.BoxArtUrl;

            AddCategory(categoryId, categoryName, boxArtUrl);
        }

        /// <summary>
        /// カテゴリIDテキストクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectCategpryNameTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DisplayLog(
                Utility.CopyClipBoad(SelectCategpryNameTextBlock.Text),
                "カテゴリをクリップボードにコピーしました。",
                "クリップボードへのコピーに失敗しました。"
            );
        }

        #endregion


        #region =============== メインパネル：タイトルテキストログ ===============

        /// <summary>
        /// 履歴アイテムクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTextLogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TitleTextLogListBox.SelectedItem is TitleTextForm selectedItem)
            {
                TitleEditTextBox.Text = selectedItem.Content;

                SelectCategpryIdTextBlock.Text = selectedItem.CategoryId;
                SelectCategpryNameTextBlock.Text = selectedItem.CategoryName;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTextLogDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is TitleTextForm item)
            {
                M_TitleText.Delete(item.Id);
            }

            ReloadTitleText();
        }

        #endregion


        #region =============== プライベート関数 ===============

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isError"></param>
        /// <param name="successStr"></param>
        /// <param name="errorStr"></param>
        private void DisplayLog(bool isError, String successStr, String errorStr)
        {
            if (isError)
            {
                StatusTextBlock.Text = successStr;
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                StatusTextBlock.Text = errorStr;
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task WaitForTargetStringAsync(String target)
        {
            // 最大3秒待つ（100msごとにチェック）
            for (int i = 0; i < 30; i++)
            {
                if (!string.IsNullOrEmpty(target)) return;

                await Task.Delay(100);
            }
        }

        #endregion


        #region =============== サイドパネル切替ボタン ===============

        /// <summary>
        /// サイドパネル：閉じるボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleCloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Width = MainWindowBaseWidth;
            AllSidePanelClose();
        }


        /// <summary>
        /// タイトルタグボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleTitleTagPanelButton_Click(object sender, RoutedEventArgs e)
        {
            AllSidePanelClose();
            TitleTagSidePanel.Visibility = Visibility.Visible;
            this.Width = MainWindowBaseWidth + SidePanelWidth;
        }


        /// <summary>
        /// サイドパネル：フレンドボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleFriendButton_Click(object sender, RoutedEventArgs e)
        {
            AllSidePanelClose();
            FriendSidePanel.Visibility = Visibility.Visible;
            this.Width = MainWindowBaseWidth + SidePanelWidth;
        }


        /// <summary>
        /// サイドパネル：カテゴリボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            AllSidePanelClose();
            CategorySidePanel.Visibility = Visibility.Visible;
            this.Width = MainWindowBaseWidth + SidePanelWidth;
        }


        /// <summary>
        /// サイドパネル：お気に入りタイトルボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleFavoriteTitleButton_Click(object sender, RoutedEventArgs e)
        {
            AllSidePanelClose();
            SaveTitleSidePanel.Visibility = Visibility.Visible;
            this.Width = MainWindowBaseWidth + SidePanelWidth;
        }


        /// <summary>
        /// サイドパネル：予約タイトルボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleReserveTitleButton_Click(object sender, RoutedEventArgs e)
        {
            AllSidePanelClose();
            CategorySearchSidePanel.Visibility = Visibility.Visible;
            this.Width = MainWindowBaseWidth + SidePanelWidth;
        }


        /// <summary>
        /// 全てのサイドパネルを閉じる
        /// </summary>
        private void AllSidePanelClose()
        {
            if (TitleTagSidePanel.Visibility == Visibility.Visible)
            {
                TitleTagSidePanel.Visibility = Visibility.Collapsed;
            }

            if (FriendSidePanel.Visibility == Visibility.Visible)
            {
                FriendSidePanel.Visibility = Visibility.Collapsed;
            }

            if (CategorySidePanel.Visibility == Visibility.Visible)
            {
                CategorySidePanel.Visibility = Visibility.Collapsed;
            }

            if (SaveTitleSidePanel.Visibility == Visibility.Visible)
            {
                SaveTitleSidePanel.Visibility = Visibility.Collapsed;
            }

            if (CategorySearchSidePanel.Visibility == Visibility.Visible)
            {
                CategorySearchSidePanel.Visibility = Visibility.Collapsed;
            }
        }

        #endregion


        #region =============== サイドパネル：タイトルテキストタグ画面 ===============

        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TitleTagListBox.SelectedItem is TitleTagForm selectedItem)
            {
                InsertTextAtCaret(selectedItem.DisplayName);
            }

            // 選択状態を解除
            FriendListBox.SelectedItem = null;
        }


        /// <summary>
        /// リストボックスアイテムクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // クリックされたアイテムを取得
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var item = ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item == null) return;

            // すでに選択されている場合は一度選択解除
            if (item != null && item.IsSelected)
            {
                listBox.SelectedIndex = -1;
            }
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if (sender is Button { DataContext: TitleTagForm item })
            {
                M_TitleTag.Delete(item.Id);
            }

            ReloadTitleTag();
        }


        /// <summary>
        /// 検索欄の文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO：検索処理追加
        }


        /// <summary>
        /// 追加ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagAddButton_Click(object sender, RoutedEventArgs e)
        {
            String displayName = TitleTextTagAddTextBox.Text.Trim();
            AddTitleTag(displayName);
        }

        #endregion


        #region =============== サイドパネル：フレンド画面 ===============

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

            ReloadFriendTag();
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
                InsertTextAtCaret(" @" + selectedItem.UserId + " ");
            }

            // 選択状態を解除
            FriendListBox.SelectedIndex = -1;
        }

        #endregion


        #region =============== サイドパネル：カテゴリ画面 ===============

        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO：カテゴリ検索処理追加
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is CategoryForm selectedItem)
            {
                SelectCategpryIdTextBlock.Text = selectedItem.CategoryId;
                SelectCategpryNameTextBlock.Text = selectedItem.DisplayName;
            }

            // 選択状態を解除
            CategoryListBox.SelectedIndex = -1;
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                M_Category.Delete(item.CategoryId);
            }

            ReloadCategory();
        }

        #endregion


        #region =============== サイドパネル：保存タイトル画面 ===============

        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO：お気に入りタイトル検索処理追加
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SaveTitleListBox.SelectedItem is TitleTextForm selectedItem)
            {
                editTitleTextForm.Content = selectedItem.Content;
                editTitleTextForm.CategoryId = selectedItem.CategoryId;
                editTitleTextForm.CategoryName = selectedItem.CategoryName;

                SetEditTitleTextForm();
            }

            // 選択状態を解除
            FriendListBox.SelectedItem = null;
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is TitleTextForm item)
            {
                var targetData = M_TitleText.SelectOneById(item.Id);

                targetData.SortNumber = 0;
                
                M_TitleText.Update(targetData);
            }

            ReloadSaveTitleText();
        }

        #endregion


        #region =============== サイドパネル：カテゴリ検索画面 ===============

        /// <summary>
        /// 検索遅延タイマー処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            categorySearchDebounceTimer.Stop();
            ReloadSearchCategory(lastCategorySearchText);
        }


        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchTitleSerchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            lastCategorySearchText = CategorySearchTitleSerchTextBox.Text;
            categorySearchDebounceTimer.Stop();
            categorySearchDebounceTimer.Start();
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategorySearchListBox.SelectedItem is CategoryForm selectedItem)
            {
                editTitleTextForm.CategoryId = selectedItem.CategoryId;
                editTitleTextForm.CategoryName = selectedItem.DisplayName;
                editTitleTextForm.BoxArtUrl = selectedItem.BoxArtUrl;

                SetEditTitleTextForm();
            }
        }

        #endregion

        private void TweetButton_Click(object sender, RoutedEventArgs e)
        {
            // 認証URL生成
            var oauthUrl = $"https://x.com/intent/post?text=";
            var text = editTitleTextForm.Content + "%0D%0A" + "配信カテゴリ：" + editTitleTextForm.CategoryName + "%0D%0A" + "配信URL：" + $"https://www.twitch.tv/" + Utility.UserName;

            // ブラウザで認証ページを開く
            Process.Start(new ProcessStartInfo
            {
                FileName = oauthUrl + text,
                UseShellExecute = true
            });
        }

        private void TitleEditTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (editTitleTextForm == null) return;
            editTitleTextForm.Content = TitleEditTextBox.Text;
        }

    }
}