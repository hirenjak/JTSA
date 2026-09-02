using JTSA.Dao;
using JTSA.Forms;
using JTSA.Forms.TwitchIF;
using JTSA.Models;
using JTSA.Panels;
using JTSA.Utility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NAudio;
using NAudio.Utils;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;

namespace JTSA
{
	/// <summary>
	/// メインウィンドウ
	/// </summary>
	public partial class MainWindow : Window
	{
		private const int DwmWindowCornerPreference = 33;
		private const int DwmWindowCornerDoNotRound = 1;
		public const string DefaultObsWebSocketUrl = "ws://127.0.0.1:4455";

		private readonly ObsController mainObsController = new();
		private readonly ObsController subObsController = new();
        private readonly SemaphoreSlim mainObsConnectionLock = new(1, 1);
        private readonly SemaphoreSlim subObsConnectionLock = new(1, 1);
        private readonly SemaphoreSlim twitchAccountTokenLock = new(1, 1);
        private bool isSteamGameLaunching;
        private readonly StreamExpansionService streamExpansionService = new();
		private bool isObsOperationRunning;
        private bool isAccountAwarePanelsInitialized;
        private int accountSwitchLoadingCount;
        private bool isObsStreaming;
        private bool? mainObsLastStreamingState;
        private bool? subObsLastStreamingState;
        private bool mainObsHasConnected;
        private bool subObsHasConnected;

		/// <summary> タイトルログ用のリスト  </summary>
		public ObservableCollection<TitleTextForm> TitleTextFormList { get; } = new();

		/// <summary> アクセストークンの再取得用タイマ </summary>
		private DispatcherTimer accessTokenRefreshTimer;

        /// <summary> 現在の配信状態を定期更新するタイマ </summary>
        private readonly DispatcherTimer streamStatusTimer;
        private readonly DispatcherTimer streamDurationTimer;
        private bool isStreamStatusUpdating;
        private DateTime? nextStreamStatusUpdateAtUtc;
        private DateTime? currentStreamStartedAtUtc;
        private int? currentViewerCount;
        private bool isViewerCountHidden;
        private const int SecretPanelClickCount = 10;
        private static readonly TimeSpan SecretPanelClickInterval = TimeSpan.FromSeconds(2);
        private int viewerCountConsecutiveClicks;
        private DateTime lastViewerCountClickUtc;
        private readonly DispatcherTimer twitchStatusHoldTimer;
        private bool isTwitchStatusHeld;
		private string currentCategoryId = string.Empty;
        private readonly Dictionary<FrameworkElement, ToolPanelWindow> toolPanelWindows = new();

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var preference = DwmWindowCornerDoNotRound;
            DwmSetWindowAttribute(
                new WindowInteropHelper(this).Handle,
                DwmWindowCornerPreference,
                ref preference,
                Marshal.SizeOf<int>());
        }

        private void MainMinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MainMaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);

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
                SetTwitchSettingApplied(false);
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
				SteamUrlTextBlock.Text = value ?? string.Empty;
				if (LaunchSteamGameButton is not null)
					LaunchSteamGameButton.IsEnabled = !isSteamGameLaunching &&
						SteamHelper.GetSteamAppId(value ?? string.Empty) is not null;
				
			}
		}

		/// <summary> ヘッダ部分：現在のカテゴリID </summary>
		public string CurrentCategoryId
		{
			get
			{
				return currentCategoryId;
			}

			set
			{
				currentCategoryId = value;
                SetTwitchSettingApplied(false);

				// カテゴリ設定をしたら同時にSteamURLを取得して設定
                SteamUrlTextSet(value);

                // Twitchへの反映を待たず、アプリ上でカテゴリを選択した時点でOBSを切り替える。
                if (!string.IsNullOrWhiteSpace(value) && ObsSettingPanel is not null)
                    _ = ObsSettingPanel.ApplyCaptureRuleForCategoryAsync(value);
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
                CurrentTitleTextUpdate();
            }
		}

        private string CurrentCategoryJapaneseName
        {
            get
            {
                var category = DAO_Category.SelectOneById(CurrentCategoryId);
                return string.IsNullOrWhiteSpace(category?.JapaneseDisplayName)
                    ? CurrentCategoryName
                    : category.JapaneseDisplayName;
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
            // XAML 内の子コントロールは InitializeComponent 中に生成され、
            // そのコンストラクタから設定テーブルを参照するため、先に DB を初期化する。
            using (var db = new AppDbContext())
            {
                ClearAbandonedMigrationLock(db);
                db.RepairLegacyMigrationHistory();
                db.Database.Migrate();
            }

            // WPF上の初期化処理
			InitializeComponent();
            DataContext = this;
            CalendarPanel.AddRequested += CalendarPanel_AddRequested;
            CalendarPanel.EditRequested += CalendarPanel_EditRequested;
            twitchStatusHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            twitchStatusHoldTimer.Tick += TwitchStatusHoldTimer_Tick;
            RestoreWindowPosition();
            mainObsController.StreamingStateChanged += isStreaming =>
            {
                HandleObsStreamingStateEvent(mainObsController, isSub: false, isStreaming);
            };
            subObsController.StreamingStateChanged += isStreaming =>
            {
                HandleObsStreamingStateEvent(subObsController, isSub: true, isStreaming);
            };

            // タイトルのバージョン設定
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"JakTwtchStreamerAssistant v{version?.ToString(3)}";

            #region ==========アクセストークンの自動リフレッシュタイマー設定==========

            accessTokenRefreshTimer = new DispatcherTimer();
            accessTokenRefreshTimer.Interval = TimeSpan.FromHours(3);
            accessTokenRefreshTimer.Tick += AccessTokenRefreshTimer_TickAsync;
            accessTokenRefreshTimer.Start();

            streamStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            streamStatusTimer.Tick += async (_, _) => await UpdateStreamStatusAsync();
            streamStatusTimer.Start();

            // APIの取得間隔中も、取得済みの配信開始時刻を基準に表示を進める。
            streamDurationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            streamDurationTimer.Tick += (_, _) =>
            {
                UpdateDisplayedStreamDuration();
                UpdateStreamStatusCountdown();
            };
            streamDurationTimer.Start();

            #endregion


            #region ==========イベントハンドラ設定==========

            Loaded += MainWindow_LoadedAsync;
            SizeChanged += MainWindow_SizeChanged;
            Closing += (_, _) => SaveWindowPosition();
            Closed += (_, _) =>
            {
                mainObsController.Dispose();
                subObsController.Dispose();
            };
            SteamUrlTextBlock.MouseLeftButtonUp += SteamUrlTextBlock_MouseLeftButtonUp;

            #endregion
        }

        /// <summary>前回終了時のメインウィンドウ位置を復元する。</summary>
        private void RestoreWindowPosition()
        {
            var savedX = DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainWindowPosX)?.Value;
            var savedY = DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainWindowPosY)?.Value;

            if (!double.TryParse(savedX, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(savedY, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return;
            }

            // モニター構成が変わった場合、タイトルバーを含むウィンドウ全体が
            // 画面外に残らないよう、保存位置を使わず WPF の既定位置で開く。
            var isVisible =
                x < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                y < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight &&
                x + Width > SystemParameters.VirtualScreenLeft &&
                y + Height > SystemParameters.VirtualScreenTop;

            if (!isVisible)
            {
                return;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = x;
            Top = y;
        }

        /// <summary>次回起動用にメインウィンドウ位置を保存する。</summary>
        private void SaveWindowPosition()
        {
            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;

            if (double.IsNaN(bounds.X) || double.IsNaN(bounds.Y) ||
                double.IsInfinity(bounds.X) || double.IsInfinity(bounds.Y))
            {
                return;
            }

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.MainWindowPosX,
                bounds.X.ToString(CultureInfo.InvariantCulture));
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.MainWindowPosY,
                bounds.Y.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// アクセストークンを定期更新する。DispatcherTimer の async void ハンドラから
        /// 例外を外へ漏らすとアプリ全体の未処理例外になるため、ここで記録して処理する。
        /// </summary>
        private async void AccessTokenRefreshTimer_TickAsync(object? sender, EventArgs e)
        {
            accessTokenRefreshTimer.Stop();
            try
            {
                var accessToken = await ResetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    LoadSubPanel.Visibility = Visibility.Visible;
                    return;
                }

                TwitchHelper.AccessToken = accessToken;
                SettingPanel.SetAccessTokenStatus(true);

                // ChatPanel はアカウント切替時のアクセストークンを保持しているため、
                // 定期更新後も古いトークンでチャットを送信しないよう同期する。
                var primaryAccount = DAO_TwitchAccount.SelectPrimary();
                if (primaryAccount is not null)
                {
                    ChatPanel.UpdateConnectedAccessToken(
                        primaryAccount.BroadcasterId,
                        accessToken);
                }

                // 追加アカウントを選択中の場合、そのアカウントのトークンも更新する。
                if (SelectedTargetAccountId is long selectedAccountId &&
                    selectedAccountId != primaryAccount?.Id)
                {
                    var selectedAccount = await GetSelectedTargetAccountAsync();
                    if (selectedAccount is not null)
                    {
                        ChatPanel.UpdateConnectedAccessToken(
                            selectedAccount.Value.Account.BroadcasterId,
                            selectedAccount.Value.AccessToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // 一時的なDB競合や通信障害でアプリ全体を終了させない。
                AppLogPanel.Error(
                    GetType().Name,
                    $"アクセストークンの自動更新に失敗しました。{ex.GetBaseException().Message}");
            }
            finally
            {
                accessTokenRefreshTimer.Start();
            }
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

        /// <summary>ウィンドウ幅に合わせてヘッダーをコンパクト表示へ切り替える。</summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var isCompact = e.NewSize.Width < 1000;

            CategoryHeaderColumn.Width = new GridLength(isCompact ? 150 : 180);
            TitleHeaderColumn.Width = new GridLength(isCompact ? 240 : 480);

            SelectCategoryNameTextBlock.FontSize = isCompact ? 8 : 9;
            CurrentTitleTextBlock.FontSize = isCompact ? 9 : 12;
            StreamStatusGrid.Margin = isCompact
                ? new Thickness(4, 4, 0, 4)
                : new Thickness(8, 4, 0, 4);

        }

        /// <summary>
        /// 【イベント】コンストラクタ終了時の処理
        /// </summary>
        private async void MainWindow_LoadedAsync(object sender, RoutedEventArgs e)
        {
            ApplyObsSceneShortcutPanelVisibility(
                DAO_Setting.SelectOneById(
                    DAO_Setting.SettingName.ObsSceneShortcutPanelVisible)?.Value != "0");
            RefreshObsSceneShortcutButtons();
            RefreshObsSourceShortcutButtons();
            SetAccountSwitchLoading(true, isStartup: true);
            try
            {
                await MainWindowLoadedCoreAsync(sender, e);
            }
            catch (Exception ex)
            {
                AppLogPanel.Error(
                    GetType().Name,
                    $"起動時の読み込みに失敗しました。{ex.GetBaseException().Message}");
                LoadSubPanel.Visibility = Visibility.Visible;
            }
            finally
            {
                SetAccountSwitchLoading(false);
            }
        }

        public void RefreshObsSceneShortcutButtons()
        {
            long? accountId = TargetAccountComboBox.SelectedValue is long selectedAccountId
                ? selectedAccountId
                : null;
            var presets = ObsSettingPanel.GetSceneSwitchPresets(accountId);
            ObsSettingPanel.RefreshSceneSwitchPresetFilter(accountId);
            var mainPresets = presets.Where(preset => !preset.IsSub).ToList();
            var subPresets = presets.Where(preset => preset.IsSub).ToList();
            MainObsSceneShortcutItemsControl.ItemsSource = mainPresets;
            SubObsSceneShortcutItemsControl.ItemsSource = subPresets;
            MainObsSceneShortcutItemsControl.Visibility = mainPresets.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
            SubObsSceneShortcutItemsControl.Visibility = subPresets.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
            ObsSceneShortcutEmptyTextBlock.Visibility = presets.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            ObsSceneShortcutScrollViewer.Visibility = presets.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateObsShortcutButtonStates();
        }

        public long? SelectedTargetAccountId =>
            TargetAccountComboBox.SelectedValue is long accountId ? accountId : null;

        public void RefreshObsSourceShortcutButtons()
        {
            var accountId = SelectedTargetAccountId;
            var presets = ObsSettingPanel.GetSourceSwitchPresets(accountId);
            ObsSettingPanel.RefreshSourceSwitchPresetFilter(accountId);
            var mainPresets = presets.Where(preset => !preset.IsSub).ToList();
            var subPresets = presets.Where(preset => preset.IsSub).ToList();
            MainObsSourceShortcutItemsControl.ItemsSource = mainPresets;
            SubObsSourceShortcutItemsControl.ItemsSource = subPresets;
            MainObsSourceShortcutItemsControl.Visibility = mainPresets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            SubObsSourceShortcutItemsControl.Visibility = subPresets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            ObsSourceShortcutEmptyTextBlock.Visibility = presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ObsSourceShortcutScrollViewer.Visibility = presets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            UpdateObsShortcutButtonStates();
        }

        private void UpdateObsShortcutButtonStates()
        {
            MainObsSceneShortcutItemsControl.IsEnabled = mainObsController.IsConnected;
            MainObsSourceShortcutItemsControl.IsEnabled = mainObsController.IsConnected;
            SubObsSceneShortcutItemsControl.IsEnabled = subObsController.IsConnected;
            SubObsSourceShortcutItemsControl.IsEnabled = subObsController.IsConnected;
        }

        private async void ObsSourceShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ObsSettingPanel.SourceSwitchPreset preset) return;
            await ObsSettingPanel.ExecuteSourceSwitchPresetAsync(preset);
        }

        private async void ObsSceneShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ObsSettingPanel.SceneSwitchPreset preset) return;
            await ObsSettingPanel.ExecuteSceneSwitchPresetAsync(preset);
        }

        private void OpenObsSceneSwitchWindowButton_Click(object sender, RoutedEventArgs e)
            => OpenObsSwitchSettingsWindow(showSourceSwitch: false);

        private void OpenObsSourceSwitchWindowButton_Click(object sender, RoutedEventArgs e)
            => OpenObsSwitchSettingsWindow(showSourceSwitch: true);

        private void OpenObsSwitchSettingsWindow(bool showSourceSwitch)
        {
            var window = new ObsSwitchSettingsWindow(showSourceSwitch)
            {
                Owner = this
            };

            window.ShowDialog();
            ObsSettingPanel.ReloadSwitchPresets();
            RefreshObsSceneShortcutButtons();
            RefreshObsSourceShortcutButtons();
        }

        private void ObsSceneShortcutToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var shouldShow = ObsShortcutPanel.Visibility != Visibility.Visible;
            ApplyObsSceneShortcutPanelVisibility(shouldShow);
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.ObsSceneShortcutPanelVisible,
                shouldShow ? "1" : "0");
        }

        private void ApplyObsSceneShortcutPanelVisibility(bool shouldShow)
        {
            ObsShortcutPanel.Visibility = shouldShow
                ? Visibility.Visible
                : Visibility.Collapsed;
            ObsSceneShortcutToggleButton.Content = "ショートカット ☰";
            ObsSceneShortcutToggleButton.Background = shouldShow
                ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
                : new SolidColorBrush(Color.FromRgb(86, 86, 86));
            ObsSceneShortcutToggleButton.Foreground = Brushes.White;
            ObsSceneShortcutToggleButton.BorderBrush = shouldShow
                ? new SolidColorBrush(Color.FromRgb(85, 85, 85))
                : new SolidColorBrush(Color.FromRgb(119, 119, 119));
            ObsSceneShortcutToggleButton.BorderThickness = shouldShow
                ? new Thickness(1, 1, 1, 0)
                : new Thickness(1);
            ObsShortcutPanel.BorderThickness = shouldShow
                ? new Thickness(1, 0, 1, 1)
                : new Thickness(1);
            if (shouldShow)
                _ = ObsSettingPanel.RefreshSourceVisibilityStatesAsync(SelectedTargetAccountId);
        }

        private async Task MainWindowLoadedCoreAsync(object sender, RoutedEventArgs e)
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
            var settingRefreshToken = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RefreshToken)?.Value;
            var primaryAccountRefreshToken = DAO_TwitchAccount.SelectPrimary()?.RefreshToken;
            if (string.IsNullOrWhiteSpace(settingRefreshToken) &&
                string.IsNullOrWhiteSpace(primaryAccountRefreshToken))
            {
                processLog.CriticalErrorLogWrite("未認証（OAuth認証が必要）");

                SettingPanel.SetAccessTokenStatus(false);
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

            // リフレッシュトークンからアクセストークンを再取得
            string accessToken = await ResetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                processLog.CriticalErrorLogWrite("アクセストークン未取得");

                SettingPanel.SetAccessTokenStatus(false);
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

            // メモリに登録
            TwitchHelper.AccessToken = accessToken;
            SettingPanel.SetAccessTokenStatus(true);

            // 認証後の初期化（OAuth認証直後と共通）
            await InitializeAfterAuthAsync();

            // OBSは補助機能なので、Twitch画面・チャットなど本体の初期化完了後、
            // UIが落ち着いてから低優先で自動接続する。
            _ = AutoConnectObsAfterStartupAsync();

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }

        private async Task AutoConnectObsAfterStartupAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await AutoConnectObsAsync();
        }

        private async Task AutoConnectObsAsync()
        {
            var legacyAutoConnect = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsAutoConnect)?.Value;
            var mainAutoConnect =
                (DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainObsAutoConnect)?.Value ?? legacyAutoConnect) == "1";
            var subAutoConnect =
                (DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsAutoConnect)?.Value ?? legacyAutoConnect) == "1";

            if (mainAutoConnect && DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainObsTwitchAccountId) is not null)
            {
                await ConnectObsAsync(forceReconnect: false);
                if (mainObsController.IsConnected)
                    await ObsSettingPanel.RefreshSavedTextSourcesAsync(mainObsController, isSub: false);
            }

            if (subAutoConnect && long.TryParse(
                DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsTwitchAccountId)?.Value,
                out _))
            {
                await ConnectObsAsync(forceReconnect: false, isSub: true);
                if (subObsController.IsConnected)
                    await ObsSettingPanel.RefreshSavedTextSourcesAsync(subObsController, isSub: true);
            }
        }

        /// <summary>
        /// Twitch API が401/403を返した場合、モーダルダイアログを出さずに
        /// OAuth再認証画面へ強制的に切り替える。
        /// </summary>
        public void RequireOAuthReauthentication(string reason, string responseDetail = "")
        {
            // 認証エラー後もチャットイベントからAPI呼び出しが連打されないようにする。
            TwitchHelper.AccessToken = string.Empty;
            SettingPanel.SetAccessTokenStatus(false);
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
                SettingPanel.SetAccessTokenStatus(false);
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
                SettingPanel.SetAccessTokenStatus(false);
                return;
            }

            TwitchHelper.AccessToken = accessTokenResponse.accessToken;
            SettingPanel.SetAccessTokenStatus(true);

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
            var categoryId = CurrentCategoryId;
            var categoryName = SelectCategoryNameTextBlock.Text;
            var categoryBoxArtUrl = SelectCategoryBoxArt.Source?.ToString() ?? "";  // ボックスアートが無いカテゴリではSourceがnullになる

            var target = await GetSelectedTargetAccountAsync();
            if (target is null)
            {
                processLog.ErrorLogWrite("送信先アカウントの認証情報を取得できませんでした");
                return;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", target.Value.AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", TwitchHelper.ClientID);

            var content = new StringContent(JsonSerializer.Serialize(new { title = title }), Encoding.UTF8, "application/json");

            // TwitchAPIで配信タイトルを更新
            var response = await client.PatchAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={target.Value.Account.BroadcasterId}", content);
            var titleApplied = response.IsSuccessStatusCode;
            if (titleApplied)
            {
                // 履歴追加処理
                AddTitleText(
                    TitleEditTextBox.Text,
                    TitlePlaceholderTextBox.Text,
                    categoryId,
                    categoryName,
                    categoryBoxArtUrl);
            }
            else
            {
                processLog.ErrorLogWrite($"配信概要送信:{(int)response.StatusCode}:{response.StatusCode}");
            }

            // カテゴリ設定処理
            string gameId = CurrentCategoryId.Trim();
            var categoryApplied = await TwitchHelper.SetCategoryAsync(gameId, target.Value.Account.BroadcasterId, target.Value.AccessToken);
            if (!categoryApplied)
            {
                processLog.ErrorLogWrite("カテゴリ設定処理失敗");
            }

            // タイトル取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(target.Value.Account.BroadcasterId, target.Value.AccessToken);
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
            // チャンネルポイント機能は常駐中のメインアカウントへ接続しているため、
            // サブアカウント送信時にメイン側の報酬を誤変更しない。
            if (target.Value.Account.BroadcasterId == TwitchHelper.BroadcasterId)
                await ApplyChannelPointPresetForCategoryAsync(getCategory.Id);

            SetTwitchSettingApplied(titleApplied && categoryApplied);

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

            var target = await GetSelectedTargetAccountAsync();
            if (target is null) { processLog.ErrorLogWrite("送信先アカウントの認証情報を取得できませんでした"); return; }

            // 配信概要取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(target.Value.Account.BroadcasterId, target.Value.AccessToken);
            if (streamInfo == null) { processLog.ErrorLogWrite("配信概要未取得"); return; }

            // カテゴリ取得処理
            var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);
            if (category == null) { processLog.ErrorLogWrite("カテゴリー未取得"); return; }

            // ヘッダ部分の表示更新
            CurrentTitleText = streamInfo.title;
            CurrentCategoryId = category.Id;
            CurrentCategoryName = category.Name;
            CurrentCategoryBoxArtUrl = category.BoxArtUrl;
            SetTwitchSettingApplied(true);

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }

        /// <summary>保存済み設定でOBSへ接続し、ヘッダーの操作状態を更新する。</summary>
        public async Task ConnectObsAsync(bool forceReconnect, bool isSub = false)
        {
            var connectionLock = isSub ? subObsConnectionLock : mainObsConnectionLock;
            var controller = isSub ? subObsController : mainObsController;
            ObsSettingPanel.SetConnectionStatus(false, "接続待機中...", isSub);
            await connectionLock.WaitAsync();
            isObsOperationRunning = true;
            ObsSettingPanel.SetConnectionStatus(false, "接続中...", isSub);
            if (IsSelectedObsController(controller))
                SetObsButtonState(enabled: false, isObsStreaming);
            try
            {
                if (forceReconnect)
                    await controller.DisconnectAsync();

                var urlSetting = isSub ? DAO_Setting.SettingName.SubObsWebSocketUrl : DAO_Setting.SettingName.ObsWebSocketUrl;
                var passwordSetting = isSub ? DAO_Setting.SettingName.SubObsWebSocketPassword : DAO_Setting.SettingName.ObsWebSocketPassword;
                var url = DAO_Setting.SelectOneById(urlSetting)?.Value
                    ?? (isSub ? "ws://127.0.0.1:4456" : DefaultObsWebSocketUrl);
                var password = DAO_Setting.SelectOneById(passwordSetting)?.Value ?? "";
                await controller.ConnectAsync(url, password);
                var connectedStreamingState = await Task.Run(controller.IsStreaming)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                if (isSub)
                {
                    subObsLastStreamingState = connectedStreamingState;
                    subObsHasConnected = true;
                }
                else
                {
                    mainObsLastStreamingState = connectedStreamingState;
                    mainObsHasConnected = true;
                }
                ObsSettingPanel.SetConnectionStatus(true, isSub: isSub);
                await ObsSettingPanel.RefreshSceneShortcutStatesAsync(SelectedTargetAccountId, isSub);
                await ObsSettingPanel.RefreshSourceVisibilityStatesAsync(SelectedTargetAccountId, isSub);
                var controlTarget = GetObsControlTarget();
                if (controlTarget is not null &&
                    ReferenceEquals(controlTarget.Value.Controller, controller))
                {
                    SetObsButtonState(enabled: true, connectedStreamingState);
                }
            }
            catch (Exception ex)
            {
                if (IsSelectedObsController(controller))
                    SetObsButtonState(enabled: false, isStreaming: false);
                ObsSettingPanel.SetConnectionStatus(false, "接続失敗", isSub);
                AppLogPanel.Error(GetType().Name, $"OBS接続失敗 「 {ex.GetBaseException().Message} 」");
            }
            finally
            {
                isObsOperationRunning = false;
                UpdateObsShortcutButtonStates();
                connectionLock.Release();
                await RefreshObsControlTargetAsync();
            }
        }

        private async Task<ObsController?> EnsureObsConnectedAsync()
        {
            var target = GetObsControlTarget();
            if (target is null)
            {
                MessageBox.Show("選択中のTwitchアカウントに操作対象のOBSが設定されていません。", "OBS連携");
                return null;
            }
            var (controller, isSub) = target.Value;
            if (!controller.IsConnected && CanAutomaticallyConnectObs(isSub))
                await ConnectObsAsync(forceReconnect: false, isSub);
            return controller.IsConnected ? controller : null;
        }

        public async Task<ObsController?> EnsureObsConnectedAsync(bool isSub)
        {
            var controller = isSub ? subObsController : mainObsController;
            if (!controller.IsConnected && CanAutomaticallyConnectObs(isSub))
                await ConnectObsAsync(forceReconnect: false, isSub);

            return controller.IsConnected ? controller : null;
        }

        /// <summary>接続処理を行わず、現在接続済みのOBSだけを返す。</summary>
        public ObsController? GetConnectedObsController(bool isSub)
        {
            var controller = isSub ? subObsController : mainObsController;
            return controller.IsConnected ? controller : null;
        }

        private bool CanAutomaticallyConnectObs(bool isSub)
        {
            if (isSub ? subObsHasConnected : mainObsHasConnected)
                return true;

            var legacyAutoConnect = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.ObsAutoConnect)?.Value;
            var setting = isSub
                ? DAO_Setting.SettingName.SubObsAutoConnect
                : DAO_Setting.SettingName.MainObsAutoConnect;
            return (DAO_Setting.SelectOneById(setting)?.Value ?? legacyAutoConnect) == "1";
        }

        public async Task SetObsTextSourceAsync(bool isSub, string sourceName, string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => SetObsTextSourceAsync(isSub, sourceName, text)).Task.Unwrap();
                return;
            }
            if (string.IsNullOrWhiteSpace(sourceName))
                throw new InvalidOperationException("OBSテキストソースが指定されていません。");
            var controller = await EnsureObsConnectedAsync(isSub)
                ?? throw new InvalidOperationException("OBSに接続できませんでした。");
            controller.SetTextSourceText(sourceName, text);
        }

        private async Task HandleObsStreamStartedAsync(bool isSub)
        {
            var obsName = isSub ? "sub" : "main";
            var settingName = isSub
                ? DAO_Setting.SettingName.SubObsTwitchAccountId
                : DAO_Setting.SettingName.MainObsTwitchAccountId;
            var accountIdText = DAO_Setting.SelectOneById(settingName)?.Value;
            var broadcasterId = string.Empty;
            var accessToken = string.Empty;
            if (long.TryParse(accountIdText, out var accountId))
            {
                var account = DAO_TwitchAccount.SelectById(accountId);
                if (account is not null)
                {
                    broadcasterId = account.BroadcasterId;
                    try
                    {
                        var token = await TwitchHelper.RefreshAccessTokenAsync(account.RefreshToken);
                        accessToken = token?.accessToken ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(token?.refreshToken))
                            DAO_TwitchAccount.UpdateRefreshToken(account.Id, token.refreshToken);
                    }
                    catch (Exception ex)
                    {
                        _ = Dispatcher.BeginInvoke(() => AppLogPanel.Error(
                            GetType().Name,
                            $"OBS配信開始時のTwitch認証更新失敗 「 {ex.GetBaseException().Message} 」"));
                    }
                }
            }
            await streamExpansionService.HandleAsync(
                StreamExpansionTriggerType.ObsStreamStart, obsName,
                triggerObs: obsName, broadcasterId: broadcasterId, accessToken: accessToken);
        }

        private void HandleObsStreamingStateEvent(ObsController controller, bool isSub, bool isStreaming)
        {
            UpdateObsButtonFromEvent(controller, isStreaming);
            var previous = isSub ? subObsLastStreamingState : mainObsLastStreamingState;
            if (isSub) subObsLastStreamingState = isStreaming;
            else mainObsLastStreamingState = isStreaming;
            if (previous == false && isStreaming)
                _ = HandleObsStreamStartedAsync(isSub);
        }

        private async Task RefreshObsStreamStateAsync(ObsController controller)
        {
            var isStreaming = await Task.Run(controller.IsStreaming);
            var target = GetObsControlTarget();
            if (target is not null && ReferenceEquals(target.Value.Controller, controller))
                SetObsButtonState(enabled: true, isStreaming);
        }

        private (ObsController Controller, bool IsSub)? GetObsControlTarget()
        {
            if (TargetAccountComboBox.SelectedValue is not long accountId)
                return null;
            var mainId = DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainObsTwitchAccountId)?.Value;
            if (mainId == accountId.ToString())
                return (mainObsController, false);
            var subId = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsTwitchAccountId)?.Value;
            if (subId == accountId.ToString())
                return (subObsController, true);
            return null;
        }

        private bool IsSelectedObsController(ObsController controller)
        {
            var target = GetObsControlTarget();
            return target is not null && ReferenceEquals(target.Value.Controller, controller);
        }

        private async Task RefreshObsControlTargetAsync()
        {
            var target = GetObsControlTarget();
            if (target is null || !target.Value.Controller.IsConnected)
            {
                SetObsButtonState(enabled: false, isStreaming: false);
                return;
            }
            try { await RefreshObsStreamStateAsync(target.Value.Controller); }
            catch { SetObsButtonState(enabled: false, isStreaming: false); }
        }

        public async void RefreshObsControlTarget()
        {
            await RefreshObsControlTargetAsync();
        }

        private void SetObsButtonState(bool enabled, bool isStreaming)
        {
            isObsStreaming = isStreaming;
            ObsStreamButton.IsEnabled = enabled;
            ObsStreamButton.Content = isStreaming ? "OBS 配信停止" : "OBS 配信開始";
            ObsStreamButton.ToolTip = isStreaming ? "OBSの配信を停止" : "OBSの配信を開始";
            ObsStreamButton.Background = enabled
                ? (isStreaming ? Brushes.Red : Brushes.Green)
                : new SolidColorBrush(Color.FromRgb(86, 86, 86));
            ObsStreamButton.Foreground = enabled
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(48, 48, 48));
            ObsStreamButton.BorderThickness = enabled ? new Thickness(1) : new Thickness(0);
        }

        /// <summary>OBS側で直接開始・停止された場合も、選択中の操作対象へ表示を追従させる。</summary>
        private void UpdateObsButtonFromEvent(ObsController controller, bool isStreaming)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var target = GetObsControlTarget();
                if (target is not null && ReferenceEquals(target.Value.Controller, controller))
                    SetObsButtonState(enabled: true, isStreaming);
            });
        }

        private async void ObsStreamButton_Click(object sender, RoutedEventArgs e)
        {
            if (isObsOperationRunning)
                return;

            var controller = await EnsureObsConnectedAsync();
            if (controller is null)
                return;

            // 接続直後やOBS側で状態が変わった直後でも、操作前に実状態を同期する。
            try
            {
                await RefreshObsStreamStateAsync(controller);
            }
            catch (Exception ex)
            {
                SetObsButtonState(enabled: false, isStreaming: false);
                MessageBox.Show(
                    $"OBSの配信状態を取得できませんでした。\n{ex.GetBaseException().Message}",
                    "OBS連携");
                return;
            }

            if (isObsStreaming && MessageBox.Show("OBSの配信を停止しますか？", "OBS連携",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            isObsOperationRunning = true;
            SetObsButtonState(enabled: false, isObsStreaming);
            try
            {
                if (isObsStreaming)
                    await Task.Run(controller.StopStreaming);
                else
                    await Task.Run(controller.StartStreaming);
                await RefreshObsStreamStateAsync(controller);
            }
            catch (Exception ex)
            {
                var operation = isObsStreaming ? "停止" : "開始";
                SetObsButtonState(enabled: true, isObsStreaming);
                MessageBox.Show($"OBSの配信を{operation}できませんでした。\n{ex.GetBaseException().Message}", "OBS連携");
            }
            finally
            {
                isObsOperationRunning = false;
            }
        }

        public void ReloadTargetAccounts(long? selectAccountId = null)
        {
            var accounts = DAO_TwitchAccount.SelectAll();
            TargetAccountComboBox.ItemsSource = accounts;
            var savedId = selectAccountId;
            if (savedId is null && long.TryParse(
                DAO_Setting.SelectOneById(DAO_Setting.SettingName.SelectedTwitchAccountId)?.Value,
                out var parsedId))
                savedId = parsedId;
            TargetAccountComboBox.SelectedValue = savedId ?? accounts.FirstOrDefault()?.Id;
            if (TargetAccountComboBox.SelectedIndex < 0 && accounts.Count > 0)
                TargetAccountComboBox.SelectedIndex = 0;
        }

        private async void TargetAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetAccountComboBox.SelectedValue is long id)
                DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SelectedTwitchAccountId, id.ToString());
            await RefreshObsControlTargetAsync();
            RefreshObsSceneShortcutButtons();
            RefreshObsSourceShortcutButtons();
            if (ObsShortcutPanel.Visibility == Visibility.Visible)
                _ = ObsSettingPanel.RefreshSourceVisibilityStatesAsync(SelectedTargetAccountId);

            if (!isAccountAwarePanelsInitialized)
                return;

            SetAccountSwitchLoading(true);
            try
            {
                var target = await GetSelectedTargetAccountAsync();
                if (target is not null &&
                    TargetAccountComboBox.SelectedValue is long selectedId &&
                    selectedId == target.Value.Account.Id)
                {
                    await ChatPanel.InitializeAsync(
                        target.Value.Account.UserName,
                        target.Value.Account.BroadcasterId,
                        target.Value.AccessToken);
                    await RaidPanel.RefreshRaidUsersAsync();
                    await UpdateStreamStatusAsync();
                }
            }
            catch (Exception ex)
            {
                AppLogPanel.Error(
                    GetType().Name,
                    $"アカウント切替に失敗しました。{ex.GetBaseException().Message}");
            }
            finally
            {
                SetAccountSwitchLoading(false);
            }
        }

        private void OpenCurrentCategoryObsCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CurrentCategoryId))
            {
                MessageBox.Show(this, "カテゴリが選択されていません。", "OBSキャプチャ設定",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var category = DAO_Category.SelectOneById(CurrentCategoryId);
            if (category is null)
            {
                MessageBox.Show(this, "現在のカテゴリ情報が見つかりません。", "OBSキャプチャ設定",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new ObsCaptureDestinationWindow(
                category.CategoryId,
                category.DisplayName,
                category.BoxArtUrl)
            {
                Owner = this
            };
            window.ShowDialog();
            CategoryPanel.ReloadCategory();
        }

        private async void LaunchSteamGameButton_Click(object sender, RoutedEventArgs e)
        {
            var appId = SteamHelper.GetSteamAppId(CurrentCategorySteamUrl);
            if (appId is null)
            {
                LaunchSteamGameButton.IsEnabled = false;
                return;
            }

            try
            {
                isSteamGameLaunching = true;
                LaunchSteamGameButton.IsEnabled = false;
                Process.Start(new ProcessStartInfo($"steam://run/{appId}")
                {
                    UseShellExecute = true
                });

                var detected = false;
                for (var elapsedSeconds = 0; elapsedSeconds < 10; elapsedSeconds++)
                {
                    if (IsSteamGameRunning(appId))
                    {
                        detected = true;
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                if (detected)
                    AppLogPanel.Success(GetType().Name, $"Steamゲーム起動完了 App ID: {appId}");
                else
                    AppLogPanel.Error(GetType().Name,
                        $"Steamゲームの起動を10秒以内に確認できませんでした。App ID: {appId}");
            }
            catch (Exception ex)
            {
                AppLogPanel.Error(GetType().Name,
                    $"Steamゲームを起動できませんでした。{ex.GetBaseException().Message}");
                MessageBox.Show(this,
                    "Steamゲームを起動できませんでした。Steamがインストールされているか確認してください。",
                    "ゲーム起動", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                isSteamGameLaunching = false;
                LaunchSteamGameButton.IsEnabled =
                    SteamHelper.GetSteamAppId(CurrentCategorySteamUrl) is not null;
            }
        }

        private static bool IsSteamGameRunning(string appId)
        {
            using var appKey = Registry.CurrentUser.OpenSubKey($@"Software\Valve\Steam\Apps\{appId}");
            var runningValue = appKey?.GetValue("Running");
            return runningValue is not null && Convert.ToInt32(runningValue, CultureInfo.InvariantCulture) == 1;
        }

        private void SetAccountSwitchLoading(bool isLoading, bool isStartup = false)
        {
            if (isLoading)
            {
                BlockingLoadingTitleTextBlock.Text = isStartup
                    ? "アプリを読み込んでいます…"
                    : "アカウントを切り替えています…";
                BlockingLoadingDetailTextBlock.Text = isStartup
                    ? "認証情報と各機能を準備中"
                    : "チャット接続と関連データを読み込み中";
            }

            accountSwitchLoadingCount = isLoading
                ? accountSwitchLoadingCount + 1
                : Math.Max(0, accountSwitchLoadingCount - 1);
            AccountSwitchLoadingOverlay.Visibility = accountSwitchLoadingCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public async Task<(M_TwitchAccount Account, string AccessToken)?> GetSelectedTargetAccountAsync()
        {
            if (TargetAccountComboBox.SelectedValue is not long selectedAccountId)
                return null;

            await twitchAccountTokenLock.WaitAsync();
            try
            {
                // 待機中に別処理が更新トークンをローテーションしている場合があるため、
                // ロック取得後にDBから最新値を読み直す。
                var account = DAO_TwitchAccount.SelectById(selectedAccountId);
                if (account is null || string.IsNullOrWhiteSpace(account.RefreshToken))
                    return null;

                var token = await TwitchHelper.RefreshAccessTokenAsync(account.RefreshToken);
                if (token is null ||
                    string.IsNullOrWhiteSpace(token.accessToken) ||
                    string.IsNullOrWhiteSpace(token.refreshToken))
                {
                    AppLogPanel.Error(
                        GetType().Name,
                        $"{account.UserName} のアクセストークン更新に失敗しました。再認証してください。");
                    return null;
                }

                DAO_TwitchAccount.UpdateRefreshToken(account.Id, token.refreshToken);
                if (account.IsPrimary)
                {
                    DAO_Setting.InsertUpdate(DAO_Setting.SettingName.RefreshToken, token.refreshToken);
                    TwitchHelper.AccessToken = token.accessToken;
                }

                account.RefreshToken = token.refreshToken;
                return (account, token.accessToken);
            }
            finally
            {
                twitchAccountTokenLock.Release();
            }
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
                TitlePlaceholderTextBox.Text = string.IsNullOrEmpty(selectedItem.TitlePlaceholder)
                    ? TitlePlaceholderReplacer.TitlePlaceholder
                    : selectedItem.TitlePlaceholder;

                CurrentCategoryId = selectedItem.CategoryId;
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

        /// <summary>配信概要パネルで現在編集中のプレースホルダー。</summary>
        public string OverviewTitlePlaceholder => TitlePlaceholderTextBox.Text;

        /// <summary>カレンダー予定を配信概要の送信予定へ反映する。</summary>
        public void ApplyCalendarEntryToOverview(T_CalendarEntry entry)
        {
            FriendPanel.SelectFriends(entry.SelectedFriendIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            TitleEditTextBox.Text = entry.Content;
            TitlePlaceholderTextBox.Text = string.IsNullOrWhiteSpace(entry.TitlePlaceholder)
                ? TitlePlaceholderReplacer.TitlePlaceholder
                : entry.TitlePlaceholder;
            CurrentCategoryId = entry.CategoryId;
            CurrentCategoryName = entry.CategoryName;
            CurrentCategoryBoxArtUrl = entry.CategoryBoxArtUrl;
            CurrentTitleTextUpdate();
        }

        private void OverviewSelectFriendsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FriendSelectionWindow(
                FriendPanel.SelectedFriendFormList.Select(friend => friend.BroadcastId))
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                FriendPanel.SelectFriends(dialog.SelectedBroadcastIds);
            }
        }

        private void OverviewRemoveFriendButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is FriendForm friend)
            {
                FriendPanel.SelectedFriendFormList.Remove(friend);
                FriendPanel.UpdateTitlePreview();
            }
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
            var combinedTitleText = TitlePlaceholderReplacer.ReplaceTitle(
                TitleEditTextBox.Text,
                TitlePlaceholderTextBox.Text,
                CurrentCategoryJapaneseName);
            var streamTitleText = TitleTextFriendTagToXReplace(combinedTitleText);
            var categoryNameText = CurrentCategoryName;
            var japaneseCategoryNameText = CurrentCategoryJapaneseName;

            // 認証URL生成
            var oauthUrl = $"https://x.com/intent/post?text=";
            var selectedAccount = SelectedTargetAccountId is long accountId
                ? DAO_TwitchAccount.SelectById(accountId)
                : null;
            var streamLoginName = selectedAccount?.UserName ?? JTSAHelper.LoginName;
            var streamUrlText = $"https://www.twitch.tv/{streamLoginName}";

            streamTitleText = streamTitleText.Replace("#", "＃");

            var template = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.XPostTemplate)?.Value
                ?? DAO_Setting.DefaultXPostTemplate;

            var postText = template
                .Replace("{title}", streamTitleText)
                .Replace("{category}", categoryNameText)
                .Replace("{category_ja}", japaneseCategoryNameText)
                .Replace("{url}", streamUrlText);

            // URIエンコード
            var encodedText = WebUtility.UrlEncode(postText);

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

            CurrentTitleTextUpdate();

            //【プロセス終了ログ】
            processLog.EventEndLogWrite();
        }

        /// <summary>
        /// タイトル編集パネル:プレースホルダーテキストボックス（テキスト変更）
        /// </summary>
        private void TitlePlaceholderTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;

            if (string.IsNullOrEmpty(TitlePlaceholderTextBox.Text))
            {
                TitlePlaceholderTextBox.Text = TitlePlaceholderReplacer.TitlePlaceholder;
                TitlePlaceholderTextBox.SelectionStart = TitlePlaceholderTextBox.Text.Length;
                return;
            }

            CurrentTitleTextUpdate();
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
        public async Task StreamerDataSet(string broadcasterId, string accessToken)
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "配信者情報設定処理");

            // タイトル取得処理
            var streamInfo = await TwitchHelper.GetTwitchStreamInfo(broadcasterId, accessToken);
            if (streamInfo == null)
            {
                processLog.ErrorLogWrite("配信情報の取得に失敗");
                return;
            }

            CurrentTitleText = streamInfo.title;

            var dbCategoryData = DAO_Category.SelectOneById(streamInfo.gameId);
            if (dbCategoryData == null)
            {
                // DBに未登録のカテゴリなので、Twitch/IGDBから取得して組み立てる
                var category = await TwitchHelper.GetCategoryByGameId(streamInfo.gameId);
                if (category == null)
                {
                    // カテゴリ未設定で配信している場合などはここに来る。タイトルだけ反映して終了する
                    processLog.ErrorLogWrite("カテゴリ情報の取得に失敗");

                    ReloadTitleText();
                    LoadFirstTitleLogIntoEditor(streamInfo.title);
                    TitleTagSidePanel.ReloadTitleTag();

                    return;
                }

                var steamUrl = await IgdbService.GetSteamUrlsAsync(category.Id);

                dbCategoryData = new M_Category
                {
                    CategoryId = category.Id,
                    DisplayName = category.Name,
                    JapaneseDisplayName = await IgdbService.GetJapaneseGameNameAsync(category.Id)
                        ?? category.Name,
                    SteamUrl = steamUrl?.FirstOrDefault() ?? string.Empty,
                    BoxArtUrl = category.BoxArtUrl,
                    LastUsedDateTime = DateTime.Now,
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now
                };
                DAO_Category.Insert(dbCategoryData);
            }

            CurrentCategoryId = dbCategoryData.CategoryId;
            CurrentCategoryName = dbCategoryData.DisplayName;
            CurrentCategoryBoxArtUrl = dbCategoryData.BoxArtUrl;
            CurrentCategorySteamUrl = dbCategoryData.SteamUrl;


            // リスト読み込み処理
            ReloadTitleText();
            LoadFirstTitleLogIntoEditor(streamInfo.title);
            TitleTagSidePanel.ReloadTitleTag();

            var titleMatches = string.Equals(
                CurrentTitleText.Trim(),
                streamInfo.title.Trim(),
                StringComparison.Ordinal);
            var categoryMatches = string.Equals(
                CurrentCategoryId.Trim(),
                streamInfo.gameId.Trim(),
                StringComparison.Ordinal);
            SetTwitchSettingApplied(titleMatches && categoryMatches);

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
                    TitlePlaceholder = item.TitlePlaceholder,
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName,
                    CategoryBoxArtUrl = item.CategoryBoxArtUrl,
                    LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }

            processLog.SuccessLogWrite();
        }

        /// <summary>
        /// タイトルログの先頭を編集欄へ読み込む。
        /// ログが無い場合はTwitchから取得したタイトルを本文として使用する。
        /// </summary>
        private void LoadFirstTitleLogIntoEditor(string twitchTitle)
        {
            var firstTitleLog = TitleTextFormList.FirstOrDefault();

            TitleEditTextBox.Text = firstTitleLog?.Content ?? twitchTitle;
            TitlePlaceholderTextBox.Text = string.IsNullOrEmpty(firstTitleLog?.TitlePlaceholder)
                ? TitlePlaceholderReplacer.TitlePlaceholder
                : firstTitleLog.TitlePlaceholder;
        }

        /// <summary>
        /// 配信概要パネルのカテゴリ一覧から現在のカテゴリを選択する。
        /// </summary>
        private void OverviewCategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
        /// タイトルプレースホルダー編集カーソル位置挿入処理
        /// </summary>
        /// <param name="insertText"></param>
        public void InsertTextAtCaret(string insertText)
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトルテキスト編集カーソル位置挿入処理");

            if (TitlePlaceholderTextBox == null)
            {
                processLog.ErrorLogWrite("タイトルプレースホルダーテキストボックスが存在しない");
                return;
            }

            int currentIndex = TitlePlaceholderTextBox.SelectionStart;
            string original = TitlePlaceholderTextBox.Text ?? "";

            // 挿入処理
            TitlePlaceholderTextBox.Text =
                original.Substring(0, currentIndex) +
                insertText +
                original.Substring(currentIndex);

            // 挿入後のカーソル位置を調整
            TitlePlaceholderTextBox.SelectionStart = currentIndex + insertText.Length;
            TitlePlaceholderTextBox.Focus();

            processLog.SuccessLogWrite();
        }


        /// <summary>
        /// タイトルテキストプレビューの更新処理
        /// </summary>
        public void CurrentTitleTextUpdate()
        {
            ProcessLog processLog = new ProcessLog(AppLogPanel, GetType().Name, "タイトルテキストプレビューの更新処理");

            if (TitleEditTextBox == null || TitlePlaceholderTextBox == null) return;

            var combinedTitleText = TitlePlaceholderReplacer.ReplaceTitle(
                TitleEditTextBox.Text,
                TitlePlaceholderTextBox.Text,
                CurrentCategoryJapaneseName);
            CurrentTitleTextBlock.Text = TitleTextFriendTagReplace(combinedTitleText);
            TitleWordNum.Content = CurrentTitleTextBlock.Text.Count() + "/140";
            SetTwitchSettingApplied(false);

            processLog.SuccessLogWrite();
        }

        #endregion


        #region ===============privateメソッド===============

        private void SetTwitchSettingApplied(bool isApplied)
        {
            if (TwitchSettingStatusTextBlock is null) return;

            TwitchSettingStatusTextBlock.Text = isApplied ? "設定反映済" : "設定未反映";
            TwitchSettingStatusTextBlock.Foreground = isApplied
                ? new SolidColorBrush(Color.FromRgb(92, 214, 122))
                : new SolidColorBrush(Color.FromRgb(255, 107, 107));
        }

        private void OpenCategorySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new CategoryWindow
            {
                Owner = this
            };

            window.ShowDialog();
            CategoryPanel.ReloadCategory();
        }

        private void OverviewAddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new CategorySearchWindow
            {
                Owner = this
            };

            window.ShowDialog();
            CategoryPanel.ReloadCategory();
        }

        private void OpenHelpWindowButton_Click(object sender, RoutedEventArgs e) =>
            OpenToolPanelWindow(HelpPanelHost, HelpPanel, "ヘルプ");

        private void OpenSettingsWindowButton_Click(object sender, RoutedEventArgs e) =>
            OpenToolPanelWindow(SettingsPanelHost, SettingPanel, "設定");

        private void OpenPatchNotesWindowButton_Click(object sender, RoutedEventArgs e) =>
            OpenToolPanelWindow(PatchNotesPanelHost, PatchNotePanel, "パッチノート");

        private void OpenAppLogWindowButton_Click(object sender, RoutedEventArgs e) =>
            OpenToolPanelWindow(AppLogPanelHost, AppLogPanel, "AppLog");

        private void OpenToolPanelWindow(ContentControl panelHost, FrameworkElement panel, string title)
        {
            if (toolPanelWindows.TryGetValue(panel, out var existingWindow))
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;
                existingWindow.Activate();
                return;
            }

            panelHost.Content = null;
            var window = new ToolPanelWindow(title, panel)
            {
                Owner = this
            };
            toolPanelWindows.Add(panel, window);
            window.Closed += (_, _) =>
            {
                window.ReleaseContent();
                panelHost.Content = panel;
                toolPanelWindows.Remove(panel);
            };
            window.Show();
        }

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
		private void AddTitleText(
            string content,
            string titlePlaceholder,
            string categoryId,
            string categoryName,
            string categoryBoxArtUrl)
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
				TitlePlaceholder = titlePlaceholder,
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
			// XAML初期化中はTitlePlaceholderTextBoxのTextChangedが
			// FriendPanel生成前に発火するため、システムタグだけを置換する。
			if (FriendPanel == null)
			{
				return TitleTextTagReplace(titleText);
			}

			var friendText = FriendPanel.FriendPrefixWordTextBox.Text;
			foreach(var friendItem in FriendPanel.SelectedFriendFormList)
			{
			 	friendText += " @" + friendItem.UserId;
			}

            titleText = titleText.Replace("${friend}", friendText + " ");

			return TitleTextTagReplace(titleText);
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
            return TitleTextTagReplace(titleText);
        }


        /// <summary>
        /// ${ID} 形式のタイトルタグを登録済みの表示文字列へ置換する。
        /// </summary>
        private static string TitleTextTagReplace(string titleText)
        {
            titleText = TitlePlaceholderReplacer.ReplaceDate(titleText, DateTime.Now);

            foreach (var titleTag in DAO_TitleTag.SelectAllOrderbyLastUser())
            {
                titleText = titleText.Replace($"${{{titleTag.Id}}}", titleTag.DisplayName);
            }

            return titleText;
        }

        #endregion


        /// <summary>ヘッダーの配信状態、経過時間、視聴者数を更新する。</summary>
        private async Task UpdateStreamStatusAsync()
        {
            if (isStreamStatusUpdating || string.IsNullOrWhiteSpace(TwitchHelper.AccessToken) ||
                TargetAccountComboBox.SelectedValue is not long selectedAccountId)
            {
                return;
            }

            var selectedAccount = DAO_TwitchAccount.SelectById(selectedAccountId);
            if (selectedAccount is null || string.IsNullOrWhiteSpace(selectedAccount.BroadcasterId))
                return;

            isStreamStatusUpdating = true;
            streamStatusTimer.Stop();
            try
            {
                var stream = await TwitchHelper.GetCurrentStreamAsync(
                    selectedAccount.BroadcasterId,
                    TwitchHelper.AccessToken);

                // Ignore a response for an account that was deselected while the API call ran.
                if (TargetAccountComboBox.SelectedValue is not long currentAccountId ||
                    currentAccountId != selectedAccountId)
                    return;

                if (stream == null)
                {
                    DAO_StreamHistory.EndActiveStreams(selectedAccount.BroadcasterId, DateTime.Now);
                    TwitchHelper.CurrentStreamId = string.Empty;
                    StreamSupportTracker.StartStream(string.Empty);
                    currentStreamStartedAtUtc = null;
                    currentViewerCount = null;
                    StreamStatusIndicator.Fill = Brushes.Gray;
                    StreamStatusTextBlock.Text = "オフライン";
                    StreamDurationTextBlock.Text = "--:--:--";
                    ViewerCountTextBlock.Text = "-- 人";
                    return;
                }

                currentStreamStartedAtUtc = stream.StartedAt.ToUniversalTime();
                TwitchHelper.CurrentStreamId = stream.StreamId;
                StreamSupportTracker.StartStream(stream.StreamId);

                var now = DateTime.Now;
                DAO_StreamHistory.Upsert(new T_StreamHistory
                {
                    StreamId = stream.StreamId,
                    BroadcasterId = stream.UserId,
                    Title = stream.Title,
                    CategoryName = stream.GameName,
                    StartedAt = stream.StartedAt.ToLocalTime(),
                    CreatedDateTime = now,
                    UpdatedDateTime = now
                });

                StreamStatusIndicator.Fill = Brushes.LimeGreen;
                StreamStatusTextBlock.Text = "配信中";
                UpdateDisplayedStreamDuration();
                currentViewerCount = stream.ViewerCount;
                UpdateDisplayedViewerCount();
            }
            finally
            {
                isStreamStatusUpdating = false;
                nextStreamStatusUpdateAtUtc = DateTime.UtcNow + streamStatusTimer.Interval;
                UpdateStreamStatusCountdown();
                streamStatusTimer.Start();
            }
        }

        /// <summary>視聴者数を次に取得するまでの残り時間を表示する。</summary>
        private void UpdateStreamStatusCountdown()
        {
            if (nextStreamStatusUpdateAtUtc is not DateTime nextUpdateAtUtc)
            {
                ViewerCountUpdateCountdownTextBlock.Text = "次回更新 --:--";
                return;
            }

            var remainingSeconds = Math.Max(
                0,
                (int)Math.Ceiling((nextUpdateAtUtc - DateTime.UtcNow).TotalSeconds));
            var remaining = TimeSpan.FromSeconds(remainingSeconds);
            ViewerCountUpdateCountdownTextBlock.Text =
                $"次回更新 {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        }

        /// <summary>最後に取得した配信開始時刻から、現在の配信時間をローカル計算して表示する。</summary>
        private void UpdateDisplayedStreamDuration()
        {
            if (currentStreamStartedAtUtc is not DateTime startedAtUtc)
                return;

            var duration = DateTime.UtcNow - startedAtUtc;
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            StreamDurationTextBlock.Text = duration.TotalDays >= 1
                ? $"{(int)duration.TotalDays}日 {duration:hh\\:mm\\:ss}"
                : duration.ToString(@"hh\:mm\:ss");
        }

        private async void ViewerCountTextBlock_MouseLeftButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            var clickedAtUtc = DateTime.UtcNow;
            viewerCountConsecutiveClicks = clickedAtUtc - lastViewerCountClickUtc <= SecretPanelClickInterval
                ? viewerCountConsecutiveClicks + 1
                : 1;
            lastViewerCountClickUtc = clickedAtUtc;

            isViewerCountHidden = !isViewerCountHidden;
            UpdateDisplayedViewerCount();

            if (viewerCountConsecutiveClicks < SecretPanelClickCount)
                return;

            viewerCountConsecutiveClicks = 0;
            await ChatStatisticsPanel.SyncArchivedStreamsAsync();
            ChatStatisticsPanel.ReloadStatisticsForSelectedPeriod();
            ChatStatisticsTabItem.Visibility = Visibility.Visible;
            MainTabControl.SelectedItem = ChatStatisticsTabItem;
            e.Handled = true;
        }

        private void ChatStatisticsPanel_CloseRequested(object sender, RoutedEventArgs e)
        {
            ChatStatisticsTabItem.Visibility = Visibility.Collapsed;
            MainTabControl.SelectedIndex = 0;
        }

        private void TwitchSettingStatusTextBlock_MouseLeftButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            isTwitchStatusHeld = true;
            TwitchSettingStatusTextBlock.CaptureMouse();
            twitchStatusHoldTimer.Stop();
            twitchStatusHoldTimer.Start();
            e.Handled = true;
        }

        private void TwitchSettingStatusTextBlock_MouseLeftButtonUp(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            CancelTwitchStatusHold();
            e.Handled = true;
        }

        private void TwitchStatusHoldTimer_Tick(object? sender, EventArgs e)
        {
            twitchStatusHoldTimer.Stop();
            if (!isTwitchStatusHeld || System.Windows.Input.Mouse.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                CancelTwitchStatusHold();
                return;
            }

            isTwitchStatusHeld = false;
            TwitchSettingStatusTextBlock.ReleaseMouseCapture();
            CalendarPanel.RefreshSelectedDate();
            MainTabControl.SelectedItem = CalendarTabItem;
        }

        private void CancelTwitchStatusHold()
        {
            isTwitchStatusHeld = false;
            twitchStatusHoldTimer.Stop();
            if (TwitchSettingStatusTextBlock.IsMouseCaptured)
                TwitchSettingStatusTextBlock.ReleaseMouseCapture();
        }

        private void CalendarPanel_AddRequested()
            => OpenCalendarRegistrationWindow();

        private void CalendarPanel_EditRequested(long entryId)
            => OpenCalendarRegistrationWindow(entryId);

        private void OpenCalendarRegistrationWindow(long? entryId = null)
        {
            var window = new CalendarRegistrationWindow(
                CalendarPanel.SelectedDate,
                OverviewTitlePlaceholder,
                entryId)
            {
                Owner = this
            };
            window.ShowDialog();
            CalendarPanel.RefreshSelectedDate();
        }

        private void UpdateDisplayedViewerCount()
        {
            ViewerCountTextBlock.Text = isViewerCountHidden || currentViewerCount is null
                ? "-- 人"
                : $"{currentViewerCount.Value:N0} 人";
        }


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

                SettingPanel.SetBroadcasterStatus(false);
                LoadSubPanel.Visibility = Visibility.Visible;
                return;
            }

            // メモリに登録
            TwitchHelper.BroadcasterId = streamerInfo.UserId;
            SettingPanel.SetBroadcasterStatus(true, streamerInfo.UserId);

            JTSAHelper.LoginName = streamerInfo.Login;
            SettingPanel.SetTwitchUserName(JTSAHelper.LoginName);

            // 表示・Twitchダッシュボードのリンク用に保存しておく
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.UserName, JTSAHelper.LoginName);

            var primaryRefreshToken = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RefreshToken)?.Value;
            if (!string.IsNullOrWhiteSpace(primaryRefreshToken))
            {
                var primaryAccount = DAO_TwitchAccount.InsertUpdate(
                    streamerInfo.Login, streamerInfo.UserId, primaryRefreshToken, isPrimary: true);
                ReloadTargetAccounts();
                SettingPanel.ReloadRegisteredAccounts();
            }

            IgdbService.Initialize(new HttpClient(), TwitchHelper.ClientID, TwitchHelper.AccessToken);

            // 右上で選択されているアカウントの配信概要を読み込む。
            var selectedAccount = await GetSelectedTargetAccountAsync();
            if (selectedAccount is null)
            {
                processLog.ErrorLogWrite("選択アカウントの認証情報を取得できませんでした");
                return;
            }

            await StreamerDataSet(
                selectedAccount.Value.Account.BroadcasterId,
                selectedAccount.Value.AccessToken);
            await UpdateStreamStatusAsync();

            // 各パネルの初期化処理
            await ChatPanel.InitializeAsync(
                selectedAccount.Value.Account.UserName,
                selectedAccount.Value.Account.BroadcasterId,
                selectedAccount.Value.AccessToken);
            isAccountAwarePanelsInitialized = true;
            CategoryPanel.Initialize();
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

            await twitchAccountTokenLock.WaitAsync();
            try
            {
                var primaryAccount = DAO_TwitchAccount.SelectPrimary();
                var settingRefreshToken = DAO_Setting
                    .SelectOneById(DAO_Setting.SettingName.RefreshToken)?.Value;
                var refreshTokenCandidates = new[]
                    {
                        settingRefreshToken,
                        primaryAccount?.RefreshToken
                    }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                if (refreshTokenCandidates.Count == 0)
                {
                    processLog.ErrorLogWrite("リフレッシュトークン未設定");
                    return string.Empty;
                }

                AccessTokenResponseIF? accessTokenResponse = null;
                foreach (var refreshToken in refreshTokenCandidates)
                {
                    var refreshed = await TwitchHelper.RefreshAccessTokenAsync(refreshToken!);
                    if (!string.IsNullOrWhiteSpace(refreshed?.accessToken) &&
                        !string.IsNullOrWhiteSpace(refreshed.refreshToken))
                    {
                        accessTokenResponse = refreshed;
                        break;
                    }
                }

                if (accessTokenResponse is null)
                {
                    processLog.ErrorLogWrite("アクセストークン未取得");
                    return string.Empty;
                }

                DAO_Setting.InsertUpdate(
                    DAO_Setting.SettingName.RefreshToken,
                    accessTokenResponse.refreshToken);
                DAO_Setting.InsertUpdate(
                    DAO_Setting.SettingName.ExpiresIn,
                    accessTokenResponse.expiresIn.ToString());
                if (primaryAccount is not null)
                    DAO_TwitchAccount.UpdateRefreshToken(primaryAccount.Id, accessTokenResponse.refreshToken);

                processLog.SuccessLogWrite();
                return accessTokenResponse.accessToken;
            }
            finally
            {
                twitchAccountTokenLock.Release();
            }
        }

        #endregion
    }
}
