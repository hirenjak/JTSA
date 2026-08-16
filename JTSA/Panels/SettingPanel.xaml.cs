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

            ReOAuthButton.Click += ReOAuthButton_Click;
            SettingOAuthCodeCopyButton.Click += SettingOAuthCodeCopyButton_Click;

            XPostTemplateTextBox.Text = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.XPostTemplate)?.Value
                ?? DAO_Setting.DefaultXPostTemplate;

            BouyomiEnabledCheckBox.IsChecked = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.BouyomiEnabled)?.Value == "1";
            BouyomiEndpointTextBox.Text = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.BouyomiEndpoint)?.Value
                ?? BouyomiChanClient.DefaultEndpoint;
        }

        private void SaveBouyomiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = BouyomiChanClient.CreateTalkUri(BouyomiEndpointTextBox.Text, "test");
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "棒読みちゃん連携");
                return;
            }

            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.BouyomiEnabled,
                BouyomiEnabledCheckBox.IsChecked == true ? "1" : "0");
            DAO_Setting.InsertUpdate(
                DAO_Setting.SettingName.BouyomiEndpoint,
                BouyomiEndpointTextBox.Text.Trim());

            mainWindow.ChatPanel.ReloadBouyomiSettings();
            MessageBox.Show("読み上げ設定を保存しました。", "棒読みちゃん連携");
        }

        private async void TestBouyomiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var client = new BouyomiChanClient();
                await client.SpeakAsync(
                    BouyomiEndpointTextBox.Text,
                    "JTSAの読み上げテストです。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"読み上げに失敗しました。\n{ex.Message}",
                    "棒読みちゃん連携");
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

        private async void ReOAuthButton_Click(object sender, RoutedEventArgs e)
        {
            var deviceCodeResponse = await TwitchHelper.RequestDeviceCodeAsync();

            // 認証URLとユーザーコードをユーザーに表示
            SettingOAuthCodeBox.Text = deviceCodeResponse.user_code;

            // 認証ページを自動で開く
            Process.Start(new ProcessStartInfo(deviceCodeResponse.verification_uri + $"user_code={JTSAHelper.LoginName}") { UseShellExecute = true });

            // アクセストークン取得
            var accessTokenResponse = await TwitchHelper.PollDeviceTokenAsync(deviceCodeResponse.device_code, deviceCodeResponse.interval, deviceCodeResponse.expires_in);

            if (accessTokenResponse != null)
            {
                TwitchHelper.AccessToken = accessTokenResponse.accessToken;
                mainWindow.AccessToken_TextBlock.Text = "OK!";
            }
            else
            {
                mainWindow.AccessToken_TextBlock.Text = "NG";
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

            await mainWindow.StreamerDataSet();
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
