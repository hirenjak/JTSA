using JTSA.Models;
using JTSA.Panels;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace JTSA
{
	public partial class MainWindow : Window
	{
		/// <summary>  </summary>
		public ObservableCollection<TitleTextForm> TitleTextFormList { get; } = new();

		public EditTitleTextForm editTitleTextForm;


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

			AllSidePanelClose();
			TitleTagSidePanel.Visibility = Visibility.Visible;

            editTitleTextForm = new()
			{
				Content = "",
				CategoryId = "",
				CategoryName = "",
				CategoryBoxArtUrl = ""
            };	

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
			LoadSubPanel.Visibility = Visibility.Collapsed;


            // クライアントID存在チェック
            if (string.IsNullOrEmpty(TwitchHelper.ClientID)) return;

			M_Setting tempSettingObj;

			// ユーザー名取得確認
			tempSettingObj = M_Setting.SelectOneById(M_Setting.SettingName.UserName) ?? new()
			{
				Name = 9999,
				Value = ""
            };

			if (tempSettingObj == null || String.IsNullOrEmpty(tempSettingObj.Value))
			{
				StatusTextBlock.Text = "ユーザー名が設定されていません";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
				LoadSubPanel.Visibility = Visibility.Visible;
				return;
			}
			else
			{
				StatusTextBlock.Text = "ユーザー名を取得";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
			}

			Utility.UserName = tempSettingObj.Value;
			UserName_TextBox.Text = Utility.UserName;

			// リフレッシュトークンからアクセストークンを再取得
			await ResetAccessTokenAsync();

            await StreamerDataSet();
			
			LoadScreen.Visibility = Visibility.Collapsed;
		}


        /// <summary>
        /// アクセストークンの再取得
        /// </summary>
        /// <returns></returns>
        private async Task ResetAccessTokenAsync()
		{

            var settingUserName = M_Setting.SelectOneById(M_Setting.SettingName.RefreshToken);
            if (settingUserName == null || String.IsNullOrEmpty(settingUserName.Value)) return;

            var accessTokenResponse = await TwitchHelper.RefreshAccessTokenAsync(settingUserName.Value);

            if (string.IsNullOrEmpty(accessTokenResponse.accessToken))
            {
                StatusTextBlock.Text = "アクセストークンの取得に失敗";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }
            else
            {
                StatusTextBlock.Text = "アクセストークンを取得";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            }

            TwitchHelper.AccessToken = accessTokenResponse.accessToken;

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
				StatusTextBlock.Text = "broadcaster_idの取得に失敗しました。";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
				return;
			}

			bool isExistAccessToken =!String.IsNullOrEmpty(TwitchHelper.AccessToken);

			if (isExistAccessToken)
			{
				StatusTextBlock.Text = "アクセストークンを取得";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
				AccessToken_TextBlock.Text = "OK!";

				// タイトル取得処理
				CurrentTitleTextBlock.Text = await TwitchHelper.GetTwitchTitle() ?? "";

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
					CategoryBoxArtUrl = category.BoxArtUrl
				};

				CategorySidePanel.ReloadCategory();

				SetEditTitleTextForm();
			}
			else
			{
				StatusTextBlock.Text = "アクセストークンの取得に失敗";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
				AccessToken_TextBlock.Text = "NG";
			}

			// リスト読み込み処理
			ReloadTitleText();
			TitleTagSidePanel.ReloadTitleTag();
			FriendSidePanel.ReloadFriend();
			CategorySidePanel.ReloadCategory();
			SaveTitleSidePanel.ReloadSaveTitleText();
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

			var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();

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

				StatusTextBlock.Text = "アクセストークンを取得";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
				AccessToken_TextBlock.Text = "OK!";
			}
			else
			{
				StatusTextBlock.Text = "アクセストークンの取得に失敗";
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
		public void ReloadTitleText()
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

			StatusTextBlock.Text = "タイトルログリストを読込";
			StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
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
			DisplayLog(M_TitleText.Insert(isnertData),
				"データを追加しました。",
				"既にデータが存在します。"
			);

			// 再読み込み処理
			ReloadTitleText();
		}

		#endregion


		#region =============== メインパネル：編集部分 ===============

		/// <summary>
		/// 読み込み処理：編集部分
		/// </summary>
		public void SetEditTitleTextForm()
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

			SaveTitleSidePanel.ReloadSaveTitleText();
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
			editTitleTextForm.CategoryBoxArtUrl = category.BoxArtUrl;
			SetEditTitleTextForm();

			var title = await TwitchHelper.GetTwitchTitle();
			if (string.IsNullOrEmpty(title))
			{
				StatusTextBlock.Text = "配信概要に失敗しました。：";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
			}
			else
			{
				CurrentTitleTextBlock.Text = title;

				StatusTextBlock.Text = "配信概要を取得しました。";
				StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
			}
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

			CategorySidePanel.AddCategory(categoryId, categoryName, boxArtUrl);
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
		public void DisplayLog(bool isError, String successStr, String errorStr)
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


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TweetButton_Click(object sender, RoutedEventArgs e)
		{
			// 必要データの取得
			var stremTitleText = editTitleTextForm.Content;
			var categoryNameText = editTitleTextForm.CategoryName;

            // 認証URL生成
            var oauthUrl = $"https://x.com/intent/post?text=";
			var text = stremTitleText + "%0D%0A" + "配信カテゴリ：" + categoryNameText + "%0D%0A" + $"https://www.twitch.tv/" + Utility.UserName;

			// ブラウザで認証ページを開く
			Process.Start(new ProcessStartInfo
			{
				FileName = oauthUrl + text,
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
			if (editTitleTextForm == null) return;
			editTitleTextForm.Content = TitleEditTextBox.Text;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TokenCodeCopyButton_Click(object sender, RoutedEventArgs e)
		{
			Utility.CopyClipBoad(LoadPanelSubTextBox.Text);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void StreamAppAllStart(object sender, RoutedEventArgs e)
        {
            AppArrangePanel.RegistAllAppStart();
        }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void StreamAppAllStop(object sender, RoutedEventArgs e)
        {
            AppArrangePanel.RegistAllAppStop();
        }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void StreamAppAllMove(object sender, RoutedEventArgs e)
        {
			AppArrangePanel.RegistAppAllMove();
        }

        private void DBFolderOpen(object sender, RoutedEventArgs e)
        {
			OpenDbFolder();
        }

        // dbDirectoryをエクスプローラーで開くメソッド
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
    }
}