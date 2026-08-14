using JTSA.Dao;
using JTSA.Forms;
using JTSA.Forms.TwitchIF;
using JTSA.Models;
using JTSA.Panels;
using JTSA.Utility;
using Microsoft.EntityFrameworkCore;
using NAudio;
using NAudio.Utils;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace JTSA
{
	/// <summary>
	/// メインウィンドウ
	/// </summary>
	public partial class MainWindow : Window
	{
		/// <summary> タイトルログ用のリスト  </summary>
		public ObservableCollection<TitleTextForm> TitleTextFormList { get; } = new();

		/// <summary> アクセストークンの再取得用タイマ </summary>
		private DispatcherTimer accessTokenRefreshTimer;

        /// <summary> ヘッダ部分：現在の設定タイトル </summary>
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

        /// <summary> ヘッダ部分：現在のスチームURL </summary>
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

		/// <summary> ヘッダ部分：現在のカテゴリID </summary>
		public string CurrentCategoryId
		{
			get
			{
				return SelectCategoryIdTextBlock.Text;
			}

			set
			{
				SelectCategoryIdTextBlock.Text = value;

				// カテゴリ設定をしたら同時にSteamURLを取得して設定
                SteamUrlTextSet(value);
            }
		}

        /// <summary> ヘッダ部分：カテゴリ名 </summary>
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

        /// <summary> ヘッダ部分：カテゴリBoxArt </summary>
        public string CurrentCategoryBoxArtUrl
		{
			set
            {
                // URLが無いカテゴリもあるため、空ならクリアするだけ
                if (string.IsNullOrWhiteSpace(value))
				{
					SelectCategoryBoxArt.Source = null;
					return;
				}

				try
				{
					SelectCategoryBoxArt.Source = new BitmapImage(new Uri(value));
				}
				catch (Exception)
				{
					SelectCategoryBoxArt.Source = null;
					AppLogPanel.Error(GetType().Name, $"ボックスアート表示失敗 「 {value} 」");
				}
            }
		}


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            // WPF上の初期化処理
			InitializeComponent();
            DataContext = this;

            // タイトルのバージョン設定
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"JakTwtchStreamerAssistant v{version?.ToString(3)}";

            #region ==========DBマイグレーション設定==========

            using (var db = new AppDbContext())
            {
                ClearAbandonedMigrationLock(db);
                db.Database.Migrate();
            }

            #endregion


            #region ==========アクセストークンの自動リフレッシュタイマー設定==========

            accessTokenRefreshTimer = new DispatcherTimer();
            accessTokenRefreshTimer.Interval = TimeSpan.FromHours(3);
            accessTokenRefreshTimer.Tick += async (s, e) =>
            {
                // リフレッシュトークンからアクセストークンを再取得
                string accessToken = await ResetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    LoadSubPanel.Visibility = Visibility.Visible;
                    return;
                }

                TwitchHelper.AccessToken = accessToken;
                AccessToken_TextBlock.Text = "OK!";
            };
            accessTokenRefreshTimer.Start();

            #endregion


            #region ==========イベントハンドラ設定==========

            Loaded += MainWindow_LoadedAsync;
            SteamUrlTextBlock.MouseLeftButtonUp += SteamUrlTextBlock_MouseLeftButtonUp;

            #endregion
        }


        /// <summary>
        /// 異常終了時に残ったEF CoreのSQLiteマイグレーションロックを解除する。
        /// 別のJTSAが動作中の場合は、有効なマイグレーションを妨げないため解除しない。
        /// </summary>
        private static void ClearAbandonedMigrationLock(AppDbContext db)
        {
            using var currentProcess = Process.GetCurrentProcess();
            var hasOtherInstance = Process
                .GetProcessesByName(currentProcess.ProcessName)
                .Any(process => process.Id != currentProcess.Id);

            if (hasOtherInstance)
            {
                return;
            }

            db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"__EFMigrationsLock\";");
        }


        #region ===============イベントハンドラ===============

        /// <summary>
        /// 【イベント】コンストラクタ終了時の処理
        /// </summary>
        private async void MainWindow_LoadedAsync(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "メインウィンドウ（読込）");
            processLog.EventStartLogWrite();

            // Loading画面表示（※MainWindow_Loaded終わりまで表示）
            LoadScreen.Visibility = Visibility.Visible;
            LoadSubPanel.Visibility = Visibility.Collapsed;

            // クライアントID存在チェック
            if (string.IsNullOrEmpty(TwitchHelper.ClientID))
            {
                processLog.CriticalErrorLogWrite("ClientID未設定");
                return;
            }

            // リフレッシュトークン取得確認
            // ユーザー名はアクセストークンから特定できるため、保存済みトークンの有無だけを見る
            M_Setting? settingRefreshToken = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RefreshToken) ?? null;
            if (settingRefreshToken == null || string.IsNullOrEmpty(settingRefreshToken.Value))
            {
                processLog.CriticalErrorLogWrite("未認証（OAuth認証が必要）");

                AccessToken_TextBlock.Text = "NG";
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

            // リフレッシュトークンからアクセストークンを再取得
            string accessToken = await ResetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                processLog.CriticalErrorLogWrite("アクセストークン未取得");

                AccessToken_TextBlock.Text = "NG";
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

            // メモリに登録
            TwitchHelper.AccessToken = accessToken;
            AccessToken_TextBlock.Text = "OK!";

            // 認証後の初期化（OAuth認証直後と共通）
            await InitializeAfterAuthAsync();

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }

        /// <summary>
        /// Twitch API が401/403を返した場合、モーダルダイアログを出さずに
        /// OAuth再認証画面へ強制的に切り替える。
        /// </summary>
        public void RequireOAuthReauthentication(string reason, string responseDetail = "")
        {
            // 認証エラー後もチャットイベントからAPI呼び出しが連打されないようにする。
            TwitchHelper.AccessToken = string.Empty;
            AccessToken_TextBlock.Text = "NG";
            LoadPanelTextBlock.Text = "OAuth再認証が必要です";
            LoadPanelSubTextBox.Text = string.Empty;
            LoadSubPanel.Visibility = Visibility.Visible;
            LoadScreen.Visibility = Visibility.Visible;

            // LoadScreenは先頭タブ内にあるため、別タブを表示中でも必ず見えるようにする。
            MainTabControl.SelectedIndex = 0;

            AppLogPanel.Error(
                GetType().Name,
                string.IsNullOrWhiteSpace(responseDetail)
                    ? reason
                    : $"{reason} Twitch応答: {responseDetail}");
        }


        /// <summary>
        /// ヘッダ部:SteamURLテキストブロック（クリック）
        /// </summary>
        private void SteamUrlTextBlock_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "ヘッダ部:SteamURLテキストブロック（クリック）");
            processLog.EventStartLogWrite();

            if (!JTSAHelper.CopyClipBoad(SteamUrlTextBlock.Text))
            {
                processLog.ErrorLogWrite("SteamURLクリップボードコピー失敗");
            }

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// OAuth認証画面:認証ボタン（クリック）
        /// </summary>
        private async void OAuthButton_Click(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "OAuth認証画面:認証ボタン（クリック）");
            processLog.EventStartLogWrite();

            // Loading画面表示
            LoadScreen.Visibility = Visibility.Visible;
            LoadSubPanel.Visibility = Visibility.Visible;


            #region ===============認証処理===============

            var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();
            if (deviceCodeResponse == null)
            {
                processLog.ErrorLogWrite("デバイスコードの取得に失敗");
                AccessToken_TextBlock.Text = "NG";
                return;
            }

            // 認証URLとユーザーコードをユーザーに表示
            LoadPanelSubTextBox.Text = deviceCodeResponse.user_code;

            // 認証ページを自動で開く
            var verificationUrl = string.IsNullOrEmpty(deviceCodeResponse.verification_uri_complete)    // verification_uri_complete はユーザーコードを埋め込み済みのURL
                ? deviceCodeResponse.verification_uri
                : deviceCodeResponse.verification_uri_complete;
            Process.Start(new ProcessStartInfo(verificationUrl) { UseShellExecute = true });

            // アクセストークン取得
            var accessTokenResponse = await TwitchHelper.PollDeviceTokenAsync(deviceCodeResponse.device_code, deviceCodeResponse.interval, deviceCodeResponse.expires_in);
            if (accessTokenResponse == null)
            {
                processLog.ErrorLogWrite("アクセストークンの取得に失敗");
                AccessToken_TextBlock.Text = "NG";
                return;
            }

            TwitchHelper.AccessToken = accessTokenResponse.accessToken;
            AccessToken_TextBlock.Text = "OK!";

            #endregion


            // ユーザー名はこの後 InitializeAfterAuthAsync がアクセストークンから特定して保存する
            #region ===============設定情報保存処理===============

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.RefreshToken,
                accessTokenResponse.refreshToken
            );

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.ExpiresIn,
                accessTokenResponse.expiresIn.ToString()
            );

            #endregion


            // 認証後の初期化（起動時と共通）
            await InitializeAfterAuthAsync();

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// ヘッダ部:送信ボタン（クリック）
        /// 配信タイトルをTwitchに送信する
        /// </summary>
        private async void SendTitleButton_Click(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "ヘッダ部:送信ボタン（クリック）");
            processLog.EventStartLogWrite();

            var title = CurrentTitleText;
            var categoryId = SelectCategoryIdTextBlock.Text;
            var categoryName = SelectCategoryNameTextBlock.Text;
            var categoryBoxArtUrl = SelectCategoryBoxArt.Source?.ToString() ?? "";  // ボックスアートが無いカテゴリではSourceがnullになる

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TwitchHelper.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", TwitchHelper.ClientID);

            var content = new StringContent(JsonSerializer.Serialize(new { title = title }), Encoding.UTF8, "application/json");

            // TwitchAPIで配信タイトルを更新
            var response = await client.PatchAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={TwitchHelper.BroadcasterId}", content);
            if (response.IsSuccessStatusCode)
            {
                // 履歴追加処理
                AddTitleText(TitleEditTextBox.Text, categoryId, categoryName, categoryBoxArtUrl);
            }
            else
            {
                processLog.ErrorLogWrite($"配信概要送信:{(int)response.StatusCode}:{response.StatusCode}");
            }

            // カテゴリ設定処理
            string gameId = SelectCategoryIdTextBlock.Text.Trim();
            if(!await TwitchHelper.SetCategoryAsync(gameId.ToString()))
            {
                processLog.ErrorLogWrite("カテゴリ設定処理失敗");
            }

            // タイトル取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
            if (streamInfo is null)
            {
                processLog.ErrorLogWrite("タイトル取得処理失敗");
                return;
            }
            var getTitleText = streamInfo.title;

            // カテゴリ取得処理
            var getCategory = await TwitchHelper.GetCategoryByGameId(gameId);
            if (getCategory is null)
            {
                processLog.ErrorLogWrite("カテゴリ取得処理失敗");
                return;
            }

            CurrentTitleText = getTitleText;
            CurrentCategoryId = getCategory.Id;
            CurrentCategoryName = getCategory.Name;
            CurrentCategoryBoxArtUrl = getCategory.BoxArtUrl;

            DAO_Category.UpdateLastUsed(getCategory.Id);

            CategoryPanel.ReloadCategory();

            // カテゴリに紐づくチャンネルポイントプリセットを適用する（紐づけが無ければ何もしない）
            await ApplyChannelPointPresetForCategoryAsync(getCategory.Id);

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// ヘッダ部:タイトルテキストボックス（クリック）
        /// </summary>
        private void CurrentTitleTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "ヘッダ部:タイトルテキストボックス（クリック）");
            processLog.EventStartLogWrite();

            // クリップボードにコピー
            if (!JTSAHelper.CopyClipBoad(CurrentTitleText))
            {
                processLog.ErrorLogWrite("「タイトル」クリップボードコピー処理失敗");
            }

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// ヘッダ部:取得ボタン（クリック）
        /// </summary>
        private async void GetTitleButton_Click(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "ヘッダ部:取得ボタン（クリック）");
            processLog.EventStartLogWrite();

            // 配信概要取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
            if (streamInfo == null) { processLog.ErrorLogWrite("配信概要未取得"); return; }

            // カテゴリ取得処理
            var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);
            if (category == null) { processLog.ErrorLogWrite("カテゴリー未取得"); return; }

            // ヘッダ部分の表示更新
            CurrentTitleText = streamInfo.title;
            CurrentCategoryId = category.Id;
            CurrentCategoryName = category.Name;
            CurrentCategoryBoxArtUrl = category.BoxArtUrl;

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// タイトル編集パネル:履歴アイテム（クリック）
        /// </summary>
        private void TitleTextLogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトル編集パネル:履歴アイテム（クリック）");
            processLog.EventStartLogWrite();


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

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// タイトル編集パネル:削除ボタン（クリック）
        /// </summary>
        private void TitleTextLogDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトル編集パネル:削除ボタン（クリック）");
            processLog.EventStartLogWrite();

            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is TitleTextForm item)
            {
                DAO_TitleText.Delete(item.Id);
            }

            ReloadTitleText();

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// ヘッダ部:X投稿ボタン（クリック）
        /// </summary>
        private void TweetButton_Click(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "ヘッダ部:X投稿ボタン（クリック）");
            processLog.EventStartLogWrite();

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

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// タイトル編集パネル:タイトル編集テキストボックス（テキスト変更）
        /// </summary>
        private void TitleEditTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトル編集パネル:タイトル編集テキストボックス（テキスト変更）");
            processLog.EventStartLogWrite();

            CurrentTitleText = TitleEditTextBox.Text;

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// 認証画面:トークンコピーボタン（クリック）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TokenCodeCopyButton_Click(object sender, RoutedEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "認証画面:トークンコピーボタン（クリック）");
            processLog.EventStartLogWrite();

            JTSAHelper.CopyClipBoad(LoadPanelSubTextBox.Text);

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectCategpryNameTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //【プロセス開始ログ】
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "ヘッダ部:DBフォルダオープンボタン（クリック）");
            processLog.EventStartLogWrite();

            AppLogPanel.AddSwitchLog(JTSAHelper.CopyClipBoad(SelectCategoryNameTextBlock.Text), GetType().Name,
                "クリップボードコピー成功 「 カテゴリ 」",
                "クリップボードコピー失敗 「 カテゴリ 」"
            );

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }

        #endregion


        #region ===============publicメソッド===============

        /// <summary>
        /// 配信者情報設定処理
        /// </summary>
        /// <param name="userName"></param>
        public async Task StreamerDataSet()
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "配信者情報設定処理");

            // タイトル取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(TwitchHelper.BroadcasterId);
            if (streamInfo == null)
            {
                processLog.ErrorLogWrite("配信情報の取得に失敗");
                return;
            }

            CurrentTitleText = streamInfo.title;
            TitleEditTextBox.Text = CurrentTitleTextBlock.Text;

            var dbCategoryData = DAO_Category.SelectOneById(streamInfo.gameId);
            if (dbCategoryData == null)
            {
                // DBに未登録のカテゴリなので、Twitch/IGDBから取得して組み立てる
                var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);
                if (category == null)
                {
                    // カテゴリ未設定で配信している場合などはここに来る。タイトルだけ反映して終了する
                    processLog.ErrorLogWrite("カテゴリ情報の取得に失敗");

                    CurrentTitleText = TitleEditTextBox.Text;
                    ReloadTitleText();
                    TitleTagSidePanel.ReloadTitleTag();

                    return;
                }

                var steamUrl = await IgdbService.GetSteamUrlsAsync(category.Id);

                dbCategoryData = new M_Category
                {
                    CategoryId = category.Id,
                    DisplayName = category.Name,
                    SteamUrl = steamUrl?.FirstOrDefault() ?? string.Empty,
                    BoxArtUrl = category.BoxArtUrl,
                    LastUsedDateTime = DateTime.Now,
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now
                };
            }

            CurrentTitleText = TitleEditTextBox.Text;

            CurrentCategoryId = dbCategoryData.CategoryId;
            CurrentCategoryName = dbCategoryData.DisplayName;
            CurrentCategoryBoxArtUrl = dbCategoryData.BoxArtUrl;
            CurrentCategorySteamUrl = dbCategoryData.SteamUrl;


            // リスト読み込み処理
            ReloadTitleText();
            TitleTagSidePanel.ReloadTitleTag();

            processLog.SuccessLogWrite();
        }


        /// <summary>
        /// タイトルログ再読み込み処理
        /// </summary>
        public void ReloadTitleText()
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトルログ再読み込み処理");

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

            processLog.SuccessLogWrite();
        }

        /// <summary>
        /// 配信概要パネルのカテゴリ一覧から現在のカテゴリを選択する。
        /// </summary>
        private void OverviewCategoryListBox_MouseDoubleClick(object sender, EventArgs e)
        {
            if (OverviewCategoryListBox.SelectedItem is not CategoryForm selectedItem) return;

            CurrentCategoryId = selectedItem.CategoryId;
            CurrentCategoryName = selectedItem.DisplayName;
            CurrentCategoryBoxArtUrl = selectedItem.BoxArtUrl;
            CurrentCategorySteamUrl = selectedItem.SteamUrl;

            OverviewCategoryListBox.SelectedIndex = -1;
        }


        /// <summary>
        /// カテゴリ連動処理
        /// 
        /// カテゴリに紐づいたチャンネルポイントプリセットを適用する。
        /// カテゴリにプリセットが紐づいていない場合は何もしない。
        ///
        /// カテゴリを切り替える経路（タイトル送信・プレイリストの「プレイ中」）から呼ばれる。
        /// </summary>
        /// <param name="categoryId">切り替え後のカテゴリID</param>
        public async Task ApplyChannelPointPresetForCategoryAsync(string categoryId)
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "カテゴリ連動処理");

            var result = await ChannelPointService.ApplyPresetForCategoryAsync(categoryId);

            // 紐づけが無い場合はnullが返る。この場合は正常なので何も表示しない
            if (result == null) 
            {
                processLog.ErrorLogWrite("カテゴリ連動失敗：APIの戻り値がnull");
                return;
            }

            if (!result.IsSuccess)
            {
                processLog.ErrorLogWrite("カテゴリ連動失敗：" + result.SummaryText + "：" + result.ErrorMessage);
            }

            // 適用によって有効/無効が変わっているのでCPタブの一覧を作り直す
            await ChannelPointPanel.ReloadChannnelPoint();

            processLog.SuccessLogWrite();
        }


        /// <summary>
        /// タイトルテキスト編集カーソル位置挿入処理
        /// </summary>
        /// <param name="insertText"></param>
        public void InsertTextAtCaret(string insertText)
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトルテキスト編集カーソル位置挿入処理");

            // TitleEditTextBoxがnullでないことを確認
            if (TitleEditTextBox == null)
            {
                processLog.ErrorLogWrite("タイトルテキストボックスが存在しない");
                return;
            }

            int currentIndex = TitleEditTextBox.SelectionStart;
            string original = TitleEditTextBox.Text ?? "";

            // 挿入処理
            TitleEditTextBox.Text =
                original.Substring(0, currentIndex) +
                insertText +
                original.Substring(currentIndex);

            // 挿入後のカーソル位置を調整
            TitleEditTextBox.SelectionStart = currentIndex + insertText.Length;
            TitleEditTextBox.Focus();

            processLog.SuccessLogWrite();
        }


        /// <summary>
        /// タイトルテキストプレビューの更新処理
        /// </summary>
        public void CurrentTitleTextUpdate()
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトルテキストプレビューの更新処理");

            CurrentTitleTextBlock.Text = TitleTextFriendTagReplace(TitleEditTextBox.Text);
            TitleWordNum.Content = CurrentTitleTextBlock.Text.Count() + "/140";

            processLog.SuccessLogWrite();
        }

        #endregion


        #region ===============privateメソッド===============

        /// <summary>
        /// SteamURLテキスト登録処理
        /// 
        /// カテゴリIDからSteamのストアURLを引いて画面に反映する。
        /// Art や Software and Game Development のような非ゲームカテゴリはSteamに存在しないため、
        /// 取得できないことは異常ではない（その場合は空欄にする）。
        /// </summary>
        private async void SteamUrlTextSet(string categoryId)
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "SteamURLテキスト登録処理");

            CurrentCategorySteamUrl = "";

            if (string.IsNullOrWhiteSpace(categoryId))
            {
                processLog.ErrorLogWrite($"categoryIdが未設定");
                return;
            }

			try
			{
				var result = await IgdbService.GetSteamUrlsAsync(categoryId);
				CurrentCategorySteamUrl = result.FirstOrDefault() ?? "";
			}
			catch (Exception)
			{
                // async voidのため、ここで握らないと未処理例外でアプリが落ちる
                processLog.ErrorLogWrite($"SteamURL取得失敗 「 {categoryId} 」");
			}

            processLog.SuccessLogWrite();
        }


		/// <summary>
		/// タイトルログ追加処理
		/// </summary>
		/// <param name="title"></param>
		private void AddTitleText(string content, string categoryId, string categoryName, string categoryBoxArtUrl)
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトルログ追加処理");

            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (string.IsNullOrWhiteSpace(content))
            {
                processLog.ErrorLogWrite("追加用テキストが未設定");
                return;
            }

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
            if (!DAO_TitleText.Insert(isnertData))
            {
                processLog.ErrorLogWrite("タイトルログ追加処理失敗");
            }

			// 再読み込み処理
			ReloadTitleText();

            processLog.SuccessLogWrite();
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

        #endregion


        #region ===============認証関連処理===============

        /// <summary>
        /// アクセストークン取得後の初期化処理。
        /// 起動時（リフレッシュトークン経由）とOAuth認証直後の両方から呼ばれる共通処理。
        ///
        /// 以前はこの処理が起動時シーケンスにしか無く、OAuth認証直後は
        /// BroadcasterIdもIgdbServiceも未設定のままStreamerDataSet()を呼んで落ちていた。
        /// </summary>
        private async Task InitializeAfterAuthAsync()
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "アクセストークン取得後初期化処理");

            // 配信者情報の取得（アクセストークンの持ち主＝配信者本人）
            var streamerInfo = await TwitchHelper.GetAuthenticatedUserAsync();
            if (streamerInfo is null)
            {
                processLog.CriticalErrorLogWrite("配信者情報未取得");

                BroadcasterId_TextBlock.Text = "NG";
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

            // メモリに登録
            TwitchHelper.BroadcasterId = streamerInfo.UserId;
            BroadcasterId_TextBlock.Text = "OK!";

            JTSAHelper.LoginName = streamerInfo.Login;
            UserName_TextBox.Text = JTSAHelper.LoginName;

            // 表示・Twitchダッシュボードのリンク用に保存しておく
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.UserName, JTSAHelper.LoginName);

            IgdbService.Initialize(new HttpClient(), TwitchHelper.ClientID, TwitchHelper.AccessToken);

            // アクセストークンの確認を持って起動時設定を完了
            await StreamerDataSet();

            // 各パネルの初期化処理
            ChatPanel.Initialize();
            CategoryPanel.Initialize();
            PlayingGamePanel.BindExistingCategoryList(CategoryPanel.CategoryFormList);
            await ChannelPointPanel.Initialize();

            PlayingGamePanel.ReloadPlaylistHeader();
            PlayingGamePanel.ReloadGamePlaylistItem();

            // ロード画面を非表示
            LoadPanelTextBlock.Text = "Loading Now...";
            LoadScreen.Visibility = Visibility.Collapsed;
            LoadSubPanel.Visibility = Visibility.Collapsed;

            processLog.SuccessLogWrite("処理完了");
        }


        /// <summary>
        /// アクセストークンの再取得
        /// </summary>
        private async Task<string> ResetAccessTokenAsync()
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "アクセストークン再取得処理");

            // リフレッシュトークンの取得（設定に無ければ失敗として戻す）
            M_Setting? settingRefreshToken = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RefreshToken);
            if (settingRefreshToken == null)
            {
                processLog.ErrorLogWrite("リフレッシュトークン未設定");
                return null;
            }

            var accessTokenResponse = await TwitchHelper.RefreshAccessTokenAsync(settingRefreshToken.Value);
            if (accessTokenResponse == null)
            {
                processLog.ErrorLogWrite("アクセストークン未取得");
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

            processLog.SuccessLogWrite();
            return accessTokenResponse.accessToken;
        }

        #endregion
    }
}
