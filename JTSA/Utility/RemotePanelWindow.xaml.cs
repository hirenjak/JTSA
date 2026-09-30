using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media.Imaging;
using JTSA.Dao;
using JTSA.Panels;
using QRCoder;

namespace JTSA.Utility;

internal sealed record RemotePanelAddress(string Label, string Url);

internal sealed class RemotePanelController : IDisposable
{
    private const int Port = 8027;
    private readonly ChatPanel chatPanel;
    private readonly System.Windows.Threading.Dispatcher dispatcher;
    private readonly Action<Exception> onError;
    private readonly string accessKey;
    private RemotePanelServer? server;

    public bool IsRunning => server != null;
    public string Pin => server?.Pin ?? string.Empty;
    public IReadOnlyList<RemotePanelAddress> Addresses { get; private set; } = [];

    public RemotePanelController(ChatPanel chatPanel, System.Windows.Threading.Dispatcher dispatcher, Action<Exception> onError)
    {
        this.chatPanel = chatPanel;
        this.dispatcher = dispatcher;
        this.onError = onError;
        var savedKey = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RemotePanelAccessKey)?.Value;
        accessKey = savedKey is { Length: 48 } && savedKey.All(Uri.IsHexDigit)
            ? savedKey
            : Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        if (!string.Equals(savedKey, accessKey, StringComparison.Ordinal))
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.RemotePanelAccessKey, accessKey);
    }

    public void Start()
    {
        if (server != null) return;
        try
        {
            var started = new RemotePanelServer(
                chatPanel.GetRemotePanelSnapshot,
                chatPanel.ApplyTodoChange,
                dispatcher,
                accessKey,
                Port,
                onError);
            var addresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                .OrderBy(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 :
                    nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 2)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses
                    .Where(entry => entry.Address.AddressFamily == AddressFamily.InterNetwork &&
                                    !IPAddress.IsLoopback(entry.Address))
                    .Select(entry => new RemotePanelAddress(
                        $"{nic.Name} · {entry.Address}",
                        $"http://{entry.Address}:{Port}/controls/")))
                .DistinctBy(item => item.Url)
                .ToArray();
            server = started;
            Addresses = addresses;
        }
        catch (Exception ex)
        {
            onError(ex);
            throw;
        }
    }

    public void Stop()
    {
        server?.Dispose();
        server = null;
        Addresses = [];
    }

    public void RegeneratePin() => server?.RegeneratePin();

    public void Dispose() => Stop();
}

public partial class RemotePanelWindow : Window
{
    private readonly RemotePanelController controller;
    private bool settingsLoaded;

    internal RemotePanelWindow(RemotePanelController controller)
    {
        this.controller = controller;
        InitializeComponent();
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(
            QrImage, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
        AutoStartCheckBox.IsChecked =
            DAO_Setting.SelectOneById(DAO_Setting.SettingName.RemotePanelAutoStart)?.Value == "1";
        settingsLoaded = true;
        RefreshState();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            controller.Start();
            RefreshState();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"開始できませんでした: {ex.GetBaseException().Message}";
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        controller.Stop();
        RefreshState();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(AddressTextBox.Text)) Clipboard.SetText(AddressTextBox.Text);
    }

    private void RegeneratePinButton_Click(object sender, RoutedEventArgs e)
    {
        controller.RegeneratePin();
        PairingCodeTextBlock.Text = controller.Pin;
    }

    private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (settingsLoaded) DAO_Setting.InsertUpdate(DAO_Setting.SettingName.RemotePanelAutoStart, "1");
    }

    private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (settingsLoaded) DAO_Setting.InsertUpdate(DAO_Setting.SettingName.RemotePanelAutoStart, "0");
    }

    private void RefreshState()
    {
        NetworkAddressesComboBox.ItemsSource = controller.Addresses;
        PairingCodeTextBlock.Text = controller.Pin;
        RegeneratePinButton.IsEnabled = controller.IsRunning;
        if (!controller.IsRunning)
        {
            QrImage.Source = null;
            AddressTextBox.Text = string.Empty;
            StatusTextBlock.Text = "停止中。［サーバー起動］を押すと共有を開始します。";
            return;
        }
        if (controller.Addresses.Count == 0)
        {
            StatusTextBlock.Text = "共有中ですが、接続できるIPv4アドレスが見つかりません。";
            return;
        }
        NetworkAddressesComboBox.SelectedIndex = 0;
        StatusTextBlock.Text = "共有中。接続コードは他人に渡さないでください。";
    }

    private void NetworkAddressesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selected = NetworkAddressesComboBox.SelectedItem as RemotePanelAddress;
        AddressTextBox.Text = selected?.Url ?? string.Empty;
        QrImage.Source = null;
        if (selected == null) return;
        var png = PngByteQRCodeHelper.GetQRCode(selected.Url, QRCodeGenerator.ECCLevel.M, 5);
        using var stream = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        QrImage.Source = bitmap;
    }
}
