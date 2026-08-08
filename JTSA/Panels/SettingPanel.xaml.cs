using JTSA.Dao;
using JTSA.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    }
}
