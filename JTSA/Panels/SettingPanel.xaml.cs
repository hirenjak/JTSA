using JTSA.Dao;
using JTSA.Models;
using JTSA.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JTSA.Panels
{
    /// <summary>
    /// SettingPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingPanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        public SettingPanel()
        {
            InitializeComponent();

            SettingOAuthCodeCopyButton.Click += SettingOAuthCodeCopyButton_Click;

            XPostTemplateTextBox.Text = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.XPostTemplate)?.Value
                ?? DAO_Setting.DefaultXPostTemplate;

            var speechEngine = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SpeechEngine)?.Value;
            if (speechEngine is null && DAO_Setting.SelectOneById(DAO_Setting.SettingName.BouyomiEnabled)?.Value == "1")
                speechEngine = "Bouyomi";
            BouyomiEnabledRadioButton.IsChecked = speechEngine == "Bouyomi";
            VoiceVoxEnabledRadioButton.IsChecked = speechEngine == "VoiceVox";
            SpeechDisabledRadioButton.IsChecked = speechEngine is not ("Bouyomi" or "VoiceVox");
            BouyomiEndpointTextBox.Text = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.BouyomiEndpoint)?.Value
                ?? BouyomiChanClient.DefaultEndpoint;
            VoiceVoxEndpointTextBox.Text = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.VoiceVoxEndpoint)?.Value
                ?? VoiceVoxClient.DefaultEndpoint;
            ReloadRegisteredAccounts();
            Loaded += SettingPanel_Loaded;
        }

        public void SetAccessTokenStatus(bool isAuthenticated) { }

        public void SetBroadcasterStatus(bool isAvailable, string broadcasterId = "") { }

        public void SetTwitchUserName(string userName) { }

        public void ReloadRegisteredAccounts()
        {
            var accounts = DAO_TwitchAccount.SelectAll();
            RegisteredAccountItemsControl.ItemsSource = accounts;
            NoRegisteredAccountTextBlock.Visibility = accounts.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void SettingPanel_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingPanel_Loaded;
            await LoadVoiceVoxSpeakersAsync(showError: false);
        }

        private async void RefreshVoiceVoxSpeakersButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadVoiceVoxSpeakersAsync(showError: true);
        }

        private async Task LoadVoiceVoxSpeakersAsync(bool showError)
        {
            var savedId = ReadVoiceVoxSpeakerId();
            try
            {
                var styles = await new VoiceVoxClient().GetSpeakerStylesAsync(VoiceVoxEndpointTextBox.Text);
                SetVoiceVoxSpeakerItems(styles, savedId);
            }
            catch (Exception ex)
            {
                VoiceVoxSpeakerComboBox.ItemsSource = new[]
                {
                    new VoiceVoxSpeakerStyle(savedId, $"保存済みの話者（ID: {savedId}）")
                };
                VoiceVoxSpeakerComboBox.SelectedIndex = 0;
                if (showError)
                    MessageBox.Show($"話者一覧を取得できませんでした。\n{ex.Message}", "VOICEVOX連携");
            }
        }

        private void SetVoiceVoxSpeakerItems(
            IReadOnlyList<VoiceVoxSpeakerStyle> styles,
            int selectedId)
        {
            VoiceVoxSpeakerComboBox.ItemsSource = styles;
            VoiceVoxSpeakerComboBox.SelectedValue = selectedId;
            if (VoiceVoxSpeakerComboBox.SelectedIndex < 0 && VoiceVoxSpeakerComboBox.Items.Count > 0)
                VoiceVoxSpeakerComboBox.SelectedIndex = 0;
        }

        private static int ReadVoiceVoxSpeakerId()
        {
            return int.TryParse(DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.VoiceVoxSpeakerId)?.Value, out var id) && id >= 0
                ? id
                : VoiceVoxClient.DefaultSpeakerId;
        }

        private void SaveSpeechButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = BouyomiChanClient.CreateTalkUri(BouyomiEndpointTextBox.Text, "test");
                _ = VoiceVoxClient.ValidateEndpoint(VoiceVoxEndpointTextBox.Text);
                if (VoiceVoxSpeakerComboBox.SelectedValue is not int)
                    throw new ArgumentException("VOICEVOXの話者を選択してください。");
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "チャット読み上げ連携");
                return;
            }

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.BouyomiEnabled,
                BouyomiEnabledRadioButton.IsChecked == true ? "1" : "0");
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.BouyomiEndpoint,
                BouyomiEndpointTextBox.Text.Trim());
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.SpeechEngine,
                VoiceVoxEnabledRadioButton.IsChecked == true ? "VoiceVox" :
                BouyomiEnabledRadioButton.IsChecked == true ? "Bouyomi" : "None");
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.VoiceVoxEndpoint,
                VoiceVoxEndpointTextBox.Text.Trim());
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.VoiceVoxSpeakerId,
                ((int)VoiceVoxSpeakerComboBox.SelectedValue).ToString());

            mainWindow.ChatPanel.ReloadSpeechSettings();
            MessageBox.Show("読み上げ設定を保存しました。", "チャット読み上げ連携");
        }

        private async void TestSpeechButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                const string testText = "JTSAの読み上げテストです。";
                if (VoiceVoxEnabledRadioButton.IsChecked == true)
                {
                    if (VoiceVoxSpeakerComboBox.SelectedValue is not int speakerId)
                        throw new ArgumentException("VOICEVOXの話者を選択してください。");
                    await new VoiceVoxClient().SpeakAsync(VoiceVoxEndpointTextBox.Text, speakerId, testText);
                }
                else if (BouyomiEnabledRadioButton.IsChecked == true)
                {
                    await new BouyomiChanClient().SpeakAsync(BouyomiEndpointTextBox.Text, testText);
                }
                else
                {
                    MessageBox.Show("読み上げエンジンを選択してください。", "チャット読み上げ連携");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"読み上げに失敗しました。\n{ex.Message}",
                    "チャット読み上げ連携");
            }
        }

        private void SaveXPostTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(XPostTemplateTextBox.Text))
            {
                MessageBox.Show("X投稿文を入力してください。", "X投稿文");
                return;
            }

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.XPostTemplate,
                XPostTemplateTextBox.Text);

            MessageBox.Show("X投稿文を保存しました。", "X投稿文");
        }

        private void ResetXPostTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            XPostTemplateTextBox.Text = DAO_Setting.DefaultXPostTemplate;
        }

        private void SettingOAuthCodeCopyButton_Click(object sender, RoutedEventArgs e)
        {
            JTSAHelper.CopyClipBoad(SettingOAuthCodeBox.Text);
        }

        private async void AddSubAccountButton_Click(object sender, RoutedEventArgs e)
        {
            AddSubAccountButton.IsEnabled = false;
            try
            {
                var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();
                if (deviceCodeResponse is null)
                    throw new InvalidOperationException("認証コードを取得できませんでした。");

                SettingOAuthCodeBox.Text = deviceCodeResponse.user_code;
                var verificationUrl = string.IsNullOrWhiteSpace(deviceCodeResponse.verification_uri_complete)
                    ? deviceCodeResponse.verification_uri
                    : deviceCodeResponse.verification_uri_complete;
                Process.Start(new ProcessStartInfo(verificationUrl) { UseShellExecute = true });

                var token = await TwitchHelper.PollDeviceTokenAsync(
                    deviceCodeResponse.device_code,
                    deviceCodeResponse.interval,
                    deviceCodeResponse.expires_in);
                if (token is null)
                    throw new InvalidOperationException("サブアカウントの認証が完了しませんでした。");

                var user = await TwitchHelper.GetAuthenticatedUserAsync(token.accessToken);
                if (user is null)
                    throw new InvalidOperationException("認証したアカウント情報を取得できませんでした。");

                var account = DAO_TwitchAccount.InsertUpdate(
                    user.Login, user.UserId, token.refreshToken);
                mainWindow.ReloadTargetAccounts(account.Id);
                ReloadRegisteredAccounts();
                MessageBox.Show($"{user.DisplayName} を送信先に追加しました。", "Twitchアカウント");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Twitchアカウント");
            }
            finally
            {
                AddSubAccountButton.IsEnabled = true;
            }
        }

        private async void ReauthenticateAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not long accountId)
                return;

            var account = DAO_TwitchAccount.SelectById(accountId);
            if (account is null)
            {
                MessageBox.Show("対象のアカウントが見つかりませんでした。", "Twitchアカウント");
                return;
            }

            button.IsEnabled = false;
            try
            {
                var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();
                if (deviceCodeResponse is null)
                    throw new InvalidOperationException("認証コードを取得できませんでした。");

                SettingOAuthCodeBox.Text = deviceCodeResponse.user_code;
                var verificationUrl = string.IsNullOrWhiteSpace(deviceCodeResponse.verification_uri_complete)
                    ? deviceCodeResponse.verification_uri
                    : deviceCodeResponse.verification_uri_complete;
                Process.Start(new ProcessStartInfo(verificationUrl) { UseShellExecute = true });

                var token = await TwitchHelper.PollDeviceTokenAsync(
                    deviceCodeResponse.device_code,
                    deviceCodeResponse.interval,
                    deviceCodeResponse.expires_in);
                if (token is null)
                    throw new InvalidOperationException("再認証が完了しませんでした。");

                var authenticatedUser = await TwitchHelper.GetAuthenticatedUserAsync(token.accessToken);
                if (authenticatedUser is null)
                    throw new InvalidOperationException("認証したアカウント情報を取得できませんでした。");
                if (authenticatedUser.UserId != account.BroadcasterId)
                {
                    throw new InvalidOperationException(
                        $"{account.UserName} ではなく {authenticatedUser.DisplayName} で認証されています。\n" +
                        "対象のTwitchアカウントへ切り替えて、もう一度再認証してください。");
                }

                DAO_TwitchAccount.InsertUpdate(
                    authenticatedUser.Login,
                    authenticatedUser.UserId,
                    token.refreshToken,
                    account.IsPrimary);

                if (account.IsPrimary)
                {
                    DAO_Setting.InsertUpdate(DAO_Setting.SettingName.UserName, authenticatedUser.Login);
                    DAO_Setting.InsertUpdate(DAO_Setting.SettingName.RefreshToken, token.refreshToken);
                    DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ExpiresIn, token.expiresIn.ToString());
                    TwitchHelper.AccessToken = token.accessToken;
                    SetAccessTokenStatus(true);
                    SetBroadcasterStatus(true, authenticatedUser.UserId);
                    SetTwitchUserName(authenticatedUser.Login);
                }

                mainWindow.ReloadTargetAccounts();
                ReloadRegisteredAccounts();
                MessageBox.Show($"{authenticatedUser.DisplayName} を再認証しました。", "Twitchアカウント");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Twitchアカウント");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }


        /// <summary>
        /// ヘッダ部:DBフォルダオープンボタン（クリック）
        /// </summary>
        private void DBFolderOpen(object sender, RoutedEventArgs e)
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
