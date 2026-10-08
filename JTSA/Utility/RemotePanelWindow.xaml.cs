using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private readonly ObsSettingPanel obsSettingPanel;
    private readonly Func<long?> getAccountId;
    private readonly System.Windows.Threading.Dispatcher dispatcher;
    private readonly Action<Exception> onError;
    private readonly string accessKey;
    private RemotePanelServer? server;
    private readonly SemaphoreSlim previewLock = new(1, 1);

    public bool IsRunning => server != null;
    public string Pin => server?.Pin ?? string.Empty;
    public IReadOnlyList<RemotePanelAddress> Addresses { get; private set; } = [];

    public RemotePanelController(
        ChatPanel chatPanel,
        ObsSettingPanel obsSettingPanel,
        Func<long?> getAccountId,
        System.Windows.Threading.Dispatcher dispatcher,
        Action<Exception> onError)
    {
        this.chatPanel = chatPanel;
        this.obsSettingPanel = obsSettingPanel;
        this.getAccountId = getAccountId;
        this.dispatcher = dispatcher;
        this.onError = onError;
        var savedOrder = DAO_Setting.SelectOneById(DAO_Setting.SettingName.RemotePanelOrder)?.Value;
        if (!string.IsNullOrEmpty(savedOrder))
        {
            try { RemotePanelRegistry.SetOrder(JsonSerializer.Deserialize<string[]>(savedOrder) ?? []); }
            catch (JsonException) { }
        }
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
                GetSnapshot,
                chatPanel.ApplyTodoChange,
                ApplyObsChangeAsync,
                GetPreviewAsync,
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

    private async Task<RemotePreview> GetPreviewAsync(bool isSub)
    {
        if (!previewLock.Wait(0)) return new(null, null, "画像を取得中です");
        try
        {
            var controller = (Application.Current.MainWindow as MainWindow)?.GetConnectedObsController(isSub);
            if (controller == null) return new(null, null, $"{(isSub ? "サブ" : "メイン")}OBSが未接続です。PCでOBSに接続してください。");
            var screenshot = await Task.Run(controller.GetProgramScreenshot);
            return new(screenshot.SceneName, screenshot.ImageData, null);
        }
        catch (Exception)
        {
            return new(null, null, "OBSの画像を取得できませんでした。OBSの接続状態を確認してください。");
        }
        finally { previewLock.Release(); }
    }

    private RemotePanelSnapshot GetSnapshot()
    {
        var snapshot = chatPanel.GetRemotePanelSnapshot() with
        {
            Panels = RemotePanelRegistry.GetPanels()
        };
        var accountId = getAccountId();
        if (accountId is null) return snapshot;
        var scenes = obsSettingPanel.GetSceneSwitchPresets(accountId)
            .Select(item => new RemoteObsSceneInfo(item.AccountId, item.IsSub, item.SceneName,
                item.ShortcutDisplayName, item.IsCurrentScene)).ToArray();
        var sources = obsSettingPanel.GetSourceSwitchPresets(accountId)
            .Select(item => new RemoteObsSourceInfo(item.AccountId, item.IsSub, item.SceneName, item.SourceName,
                item.ContainerName, item.ShortcutDisplayName, item.DetailText, item.IsVisible)).ToArray();
        return snapshot with { Scenes = scenes, Sources = sources };
    }

    private async Task<bool> ApplyObsChangeAsync(RemoteObsChange change)
    {
        var accountId = getAccountId();
        if (accountId is null || accountId.Value != change.AccountId) return false;
        if (change.Action == "scene")
        {
            var preset = obsSettingPanel.GetSceneSwitchPresets(change.AccountId).FirstOrDefault(item =>
                item.IsSub == change.IsSub &&
                string.Equals(item.SceneName, change.SceneName, StringComparison.Ordinal));
            if (preset == null) return false;
            await obsSettingPanel.ExecuteSceneSwitchPresetAsync(preset);
            return true;
        }
        if (change.Action == "source")
        {
            var preset = obsSettingPanel.GetSourceSwitchPresets(change.AccountId).FirstOrDefault(item =>
                item.IsSub == change.IsSub &&
                string.Equals(item.SceneName, change.SceneName, StringComparison.Ordinal) &&
                string.Equals(item.SourceName, change.SourceName ?? string.Empty, StringComparison.Ordinal) &&
                string.Equals(item.ContainerName, change.ContainerName ?? string.Empty, StringComparison.Ordinal));
            if (preset == null) return false;
            await obsSettingPanel.ExecuteSourceSwitchPresetAsync(preset);
            return true;
        }
        return false;
    }

    public void Dispose() => Stop();
}

public partial class RemotePanelWindow : Window
{
    private readonly RemotePanelController controller;
    private bool settingsLoaded;
    private Point dragStart;
    private string? dragPanelId;
    private ObservableCollection<RemotePanelInfo>? dragPreview;
    private const string PanelDragFormat = "JTSA.RemotePanelId";

    private void PanelDragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(PanelListBox);
        dragPanelId = (sender as FrameworkElement)?.DataContext is RemotePanelInfo panel ? panel.Id : null;
    }

    private void PanelDragHandle_MouseUp(object sender, MouseButtonEventArgs e) => dragPanelId = null;

    private void PanelDragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not FrameworkElement { DataContext: RemotePanelInfo panel } handle) return;
        if (dragPanelId != panel.Id) return;
        var position = e.GetPosition(PanelListBox);
        if (Math.Abs(position.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragPreview = new ObservableCollection<RemotePanelInfo>(RemotePanelRegistry.GetPanels());
        PanelListBox.ItemsSource = dragPreview;
        PanelListBox.UpdateLayout();
        try
        {
            DragDrop.DoDragDrop(PanelListBox, new DataObject(PanelDragFormat, panel.Id), DragDropEffects.Move);
        }
        finally
        {
            dragPreview = null;
            dragPanelId = null;
            RefreshPanelList();
        }
        e.Handled = true;
    }

    private void PanelListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = dragPreview != null && e.Data.GetData(PanelDragFormat) is string id && id == dragPanelId
            ? DragDropEffects.Move : DragDropEffects.None;
        if (e.Effects == DragDropEffects.Move) UpdateDragPreview(e.GetPosition(PanelListBox).Y);
        e.Handled = true;
    }

    private void UpdateDragPreview(double pointerY)
    {
        if (dragPreview == null) return;
        var source = dragPreview.ToList().FindIndex(panel => panel.Id == dragPanelId);
        if (source < 0) return;
        var positions = new Dictionary<string, double>();
        var destination = 0;
        for (var i = 0; i < dragPreview.Count; i++)
        {
            if (PanelListBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item) return;
            var visualY = item.TranslatePoint(new Point(0, 0), PanelListBox).Y;
            var offset = (item.RenderTransform as TranslateTransform)?.Y ?? 0;
            positions[dragPreview[i].Id] = visualY;
            // Use the settled row positions so an animation cannot trigger repeated swaps.
            if (i != source && pointerY > visualY - offset + item.ActualHeight / 2) destination++;
        }
        if (source == destination) return;
        dragPreview.Move(source, destination);
        PanelListBox.UpdateLayout();
        for (var i = 0; i < dragPreview.Count; i++)
        {
            if (PanelListBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item) continue;
            item.RenderTransform = Transform.Identity;
            var settledY = item.TranslatePoint(new Point(0, 0), PanelListBox).Y;
            var transform = new TranslateTransform();
            item.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(positions[dragPreview[i].Id] - settledY, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
        }
    }

    private void PanelListBox_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (dragPreview == null || e.Data.GetData(PanelDragFormat) is not string id || id != dragPanelId) return;
        UpdateDragPreview(e.GetPosition(PanelListBox).Y);
        var order = dragPreview.Select(panel => panel.Id).ToArray();
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.RemotePanelOrder, JsonSerializer.Serialize(order));
        RemotePanelRegistry.SetOrder(order);
        e.Effects = DragDropEffects.Move;
    }

    internal RemotePanelWindow(RemotePanelController controller)
    {
        this.controller = controller;
        InitializeComponent();
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(
            QrImage, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
        AutoStartCheckBox.IsChecked =
            DAO_Setting.SelectOneById(DAO_Setting.SettingName.RemotePanelAutoStart)?.Value == "1";
        settingsLoaded = true;
        RemotePanelRegistry.Changed += RefreshPanelList;
        Closed += (_, _) => RemotePanelRegistry.Changed -= RefreshPanelList;
        RefreshPanelList();
        RefreshState();
    }

    private void RefreshPanelList()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(RefreshPanelList);
            return;
        }
        if (dragPreview == null) PanelListBox.ItemsSource = RemotePanelRegistry.GetPanels();
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
