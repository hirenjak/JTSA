using JTSA.Dao;
using JTSA.Forms;
using JTSA.Forms.TwitchIF;
using JTSA.Models;
using JTSA.Panels;
using JTSA.Utility;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Bson;
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

		//public EditTitleTextForm editTitleTextForm = new();

		private DispatcherTimer accessTokenRefreshTimer;


		/// <summary>
		/// 現在の設定タイトル
		/// </summary>
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
		/// 現在のスチームURL
		/// </summary>
		public string CurrentCategorySteamUrl
		{
			get
			{
				return SteamUrlTextBlock.Text;
            }

			set
			{
				SteamUrlTextBlock.Text = value;
				
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public string CurrentTtitleTextPreview
		{
			get
			{
				return CurrentTitleTextBlock.Text;
			}

			set
			{
				CurrentTitleTextBlock.Text = value;

            }
		}


		/// <summary>
		/// 
		/// </summary>
		public string CurrentCategoryId
		{
			get
			{
				return SelectCategoryIdTextBlock.Text;
			}

			set
			{
				SelectCategoryIdTextBlock.Text = value;

                SteamUrlTextSet(value);

            }
		}


		/// <summary>
		/// 
		/// </summary>
		public string CurrentCategoryName
		{
			get
			{
				return SelectCategoryNameTextBlock.Text;
            }

			set
			{
				SelectCategoryNameTextBlock.Text = value;
            }
		}


		/// <summary>
		/// 
		/// </summary>
		public string CurrentCategoryBoxArtUrl
		{
			set
			{
				SelectCategoryBoxArt.Source = new BitmapImage(new Uri(value)); ;
            }
		}


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            using (var db = new AppDbContext())
            {
                db.Database.Migrate();
            }
			AppLogPanel.Success(GetType().Name, "DBマイグレーション確認");

            // アクセストークンの自動リフレッシュタイマー設定
            accessTokenRefreshTimer = new DispatcherTimer();
            accessTokenRefreshTimer.Interval = TimeSpan.FromHours(3);
            accessTokenRefreshTimer.Tick += async (s, e) =>
            {
                // リフレッシュトークンからアクセストークンを再取得
                string accessToken = await ResetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    AppLogPanel.Error(GetType().Name, "アクセストークン未取得");
                    LoadSubPanel.Visibility = Visibility.Visible;
                    return;
                }

                TwitchHelper.AccessToken = accessToken;
                AccessToken_TextBlock.Text = "OK!";
            };
            accessTokenRefreshTimer.Start();

            AppLogPanel.Success(GetType().Name, "アクセストークン自動リフレッシュタイマー登録");

            // イベント登録
            Loaded += MainWindow_LoadedAsync;
            SteamUrlTextBlock.MouseLeftButtonUp += SteamUrlTextBlock_MouseLeftButtonUp;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void SteamUrlTextBlock_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var isProcessSuccess = JTSAHelper.CopyClipBoad(SteamUrlTextBlock.Text);
            AppLogPanel.AddSwitchLog(isProcessSuccess, GetType().Name,
                "クリップボードコピー成功 「 SteamUrl 」",
                "クリップボードコピー失敗 「 SteamUrl 」"
            );
        }

		private async void SteamUrlTextSet(string categoryId)
		{

			var result = await IgdbService.GetSteamUrlsAsync(categoryId);
			CurrentCategorySteamUrl = result[0];

        }


        /// <summary>
        /// コンストラクタ終了時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MainWindow_LoadedAsync(object sender, RoutedEventArgs e)
        {
           　var appLogProcessName = AppLogPanel.ProcessStart(GetType().Name, "アプリ起動処理");

            // Loading画面表示（※MainWindow_Loaded終わりまで表示）
            LoadScreen.Visibility = Visibility.Visible;
			LoadSubPanel.Visibility = Visibility.Collapsed;

			// クライアントID存在チェック
			if (string.IsNullOrEmpty(TwitchHelper.ClientID))
            {
                AppLogPanel.CriticalError(GetType().Name, appLogProcessName + "：ClientID未設定");
				return;
            }

			// ユーザー名取得確認
			M_Setting? settingUserName = DAO_Setting.SelectOneById(DAO_Setting.SettingName.UserName) ?? null;
			if (settingUserName == null || string.IsNullOrEmpty(settingUserName.Value))
            {
                UserName_TextBox.Text = "NG";
                AppLogPanel.Error(GetType().Name, appLogProcessName + "：ユーザー名未設定");
                LoadSubPanel.Visibility = Visibility.Visible;
				return;
			}

            // メモリに登録
            JTSAHelper.LoginName = settingUserName.Value;
			UserName_TextBox.Text = JTSAHelper.LoginName;

			// リフレッシュトークンからアクセストークンを再取得
			string accessToken = await ResetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                AccessToken_TextBlock.Text = "NG";
                AppLogPanel.Error(GetType().Name, "アクセストークン未取得");
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

			// メモリに登録
            TwitchHelper.AccessToken = accessToken;
            AccessToken_TextBlock.Text = "OK!";

            AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);

			// 配信者IDの取得
            var streamerInfo = await TwitchHelper.GetBroadcasterIdAsync(JTSAHelper.LoginName);
            if (streamerInfo == null)
            {
                AppLogPanel.Error(GetType().Name, "配信者情報未取得");
                return;
            }

			// メモリに登録
            TwitchHelper.BroadcasterId = streamerInfo.UserId;
            BroadcasterId_TextBlock.Text = "OK!";

            appLogProcessName = AppLogPanel.ProcessStart(GetType().Name, "アプリ初期化処理");


            IgdbService.Initialize(new HttpClient(), TwitchHelper.ClientID, TwitchHelper.AccessToken);


            // アクセストークンの確認を持って起動時設定を完了
            await StreamerDataSet();

            // 各パネルの初期化処理
            ChatPanel.Initialize();
			CategoryPanel.Initialize();

            PlayingGamePanel.ReloadPlaylistHeader();
            PlayingGamePanel.ReloadGamePlaylistItem();

            // ロード画面を非表示
            LoadScreen.Visibility = Visibility.Collapsed;

            AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
        }


        /// <summary>
        /// アクセストークンの再取得
        /// </summary>
        /// <returns></returns>
        private async Task<string> ResetAccessTokenAsync()
        {
            var appLogProcessName = AppLogPanel.ProcessStart(GetType().Name, "アクセストークン再取得");

			// リフレッシュトークンの取得（設定に無ければ失敗として戻す）
            M_Setting? settingRefreshToken = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RefreshToken);
			if (settingRefreshToken == null)
			{
				AppLogPanel.Error(GetType().Name, "リフレッシュトークン未設定");
                return null;
			}

            var accessTokenResponse = await TwitchHelper.RefreshAccessTokenAsync(settingRefreshToken.Value);
			if (accessTokenResponse == null)
			{
				AppLogPanel.Error(GetType().Name, "アクセストークン未取得");
				return null;
			}

            DAO_Setting.InsertUpdate(
				DAO_Setting.SettingName.RefreshToken,
                accessTokenResponse.refreshToken
			);

            DAO_Setting.InsertUpdate(
				DAO_Setting.SettingName.ExpiresIn,
                accessTokenResponse.expiresIn.ToString()
			);

            AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
			return accessTokenResponse.accessToken;
        }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="userName"></param>
		public async Task StreamerDataSet()
        {
            var appLogProcessName = AppLogPanel.ProcessStart(GetType().Name, "配信者情報設定");

            // タイトル取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
            CurrentTitleText = streamInfo.title;

            TitleEditTextBox.Text = CurrentTitleTextBlock.Text;

			var dbCategoryData = DAO_Category.SelectOneById(streamInfo.gameId);

			if(dbCategoryData == null)
			{
                // カテゴリ名取得処理
                var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);
				var steamUrl = await IgdbService.GetSteamUrlsAsync(category.Id);

                dbCategoryData.CategoryId = category.Id;
				dbCategoryData.DisplayName = category.Name;
				dbCategoryData.SteamUrl = steamUrl[0] ?? string.Empty;
				dbCategoryData.BoxArtUrl = category.BoxArtUrl;
            }

            CurrentTitleText = TitleEditTextBox.Text;

			CurrentCategoryId = dbCategoryData.CategoryId;
			CurrentCategoryName = dbCategoryData.DisplayName;
            CurrentCategoryBoxArtUrl = dbCategoryData.BoxArtUrl;
			CurrentCategorySteamUrl = dbCategoryData.SteamUrl;


            // リスト読み込み処理
            ReloadTitleText();
            TitleTagSidePanel.ReloadTitleTag();

            AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
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

            JTSAHelper.LoginName = LoadPanelUserNameTextBox.Text.Trim();
			UserName_TextBox.Text = JTSAHelper.LoginName;

			var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();

			// 認証URLとユーザーコードをユーザーに表示
			LoadPanelSubTextBox.Text = deviceCodeResponse.user_code;

			LoadSubPanel.Visibility = Visibility.Visible;

			// 認証ページを自動で開く
			Process.Start(new ProcessStartInfo(deviceCodeResponse.verification_uri + $"user_code={JTSAHelper.LoginName}") { UseShellExecute = true });

			// アクセストークン取得
			var accessTokenResponse = await TwitchHelper.PollDeviceTokenAsync(deviceCodeResponse.device_code, deviceCodeResponse.interval, deviceCodeResponse.expires_in);

            if (accessTokenResponse != null)
			{
				TwitchHelper.AccessToken = accessTokenResponse.accessToken;
				AccessToken_TextBlock.Text = "OK!";
			}
			else
			{
				AccessToken_TextBlock.Text = "NG";
			}

			// --- 設定情報保存処理 ---
			DAO_Setting.InsertUpdate(
				DAO_Setting.SettingName.UserName,
				JTSAHelper.LoginName
			);

			DAO_Setting.InsertUpdate(
				DAO_Setting.SettingName.RefreshToken,
				accessTokenResponse.refreshToken
			);

			DAO_Setting.InsertUpdate(
				DAO_Setting.SettingName.ExpiresIn,
				accessTokenResponse.expiresIn.ToString()
			);

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
			var processLogName = AppLogPanel.ProcessStart(GetType().Name, "タイトルログ一覧読込");
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

			AppLogPanel.ProcessEnd(GetType().Name, processLogName);
        }

		#endregion


		#region =============== リストデータ追加処理 ===============

		/// <summary>
		/// タイトルテキスト：追加処理
		/// </summary>
		/// <param name="title"></param>
		private void AddTitleText(string content, string categoryId, string categoryName, string categoryBoxArtUrl)
        {
            var processLogName = AppLogPanel.ProcessStart(GetType().Name, "タイトルログ一覧読込");

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

            AppLogPanel.ProcessEnd(GetType().Name, processLogName);
        }

		#endregion


		#region =============== メインパネル：編集部分 ===============

		/// <summary>
		/// 送信ボタンクリック時
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void SendTitleButton_Click(object sender, RoutedEventArgs e)
        {
            var processLogName = AppLogPanel.ProcessStart(GetType().Name, "配信タイトル送信");

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
            
			CurrentCategoryId = getCategory.Id;
			CurrentCategoryName = getCategory.Name;
			CurrentCategoryBoxArtUrl = getCategory.BoxArtUrl;

            DAO_Category.UpdateLastUsed(getCategory.Id);
            CategoryPanel.ReloadCategory();

            AppLogPanel.ProcessEnd(GetType().Name, processLogName);
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
            var processLogName = AppLogPanel.ProcessStart(GetType().Name, "配信タイトル取得");

            // 配信概要取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
			if (streamInfo == null) { AppLogPanel.Error(GetType().Name, "配信概要未取得"); return; }

            // カテゴリ取得処理
            var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);
            if (category == null) { AppLogPanel.Error(GetType().Name, "カテゴリー未取得"); return; }

            CurrentTitleText = streamInfo.title;

			CurrentCategoryId = category.Id;
			CurrentCategoryName= category.Name;
			CurrentCategoryBoxArtUrl = category.BoxArtUrl;

            AppLogPanel.ProcessEnd(GetType().Name, processLogName);
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
			var stremTitleText = TitleTextFriendTagToXReplace(TitleEditTextBox.Text);
			var categoryNameText = CurrentCategoryName;

            // 認証URL生成
            var oauthUrl = $"https://x.com/intent/post?text=";
			var categoryText = "配信カテゴリ：" + categoryNameText;
			var streamUrlText = $"https://www.twitch.tv/" + JTSAHelper.LoginName;

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
            CurrentTitleText = TitleEditTextBox.Text;
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