using JTSA.Dao;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public partial class ObsSettingPanel : UserControl
{
    public ObsSettingPanel()
    {
        InitializeComponent();
        var accounts = DAO_TwitchAccount.SelectAll();
        MainTwitchAccountComboBox.ItemsSource = accounts;
        SubTwitchAccountComboBox.ItemsSource = accounts;
        ReloadSettings();
    }

    public void ReloadSettings()
    {
        ObsUrlTextBox.Text = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketUrl)?.Value
            ?? MainWindow.DefaultObsWebSocketUrl;
        ObsPasswordBox.Password = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketPassword)?.Value ?? "";
        ObsAutoConnectCheckBox.IsChecked =
            DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsAutoConnect)?.Value == "1";
        if (long.TryParse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainObsTwitchAccountId)?.Value, out var mainAccountId))
            MainTwitchAccountComboBox.SelectedValue = mainAccountId;

        SubObsUrlTextBox.Text = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsWebSocketUrl)?.Value
            ?? "ws://127.0.0.1:4456";
        SubObsPasswordBox.Password = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsWebSocketPassword)?.Value ?? "";
        if (long.TryParse(DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsTwitchAccountId)?.Value, out var subAccountId))
            SubTwitchAccountComboBox.SelectedValue = subAccountId;
        UpdateSubObsVisibility();
    }

    public void SetConnectionStatus(bool connected, string? message = null, bool isSub = false)
    {
        var status = isSub ? SubObsConnectionStatusTextBlock : ObsConnectionStatusTextBlock;
        status.Text = message ?? (connected ? "接続済み" : "未接続");
        status.Foreground = connected
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
        if (MainTwitchAccountComboBox.SelectedValue is not long)
        {
            MessageBox.Show("メインOBSに紐づけるTwitchアカウントを選択してください。", "OBS連携");
            return;
        }
        if (SubTwitchAccountComboBox.SelectedValue is not null &&
            Equals(MainTwitchAccountComboBox.SelectedValue, SubTwitchAccountComboBox.SelectedValue))
        {
            MessageBox.Show("メインOBSとサブOBSに同じTwitchアカウントは割り当てられません。", "OBS連携");
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
        if (MainTwitchAccountComboBox.SelectedValue is long accountId)
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.MainObsTwitchAccountId, accountId.ToString());

        var mainWindow = (MainWindow)Application.Current.MainWindow;
        await mainWindow.ConnectObsAsync(forceReconnect: connectionSettingsChanged, showError: true);
        mainWindow.RefreshObsControlTarget();
    }

    private async void SaveAndTestSubButton_Click(object sender, RoutedEventArgs e)
    {
        var url = SubObsUrlTextBox.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("ws" or "wss"))
        {
            MessageBox.Show("WebSocket URLは ws:// または wss:// から入力してください。", "サブOBS連携");
            return;
        }

        if (SubTwitchAccountComboBox.SelectedValue is not long)
        {
            MessageBox.Show("サブOBSに紐づけるTwitchアカウントを選択してください。", "サブOBS連携");
            return;
        }
        if (Equals(MainTwitchAccountComboBox.SelectedValue, SubTwitchAccountComboBox.SelectedValue))
        {
            MessageBox.Show("メインOBSとサブOBSに同じTwitchアカウントは割り当てられません。", "OBS連携");
            return;
        }

        var savedUrl = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsWebSocketUrl)?.Value ?? "ws://127.0.0.1:4456";
        var savedPassword = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsWebSocketPassword)?.Value ?? "";
        var changed = !string.Equals(savedUrl, url, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(savedPassword, SubObsPasswordBox.Password, StringComparison.Ordinal);

        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SubObsWebSocketUrl, url);
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SubObsWebSocketPassword, SubObsPasswordBox.Password);
        if (SubTwitchAccountComboBox.SelectedValue is long accountId)
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SubObsTwitchAccountId, accountId.ToString());

        var mainWindow = (MainWindow)Application.Current.MainWindow;
        await mainWindow.ConnectObsAsync(forceReconnect: changed, showError: true, isSub: true);
        UpdateSubObsVisibility();
        mainWindow.RefreshObsControlTarget();
    }

    private void AddSubObsButton_Click(object sender, RoutedEventArgs e)
    {
        SubObsSettingBorder.Visibility = Visibility.Visible;
        AddSubObsButton.Visibility = Visibility.Collapsed;
    }

    private void RemoveSubObsButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("サブOBSの登録を削除しますか？", "OBS連携",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SubObsTwitchAccountId, "");
        SubTwitchAccountComboBox.SelectedIndex = -1;
        SetConnectionStatus(false, "未登録", isSub: true);
        UpdateSubObsVisibility();
        ((MainWindow)Application.Current.MainWindow).RefreshObsControlTarget();
    }

    private void UpdateSubObsVisibility()
    {
        var isRegistered = long.TryParse(
            DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsTwitchAccountId)?.Value,
            out _);
        SubObsSettingBorder.Visibility = isRegistered ? Visibility.Visible : Visibility.Collapsed;
        AddSubObsButton.Visibility = isRegistered ? Visibility.Collapsed : Visibility.Visible;
    }
}
