using JTSA.Dao;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.ComponentModel;
using JTSA.Models;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public partial class ObsSettingPanel : UserControl
{
    private readonly ObservableCollection<ObsTextSourceCard> textSourceCards = [];
    private readonly TaskCompletionSource<bool> panelLoaded =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool isRestoringCards;
    private bool cardsLoaded;

    public ObsSettingPanel()
    {
        InitializeComponent();
        TextSourceCardsItemsControl.ItemsSource = textSourceCards;
        var accounts = DAO_TwitchAccount.SelectAll();
        MainTwitchAccountComboBox.ItemsSource = accounts;
        SubTwitchAccountComboBox.ItemsSource = accounts;
        ReloadSettings();
        RestoreTextSourceCards();
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

    private void ObsAutoConnectCheckBox_Click(object sender, RoutedEventArgs e)
    {
        DAO_Setting.InsertUpdate(
            DAO_Setting.SettingName.ObsAutoConnect,
            ObsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
    }

    private void ObsSettingPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (cardsLoaded)
        {
            panelLoaded.TrySetResult(true);
            return;
        }
        cardsLoaded = true;
        foreach (var card in textSourceCards)
            card.Status = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsAutoConnect)?.Value == "1"
                ? "OBS接続完了後に読み込みます"
                : "自動接続オフ（文言読込を押すと接続します）";
        panelLoaded.TrySetResult(true);
    }

    public async Task RefreshSavedTextSourcesAsync(ObsController controller, bool isSub)
    {
        // ローカルOBSは接続が速く、画面のLoadedより先にここへ到達することがある。
        // カード初期表示による状態上書きを防ぐため、パネル準備完了後に読み込む。
        if (await Task.WhenAny(panelLoaded.Task, Task.Delay(TimeSpan.FromSeconds(5))) != panelLoaded.Task)
            return;
        isRestoringCards = true;
        try
        {
            foreach (var card in textSourceCards.Where(card =>
                         card.IsSub == isSub && card.SelectedScene is not null))
            {
                var sceneName = card.SelectedScene;
                var sourceName = card.SelectedSource;
                card.Controller = controller;
                card.Status = "文言読込中...";

                var scenes = await Task.Run(controller.GetSceneNames);
                ReplaceItems(card.Scenes, scenes);
                card.SelectedScene = sceneName;
                if (sceneName is null || !card.Scenes.Contains(sceneName))
                {
                    card.Status = "保存済みシーンが見つかりません";
                    continue;
                }

                var sources = await Task.Run(() => controller.GetTextSourceNames(sceneName));
                ReplaceItems(card.Sources, sources);
                card.SelectedSource = sourceName;
                if (sourceName is null || !card.Sources.Contains(sourceName))
                {
                    card.Status = "保存済みソースが見つかりません";
                    continue;
                }

                card.Text = await Task.Run(() => controller.GetTextSourceText(sourceName));
                card.IsTextLoaded = true;
                card.Status = "保存済み設定を読み込みました";
            }
        }
        catch (Exception ex)
        {
            foreach (var card in textSourceCards.Where(card => card.IsSub == isSub))
                card.Status = $"読込失敗: {ex.GetBaseException().Message}";
        }
        finally
        {
            isRestoringCards = false;
        }
    }

    private void AddTextSourceCardButton_Click(object sender, RoutedEventArgs e)
    {
        textSourceCards.Add(new ObsTextSourceCard());
    }

    private void RemoveTextSourceCardButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ObsTextSourceCard card)
        {
            textSourceCards.Remove(card);
            SaveTextSourceCards();
        }
    }

    private async void CardObs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.Tag is not ObsTextSourceCard card || isRestoringCards)
            return;

        var isSub = ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "sub";
        // ItemsControl生成時にもSelectionChangedが発火するため、保存値と同じ初期選択では
        // 復元済みのシーン・ソースを消さない。
        if (card.IsSub == isSub && card.SelectedScene is not null)
            return;

        card.IsSub = isSub;
        card.SelectedScene = null;
        card.SelectedSource = null;
        card.Scenes.Clear();
        card.Sources.Clear();
        card.Text = "";
        card.IsTextLoaded = false;
        await LoadScenesAsync(card);
    }

    private async Task LoadScenesAsync(ObsTextSourceCard card)
    {
        card.Status = "接続中...";
        try
        {
            card.Controller = await ((MainWindow)Application.Current.MainWindow).EnsureObsConnectedAsync(card.IsSub);
            if (card.Controller is null)
            {
                card.Status = "OBSに接続できません";
                return;
            }

            ReplaceItems(card.Scenes, card.Controller.GetSceneNames());
            card.Status = card.Scenes.Count == 0 ? "シーンがありません" : "シーンを選択してください";
        }
        catch (Exception ex)
        {
            card.Status = $"接続失敗: {ex.GetBaseException().Message}";
        }
    }

    private async void CardScene_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.Tag is not ObsTextSourceCard card || isRestoringCards || card.SelectedScene is null)
            return;

        // 保存済みカードの初期描画イベントでは、復元済みソースを維持する。
        // Loaded後の再取得処理が一覧と現在テキストを更新する。
        if (card.Controller is null && card.SelectedSource is not null)
            return;

        card.SelectedSource = null;
        card.Sources.Clear();
        card.Text = "";
        card.IsTextLoaded = false;
        if (card.Controller is null)
            await LoadScenesAsync(card);

        try
        {
            if (card.Controller is null)
                return;
            ReplaceItems(card.Sources, card.Controller.GetTextSourceNames(card.SelectedScene));
            card.Status = card.Sources.Count == 0 ? "GDI+テキストソースがありません" : "ソースを選択してください";
        }
        catch (Exception ex)
        {
            card.Status = $"ソース取得失敗: {ex.GetBaseException().Message}";
        }
    }

    private void CardSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.Tag is not ObsTextSourceCard card || isRestoringCards || card.SelectedSource is null || card.Controller is null)
            return;

        try
        {
            card.Text = card.Controller.GetTextSourceText(card.SelectedSource);
            card.IsTextLoaded = true;
            card.Status = "現在の文言を取得しました";
        }
        catch (Exception ex)
        {
            card.Status = $"文言取得失敗: {ex.GetBaseException().Message}";
        }
        SaveTextSourceCards();
    }

    private void CardDisplayName_LostFocus(object sender, RoutedEventArgs e)
    {
        if ((sender as TextBox)?.Tag is ObsTextSourceCard card && card.SelectedSource is not null)
            SaveTextSourceCards();
    }

    private async void ApplySourceCardButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ObsTextSourceCard card || card.SelectedSource is null)
            return;

        try
        {
            // 自動接続オフで復元されたカードも、OBS反映を押した時点で明示的に接続する。
            if (card.Controller is null)
            {
                card.Status = "接続中...";
                card.Controller = await ((MainWindow)Application.Current.MainWindow)
                    .EnsureObsConnectedAsync(card.IsSub);
            }

            if (card.Controller is null)
            {
                card.Status = "OBSに接続できません";
                return;
            }

            if (!card.IsTextLoaded)
            {
                card.Text = card.Controller.GetTextSourceText(card.SelectedSource);
                card.IsTextLoaded = true;
                card.Status = "現在の文言を読み込みました";
                return;
            }

            card.Controller.SetTextSourceText(card.SelectedSource, card.Text);
            card.Status = "反映しました";
            SaveTextSourceCards();
        }
        catch (Exception ex)
        {
            card.Status = $"反映失敗: {ex.GetBaseException().Message}";
        }
    }

    private void RestoreTextSourceCards()
    {
        isRestoringCards = true;
        try
        {
            foreach (var setting in DAO_ObsTextSource.SelectAll())
            {
                var card = new ObsTextSourceCard { IsSub = setting.IsSubObs, DisplayName = setting.DisplayName };
                if (setting.SceneName is not null) card.Scenes.Add(setting.SceneName);
                if (setting.SourceName is not null) card.Sources.Add(setting.SourceName);
                card.SelectedScene = setting.SceneName;
                card.SelectedSource = setting.SourceName;
                card.Status = "接続先を選び直すと一覧を更新します";
                textSourceCards.Add(card);
            }
            if (textSourceCards.Count == 0)
                textSourceCards.Add(new ObsTextSourceCard());
        }
        catch
        {
            textSourceCards.Add(new ObsTextSourceCard { Status = "保存済み設定を読み込めませんでした" });
        }
        finally
        {
            isRestoringCards = false;
        }
    }

    private void SaveTextSourceCards()
    {
        if (isRestoringCards) return;
        DAO_ObsTextSource.ReplaceAll(textSourceCards
            .Where(card => card.SelectedScene is not null && card.SelectedSource is not null)
            .Select((card, index) => new M_ObsTextSource
        {
            IsSubObs = card.IsSub,
            DisplayName = card.DisplayName,
            SceneName = card.SelectedScene,
            SourceName = card.SelectedSource,
            SortNumber = index,
            UpdatedDateTime = DateTime.Now
        }));
    }

    private static void ReplaceItems(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private sealed class ObsTextSourceCard : INotifyPropertyChanged
    {
        private string text = "";
        private string status = "";
        private string? selectedScene;
        private string? selectedSource;
        private bool isTextLoaded;
        public string DisplayName { get; set; } = "";
        public bool IsSub { get; set; }
        public int ObsSelectedIndex { get => IsSub ? 1 : 0; set => IsSub = value == 1; }
        public ObservableCollection<string> Scenes { get; } = [];
        public ObservableCollection<string> Sources { get; } = [];
        public string? SelectedScene
        {
            get => selectedScene;
            set
            {
                if (selectedScene == value) return;
                selectedScene = value;
                Notify();
            }
        }
        public string? SelectedSource
        {
            get => selectedSource;
            set
            {
                if (selectedSource == value) return;
                selectedSource = value;
                Notify();
            }
        }
        public ObsController? Controller { get; set; }
        public bool IsTextLoaded
        {
            get => isTextLoaded;
            set
            {
                if (isTextLoaded == value) return;
                isTextLoaded = value;
                Notify();
                Notify(nameof(ActionButtonText));
            }
        }
        public string ActionButtonText => IsTextLoaded ? "OBS反映" : "文言読込";
        public string Text { get => text; set { text = value; Notify(); } }
        public string Status { get => status; set { status = value; Notify(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
