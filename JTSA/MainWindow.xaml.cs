using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Panels;
using JTSA.Utility;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace JTSA
{
	public partial class MainWindow : Window
	{
		/// <summary>  </summary>
		public ObservableCollection<TitleTextForm> TitleTextFormList { get; } = new();

		public EditTitleTextForm editTitleTextForm = new();

		private DispatcherTimer accessTokenRefreshTimer;

		public string CurrentTitleText 
		{ 
			get
			{ 
				return CurrentTitleTextBlock.Text; 
			}
			set
			{ 
				CurrentTitleTextBlock.Text = TitleTextFriendTagReplace(value); 
				TitleWordNum.Content = CurrentTitleTextBlock.Text.Count() + "/140";  
			} 
		}


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            using (var db = new AppDbContext())
            {
                db.Database.Migrate();
            }

            InitializeComponent();

			DataContext = this;

			TitleTagSidePanel.Visibility = Visibility.Visible;

            // イベント登録
            this.Loaded += MainWindow_LoadedAsync;

            // アクセストークンの自動リフレッシュタイマー設定
            accessTokenRefreshTimer = new DispatcherTimer();
            accessTokenRefreshTimer.Interval = TimeSpan.FromHours(3);
            accessTokenRefreshTimer.Tick += async (s, e) =>
            {
                await ResetAccessTokenAsync();
                AppLogPanel.AddProcessLog(GetType().Name, "アクセストークン自動リフレッシュ", "実行");
            };

            accessTokenRefreshTimer.Start();
        }


		/// <summary>
		/// コンストラクタ終了時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void MainWindow_LoadedAsync(object sender, RoutedEventArgs e)
        {
            AppLogPanel.AddProcessLog(GetType().Name, "アプリ起動", "処理開始");

            // Loading画面表示（※MainWindow_Loaded終わりまで表示）
            LoadScreen.Visibility = Visibility.Visible;
			LoadSubPanel.Visibility = Visibility.Collapsed;

            // クライアントID存在チェック
            if (string.IsNullOrEmpty(TwitchHelper.ClientID)) return;

			// ユーザー名取得確認
			M_Setting? settingUserName = DAO_Setting.SelectOneById(DAO_Setting.SettingName.UserName) ?? null;
			if (settingUserName == null || string.IsNullOrEmpty(settingUserName.Value))
			{
				StatusTextBlock.Text = "ユーザー名が設定されていません";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
				LoadSubPanel.Visibility = Visibility.Visible;

				return;
			}
            
			AppLogPanel.AddSuccessLog(GetType().Name, "取得成功 「 ユーザー名 」");

            // ユーザー名の取得に成功していれば画面とアプリメモリに値を登録
            JTSAHelper.UserName = settingUserName.Value;
			UserName_TextBox.Text = JTSAHelper.UserName;

			// リフレッシュトークンからアクセストークンを再取得
			bool isProcessSuccess = await ResetAccessTokenAsync();
            if (!isProcessSuccess)
            {
                AppLogPanel.AddCriticalErrorLog(GetType().Name, "※※※ 再認証してください ※※※");
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

			// アクセストークンの確認を持って起動時設定を完了
            await StreamerDataSet();

			await ChatPanel.Initialize();

            await PlayingGamePanel.ReloadGameAllPlaylist();

            // ロード画面を非表示
            LoadScreen.Visibility = Visibility.Collapsed;

            AppLogPanel.AddProcessLog(GetType().Name, "アプリ起動", "処理終了");
        }


        /// <summary>
        /// アクセストークンの再取得
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ResetAccessTokenAsync()
        {
            AppLogPanel.AddProcessLog(GetType().Name, "アクセストークン再取得", "処理開始");

			// リフレッシュトークンの取得（設定に無ければ失敗として戻す）
            M_Setting? settingRefreshToken = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RefreshToken);
			if (settingRefreshToken == null) return false;

            bool isProcessSuccess = !(string.IsNullOrEmpty(settingRefreshToken.Value));
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name, "DB取得成功 「 ユーザー名 」", "DB取得失敗 「 ユーザー名 」" );

            if (!isProcessSuccess) return false;

            var accessTokenResponse = await TwitchHelper.RefreshAccessTokenAsync(settingRefreshToken.Value);
            isProcessSuccess = !string.IsNullOrEmpty(accessTokenResponse.accessToken);
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "取得成功 「 アクセストークン 」",
                "取得失敗 「 アクセストークン 」"
            );

            if (!isProcessSuccess) return false;

            TwitchHelper.AccessToken = accessTokenResponse.accessToken;

            DAO_Setting.InsertUpdate(new M_Setting
            {
                Name = (int)DAO_Setting.SettingName.RefreshToken,
                Value = accessTokenResponse.refreshToken,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now,
                LastUsedDateTime = DateTime.Now
            });

            DAO_Setting.InsertUpdate(new M_Setting
            {
                Name = (int)DAO_Setting.SettingName.ExpiresIn,
                Value = accessTokenResponse.expiresIn.ToString(),
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now,
                LastUsedDateTime = DateTime.Now
            });

            AppLogPanel.AddProcessLog(GetType().Name, "アクセストークン再取得", "処理終了");
			return true;
        }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="userName"></param>
		private async Task StreamerDataSet()
        {
            AppLogPanel.AddProcessLog(GetType().Name, "配信者情報設定", "処理開始");

            var streamerInfo = await TwitchHelper.GetBroadcasterIdAsync();
            if (streamerInfo == null) return;

            var isProcessSuccess = streamerInfo != null && !string.IsNullOrEmpty(streamerInfo.BroadcastId);
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "取得成功 「 配信者ID 」",
                "取得失敗 「 配信者ID 」"
            );

            if (!isProcessSuccess) return;

			TwitchHelper.BroadcasterId = streamerInfo.BroadcastId;


            bool isExistAccessToken =!string.IsNullOrEmpty(TwitchHelper.AccessToken);

			if (isExistAccessToken)
            {
                AppLogPanel.AddSuccessLog(GetType().Name, "取得成功 「 アクセストークン 」");
				AccessToken_TextBlock.Text = "OK!";

                // タイトル取得処理
                var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
                CurrentTitleText = streamInfo.title;

                TitleEditTextBox.Text = CurrentTitleTextBlock.Text;

				// カテゴリ名取得処理
				var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);

				editTitleTextForm.Content = TitleEditTextBox.Text;
				editTitleTextForm.SetCategory(category.Id, category.Name, category.BoxArtUrl);
                SetDisplayFromEditFrom();

			}
			else
            {
                AppLogPanel.AddErrorLog(GetType().Name, "取得失敗 「 アクセストークン 」");
				AccessToken_TextBlock.Text = "NG";
			}

			// リスト読み込み処理
			ReloadTitleText();
			TitleTagSidePanel.ReloadTitleTag();
			FriendPanel.ReloadFriend();

            AppLogPanel.AddProcessLog(GetType().Name, "配信者情報設定", "処理終了");
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

            JTSAHelper.UserName = LoadPanelUserNameTextBox.Text.Trim();
			UserName_TextBox.Text = JTSAHelper.UserName;

			var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();

			// 認証URLとユーザーコードをユーザーに表示
			LoadPanelSubTextBox.Text = deviceCodeResponse.user_code;

			LoadSubPanel.Visibility = Visibility.Visible;

			// 認証ページを自動で開く（オプション）
			Process.Start(new ProcessStartInfo(deviceCodeResponse.verification_uri + $"user_code={JTSAHelper.UserName}") { UseShellExecute = true });

			// ポーリングでトークン取得
			var accessTokenResponse = await TwitchHelper.PollDeviceTokenAsync(deviceCodeResponse.device_code, deviceCodeResponse.interval, deviceCodeResponse.expires_in);

            var isProcessSuccess = !string.IsNullOrEmpty(accessTokenResponse.accessToken);
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "取得成功 「 アクセストークン 」",
                "取得失敗 「 アクセストークン 」"
            );

            if (isProcessSuccess)
			{
				TwitchHelper.AccessToken = accessTokenResponse.accessToken;
				AccessToken_TextBlock.Text = "OK!";
			}
			else
			{
				AccessToken_TextBlock.Text = "NG";
			}

			// --- 設定情報保存処理 ---
			DAO_Setting.InsertUpdate(new M_Setting
			{
				Name = (int)DAO_Setting.SettingName.UserName,
				Value = JTSAHelper.UserName,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now,
                LastUsedDateTime = DateTime.Now
            });

			DAO_Setting.InsertUpdate(new M_Setting
			{
				Name = (int)DAO_Setting.SettingName.RefreshToken,
				Value = accessTokenResponse.refreshToken,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now,
                LastUsedDateTime = DateTime.Now
            });

			DAO_Setting.InsertUpdate(new M_Setting
			{
				Name = (int)DAO_Setting.SettingName.ExpiresIn,
				Value = accessTokenResponse.expiresIn.ToString(),
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now,
                LastUsedDateTime = DateTime.Now
            });

			await StreamerDataSet();

			LoadScreen.Visibility = Visibility.Collapsed;
		}

		#endregion


		#region =============== リストデータ更新処理 ===============

		/// <summary>
		/// 読み込み処理：タイトルテキスト
		/// </summary>
		public void ReloadTitleText()
		{
			// DB接続と初期化処理
			using var db = new AppDbContext();
			TitleTextFormList.Clear();

			// データの取得
			var records = DAO_TitleText.SelectAllOrderbyLastUser(db);

			// 画面データ入れ換え処理
			foreach (var item in records)
			{
				TitleTextFormList.Add(new()
				{
					Id = item.Id,
					Content = item.Content,
					CategoryId = item.CategoryId,
					CategoryName = item.CategoryName,
					CategoryBoxArtUrl = item.CategoryBoxArtUrl,
                    LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm")
				});
			}

            AppLogPanel.AddSuccessLog(GetType().Name, "タイトルログリストを読込");
		}

		#endregion


		#region =============== リストデータ追加処理 ===============

		/// <summary>
		/// タイトルテキスト：追加処理
		/// </summary>
		/// <param name="title"></param>
		private void AddTitleText(string content, string categoryId, string categoryName, string categoryBoxArtUrl)
        {
            AppLogPanel.AddProcessLog(GetType().Name, "タイトルログリスト", "追加処理開始");

            // DB接続処理
            using var db = new AppDbContext();

			// データチェック
			if (string.IsNullOrWhiteSpace(content)) return;

			// データ作成
			var isnertData = new T_TitleText
			{
				Content = content,
				CategoryId = categoryId,
				CategoryName = categoryName,
				CategoryBoxArtUrl = categoryBoxArtUrl,
                SelectedCount = 0,
				SortNumber = 9999,
				LastUsedDateTime = DateTime.Now,
				CreatedDateTime = DateTime.Now,
				UpdatedDateTime = DateTime.Now
			};

            // 挿入処理
            var isProcessSuccess = DAO_TitleText.Insert(isnertData);
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "データ追加成功 「 タイトルログ 」",
				"データ追加失敗 「 タイトルログ 」"
			);

			// 再読み込み処理
			ReloadTitleText();

            AppLogPanel.AddProcessLog(GetType().Name, "タイトルログリスト", "追加処理終了");
        }

		#endregion


		#region =============== メインパネル：編集部分 ===============


		/// <summary>
		/// 読み込み処理：編集部分
		/// </summary>
		public void SetDisplayFromEditFrom()
		{
			TitleEditTextBox.Text = editTitleTextForm.Content;
			SelectCategoryIdTextBlock.Text = editTitleTextForm.CategoryId;
			SelectCategoryNameTextBlock.Text = editTitleTextForm.CategoryName;
			if (!string.IsNullOrEmpty(editTitleTextForm.CategoryBoxArtUrl))
            {
                SelectCategoryBoxArt.Source = new BitmapImage(new Uri(editTitleTextForm.CategoryBoxArtUrl));
            }
        }


		/// <summary>
		/// 送信ボタンクリック時
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void SendTitleButton_Click(object sender, RoutedEventArgs e)
        {
            AppLogPanel.AddProcessLog(GetType().Name, "配信タイトル送信", "処理開始");

            var title = CurrentTitleText;
			var categoryId = SelectCategoryIdTextBlock.Text;
			var categoryName = SelectCategoryNameTextBlock.Text;
			var categoryBoxArtUrl = SelectCategoryBoxArt.Source.ToString();


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

			var isProcessSuccess = response.IsSuccessStatusCode;
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "送信成功 「 配信概要 」",
                "送信失敗 「 配信概要 」：" + (int)response.StatusCode + "：" + response.StatusCode
            );

            // レスポンスの処理
            if (isProcessSuccess)
			{
				// --- 履歴追加処理 ---
				AddTitleText(TitleEditTextBox.Text, categoryId, categoryName, categoryBoxArtUrl);
			}

			String gameId = SelectCategoryIdTextBlock.Text.Trim();
            isProcessSuccess = await TwitchHelper.SetCategoryAsync(gameId.ToString());
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "送信成功 「 カテゴリ 」",
				"送信失敗 「 カテゴリ 」"
            );

            // タイトル取得処理
			var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
            var getTitleText = streamInfo.title;
            var getCategory = await TwitchHelper.GetCategoryByGameId(gameId);

            CurrentTitleText = getTitleText;
            editTitleTextForm.SetCategory(getCategory.Id, getCategory.Name, getCategory.BoxArtUrl);

            SetDisplayFromEditFrom();


            DAO_Category.UpdateLastUsed(getCategory.Id);
            CategoryPanel.ReloadCategory();

            AppLogPanel.AddProcessLog(GetType().Name, "配信タイトル送信", "処理終了");
        }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CurrentTitleTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
            var isProcessSuccess = JTSAHelper.CopyClipBoad(CurrentTitleText);
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "クリップボードコピー成功 「 タイトル 」",
                "クリップボードコピー失敗 「 タイトル 」"
            );
        }


		/// <summary>
		/// 取得ボタンクリック時
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void GetTitleButton_Click(object sender, RoutedEventArgs e)
        {
            AppLogPanel.AddProcessLog(GetType().Name, "配信タイトル取得", "処理開始");

            // カテゴリID処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);

			// カテゴリ名取得処理
			var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);

			var isProcessSuccess = !string.IsNullOrEmpty(streamInfo.title);

            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "取得成功 「 配信概要 」",
                "取得失敗 「 配信概要 」"
            );

			if (isProcessSuccess)
			{
                CurrentTitleText = streamInfo.title;
            }

            editTitleTextForm.SetCategory(category.Id, category.Name, category.BoxArtUrl);
            SetDisplayFromEditFrom();

            AppLogPanel.AddProcessLog(GetType().Name, "配信タイトル取得", "処理終了");
        }


		/// <summary>
		/// テキスト編集のカーソル位置にテキストを挿入
		/// </summary>
		/// <param name="insertText"></param>
		public void InsertTextAtCaret(string insertText)
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
			String boxArtUrl = editTitleTextForm.CategoryBoxArtUrl;
		}


		/// <summary>
		/// カテゴリIDテキストクリック時
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void SelectCategpryNameTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
            AppLogPanel.AddSwitchLog(JTSAHelper.CopyClipBoad(SelectCategoryNameTextBlock.Text), GetType().Name,
                "クリップボードコピー成功 「 カテゴリ 」",
                "クリップボードコピー失敗 「 カテゴリ 」"
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

				SelectCategoryIdTextBlock.Text = selectedItem.CategoryId;
				SelectCategoryNameTextBlock.Text = selectedItem.CategoryName;
                if (!string.IsNullOrEmpty(selectedItem.CategoryBoxArtUrl))
                {
					try
                    {
                        SelectCategoryBoxArt.Source = new BitmapImage(new Uri(selectedItem.CategoryBoxArtUrl));
                    }
					catch
					{
						SelectCategoryBoxArt.Source = null;
					}
                }
            }
		}


		/// <summary>
		/// 削除ボタンクリック時
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TitleTextLogDeleteButton_Click(object sender, RoutedEventArgs e)
		{
			// ボタンのDataContextから削除対象を取得
			if ((sender as Button)?.DataContext is TitleTextForm item)
			{
				DAO_TitleText.Delete(item.Id);
			}

			ReloadTitleText();
		}

		#endregion


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TweetButton_Click(object sender, RoutedEventArgs e)
		{
			// 必要データの取得
			var stremTitleText = TitleTextFriendTagToXReplace(editTitleTextForm.Content);
			var categoryNameText = editTitleTextForm.CategoryName;

            // 認証URL生成
            var oauthUrl = $"https://x.com/intent/post?text=";
			var categoryText = "配信カテゴリ：" + categoryNameText;
			var streamUrlText = $"https://www.twitch.tv/" + JTSAHelper.UserName;

            stremTitleText = stremTitleText.Replace("#", "＃");

			// URIエンコード
			var encodedText = WebUtility.UrlEncode(stremTitleText) + "%0A" + WebUtility.UrlEncode(categoryText) + "%0A" + WebUtility.UrlEncode(streamUrlText);

            // ブラウザで認証ページを開く
            Process.Start(new ProcessStartInfo
			{
				FileName = oauthUrl + encodedText,
				UseShellExecute = true
			});
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TitleEditTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			editTitleTextForm.Content = TitleEditTextBox.Text;
            CurrentTitleText = editTitleTextForm.Content;
        }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TokenCodeCopyButton_Click(object sender, RoutedEventArgs e)
		{
            JTSAHelper.CopyClipBoad(LoadPanelSubTextBox.Text);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void DBFolderOpen(object sender, RoutedEventArgs e)
        {
			OpenDbFolder();
        }


        /// <summary>
        /// dbDirectoryをエクスプローラーで開くメソッド
        /// </summary>
        private void OpenDbFolder()
        {
            string folder = AppDbContext.dbDirectory;
            if (Directory.Exists(folder))
            {
                Process.Start("explorer.exe", folder);
            }
            else
            {
                MessageBox.Show("フォルダが存在しません: " + folder);
            }
        }


		/// <summary>
		/// 
		/// </summary>
		private string TitleTextFriendTagReplace(string titleText)
		{
			var friendText = FriendPanel.FriendPrefixWordTextBox.Text;
			foreach(var friendItem in FriendPanel.SelectedFriendFormList)
			{
			 	friendText += " @" + friendItem.UserId;
			}

            titleText = titleText.Replace("${friend}", friendText + " ");
			return titleText;
		}


        /// <summary>
        /// 
        /// </summary>
        private string TitleTextFriendTagToXReplace(string titleText)
        {
            var friendText = FriendPanel.FriendPrefixWordTextBox.Text;
            foreach (var friendItem in FriendPanel.SelectedFriendFormList)
            {
                friendText += friendItem.DisplayName + "、";
            }

			if(friendText.Length > 0)
            {
                friendText = friendText.Substring(0, friendText.Length - 1);
            }

            titleText = titleText.Replace("${friend}", friendText);
            return titleText;
        }


        /// <summary>
        /// 
        /// </summary>
        public void CurrentTitleTextUpdate()
		{
            CurrentTitleTextBlock.Text = TitleTextFriendTagReplace(TitleEditTextBox.Text);

            TitleWordNum.Content = CurrentTitleTextBlock.Text.Count() + "/140";
        }
    }
}