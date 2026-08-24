using JTSA.Dao;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public partial class ObsSettingPanel : UserControl
{
    public ObsSettingPanel()
    {
        InitializeComponent();
        ReloadSettings();
    }

    public void ReloadSettings()
    {
        ObsUrlTextBox.Text = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketUrl)?.Value
            ?? MainWindow.DefaultObsWebSocketUrl;
        ObsPasswordBox.Password = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketPassword)?.Value ?? "";
        ObsAutoConnectCheckBox.IsChecked =
            DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsAutoConnect)?.Value == "1";
    }

    public void SetConnectionStatus(bool connected, string? message = null)
    {
        ObsConnectionStatusTextBlock.Text = message ?? (connected ? "接続済み" : "未接続");
        ObsConnectionStatusTextBlock.Foreground = connected
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.Orange;
    }

    private async void SaveAndTestButton_Click(object sender, RoutedEventArgs e)
    {
        var url = ObsUrlTextBox.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("ws" or "wss"))
        {
            MessageBox.Show("WebSocket URLは ws:// または wss:// から入力してください。", "OBS連携");
            return;
        }

        var savedUrl = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketUrl)?.Value
            ?? MainWindow.DefaultObsWebSocketUrl;
        var savedPassword = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketPassword)?.Value ?? "";
        var connectionSettingsChanged =
            !string.Equals(savedUrl, url, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(savedPassword, ObsPasswordBox.Password, StringComparison.Ordinal);

        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ObsWebSocketUrl, url);
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ObsWebSocketPassword, ObsPasswordBox.Password);
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ObsAutoConnect,
            ObsAutoConnectCheckBox.IsChecked == true ? "1" : "0");

        var mainWindow = (MainWindow)Application.Current.MainWindow;
        await mainWindow.ConnectObsAsync(forceReconnect: connectionSettingsChanged, showError: true);
    }
}
