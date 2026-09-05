using JTSA.Dao;
using JTSA.Models;
using JTSA.Utility;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public class StreamExpansionHeaderForm : INotifyPropertyChanged
{
    private string headerName = string.Empty;
    private bool isActive;
    private bool doShoutout;
    public long HeaderId { get; set; }
    public string HeaderName { get => headerName; set { headerName = value; Changed(); } }
    public bool IsActive { get => isActive; set { isActive = value; Changed(); } }
    public bool IsRaid { get; set; }
    public bool IsSubscribe { get; set; }
    public bool IsBits { get; set; }
    public bool IsFirstChat { get; set; }
    public bool IsFollow { get; set; }
    public bool IsHourly { get; set; }
    public bool IsAdStart { get; set; }
    public bool IsAdEnd { get; set; }
    public bool IsAdUpcoming { get; set; }
    public int AdAdvanceMinutes { get; set; } = 1;
    public bool IsScheduledTime { get; set; }
    public int ScheduledHour { get; set; }
    public int ScheduledMinute { get; set; }
    public bool IsObsStreamStart { get; set; }
    public bool IsObsStreamStartMain { get; set; }
    public bool IsObsStreamStartSub { get; set; }
    public bool DoShoutout { get => doShoutout; set { doShoutout = value; Changed(); } }
    public int DelaySeconds { get; set; }
    public string TriggerComment { get; set; } = string.Empty;
    public bool ChatPermissionEveryone { get; set; }
    public bool ChatPermissionModerator { get; set; }
    public bool ChatPermissionVip { get; set; }
    public bool ChatPermissionSubscriber { get; set; }
    public string TriggerChannelPointId { get; set; } = string.Empty;
    public string TriggerChannelPointDisplayName { get; private set; } = string.Empty;
    public IReadOnlyList<string> ExecutionTimingItems
    {
        get
        {
            var items = new List<string>();
            if (IsRaid) items.Add("レイド");
            if (IsSubscribe) items.Add("サブスク");
            if (IsBits) items.Add("ビッツ");
            if (IsFirstChat) items.Add("チャット入室");
            if (IsFollow) items.Add("フォロー");
            if (IsAdStart) items.Add("CM開始");
            if (IsAdEnd) items.Add("CM終了予定");
            if (IsAdUpcoming) items.Add($"CM開始{AdAdvanceMinutes}分前");
            if (IsHourly) items.Add("時報（毎時00分）");
            if (IsScheduledTime) items.Add($"指定時刻：{ScheduledHour:00}:{ScheduledMinute:00}");
            if (IsObsStreamStartMain) items.Add("配信開始：メインOBS");
            if (IsObsStreamStartSub) items.Add("配信開始：サブOBS");
            if (!string.IsNullOrWhiteSpace(TriggerChannelPointId)) items.Add("チャンネルポイント");
            if (!string.IsNullOrWhiteSpace(TriggerComment)) items.Add("トリガーコメント");
            if (DelaySeconds > 0) items.Add($"遅延：{DelaySeconds}秒");
            return items;
        }
    }
    public string ExecutionTimingSummary => string.Join(" / ", ExecutionTimingItems);
    public string TriggerSummary =>
        !string.IsNullOrWhiteSpace(TriggerChannelPointId)
            ? $"CP：{(string.IsNullOrWhiteSpace(TriggerChannelPointDisplayName) ? TriggerChannelPointId : TriggerChannelPointDisplayName)}"
            : !string.IsNullOrWhiteSpace(TriggerComment) ? $"コメント：{TriggerComment}" :
        "イベント発火";

    public void SetTriggerChannelPointDisplayName(string displayName)
    {
        TriggerChannelPointDisplayName = displayName;
        Changed(nameof(TriggerChannelPointDisplayName));
        Changed(nameof(TriggerSummary));
    }

    public IReadOnlyList<string> ExecutionPermissionItems
    {
        get
        {
            var items = new List<string>();
            if (ChatPermissionEveryone) items.Add("全員");
            if (ChatPermissionModerator) items.Add("モデレーター");
            if (ChatPermissionVip) items.Add("VIP");
            if (ChatPermissionSubscriber) items.Add("サブスク");
            if (items.Count == 0) items.Add("配信者のみ");
            return items;
        }
    }

    public void NotifyExecutionTimingSummary()
    {
        Changed(nameof(ExecutionTimingItems));
        Changed(nameof(ExecutionTimingSummary));
        Changed(nameof(TriggerSummary));
    }
    public void NotifyExecutionPermissionItems() => Changed(nameof(ExecutionPermissionItems));
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public class StreamExpansionItemForm : INotifyPropertyChanged
{
    private bool isImage;
    private bool isAudio;
    private bool isChat;
    private int audioVolume = 100;
    private int weight = 1;
    private string probabilityText = "（000.0%）";
    private bool isImageSettingsExpanded;
    private bool isAudioSettingsExpanded;
    private bool isChatSettingsExpanded;
    private bool isTwitchSettingsExpanded;
    private bool isObsSettingsExpanded;
    private string imageContent = string.Empty;
    private string audioContent = string.Empty;
    private string chatContent = string.Empty;

    public bool IsImage { get => isImage; set { isImage = value; Changed(); } }
    public bool IsAudio { get => isAudio; set { isAudio = value; Changed(); } }
    public bool IsChat { get => isChat; set { isChat = value; Changed(); } }
    public string ImageContent { get => imageContent; set { imageContent = value ?? string.Empty; Changed(); Changed(nameof(IsImageConfigured)); } }
    public bool IsImageConfigured => !string.IsNullOrWhiteSpace(ImageContent);
    public int ImageWidth { get; set; } = 1920;
    public int ImageHeight { get; set; } = 1080;
    public int ImageX { get; set; }
    public int ImageY { get; set; }
    public bool IsImageRandomPosition { get; set; }
    public string AudioContent { get => audioContent; set { audioContent = value ?? string.Empty; Changed(); Changed(nameof(IsAudioConfigured)); } }
    public bool IsAudioConfigured => !string.IsNullOrWhiteSpace(AudioContent);
    public string ChatContent { get => chatContent; set { chatContent = value ?? string.Empty; Changed(); Changed(nameof(IsChatConfigured)); } }
    public bool IsChatConfigured => !string.IsNullOrWhiteSpace(ChatContent);
    public int Weight { get => weight; set { weight = value; Changed(); } }
    public int AudioVolume { get => audioVolume; set { audioVolume = Math.Clamp(value, 0, 100); Changed(); } }
    public string ProbabilityText { get => probabilityText; set { probabilityText = value; Changed(); } }
    public bool IsImageSettingsExpanded { get => isImageSettingsExpanded; set { isImageSettingsExpanded = value; Changed(); } }
    public bool IsAudioSettingsExpanded { get => isAudioSettingsExpanded; set { isAudioSettingsExpanded = value; Changed(); } }
    public bool IsChatSettingsExpanded { get => isChatSettingsExpanded; set { isChatSettingsExpanded = value; Changed(); } }
    public bool IsTwitchSettingsExpanded { get => isTwitchSettingsExpanded; set { isTwitchSettingsExpanded = value; Changed(); } }
    public bool IsObsSettingsExpanded { get => isObsSettingsExpanded; set { isObsSettingsExpanded = value; Changed(); } }
    public ObservableCollection<StreamExpansionObsTextForm> ObsTextForms { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}


public class StreamExpansionObsTextForm : INotifyPropertyChanged
{
    private bool isSubObs;
    private string sceneName = string.Empty;
    private string sourceName = string.Empty;
    public bool IsSubObs { get => isSubObs; set { isSubObs = value; Changed(); } }
    public int ObsSelectedIndex { get => IsSubObs ? 1 : 0; set { IsSubObs = value == 1; Changed(); } }
    public string SceneName { get => sceneName; set { sceneName = value; Changed(); } }
    public string SourceName { get => sourceName; set { sourceName = value; Changed(); } }
    public string TextTemplate { get; set; } = string.Empty;
    public ObservableCollection<string> SceneNames { get; } = [];
    public ObservableCollection<string> SourceNames { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public class StreamExpansionChannelPointForm
{
    public string RewardId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public partial class StereamExpansionPanel : UserControl , INotifyPropertyChanged
{
    private StreamExpansionHeaderForm? selectedHeader;
    private StreamExpansionHeaderForm? editingHeader;
    private bool isReloading;
    private bool isSwitchingHeader;
    private bool isSaving;
    private readonly StreamExpansionService streamExpansionService = new();
    private StreamExpansionPlaceholderHelpWindow? placeholderHelpWindow;

    public ObservableCollection<StreamExpansionHeaderForm> HeaderFormList { get; } = [];
    public ObservableCollection<StreamExpansionItemForm> ItemFormList { get; } = [];
    public ObservableCollection<StreamExpansionChannelPointForm> ChannelPointFormList { get; } = [];

    public StreamExpansionHeaderForm? SelectedHeader { get => selectedHeader; set { selectedHeader = value; PropertyChanged?.Invoke(this, new(nameof(SelectedHeader))); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public StereamExpansionPanel() 
    {
        InitializeComponent();
        ImplementationTabControl.Items.Remove(PlaceholderHelpTab);
        DataContext = this;


        Loaded += StereamExpansionPanel_Loaded;
        IsVisibleChanged += StereamExpansionPanel_IsVisibleChanged;
        AddHandler(TextBox.LostKeyboardFocusEvent, new System.Windows.Input.KeyboardFocusChangedEventHandler(AutoSaveTextBox_LostKeyboardFocus), true);
        AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(AutoSaveCheckBox_Click), true);
        AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(AutoSaveComboBox_SelectionChanged), true);
        AddHandler(System.Windows.Controls.Primitives.RangeBase.ValueChangedEvent,
            new RoutedPropertyChangedEventHandler<double>(AutoSaveSlider_ValueChanged), true);
    }

    private void StereamExpansionPanel_Loaded(object sender, RoutedEventArgs e)
    {
        ReloadChannelPoints();
        Reload();
    }

    private void StereamExpansionPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ReloadChannelPoints();
        }
    }

    /// <summary>
    /// チャンネルポイント選択肢再読み込み
    /// </summary>
    public void ReloadChannelPoints()
    {
        // Clearing the ItemsSource temporarily clears ComboBox.SelectedValue. Since the
        // binding is TwoWay, that transient value would otherwise overwrite and save the
        // channel point selected on the header when this panel becomes visible again.
        var triggerChannelPointIds = HeaderFormList.ToDictionary(
            header => header,
            header => header.TriggerChannelPointId);
        var wasReloading = isReloading;
        isReloading = true;
        try
        {
            ChannelPointFormList.Clear();
            ChannelPointFormList.Add(new()
            {
                RewardId = string.Empty,
                DisplayName = "（指定なし）"
            });

            foreach (var reward in DAO_ChannelPoint.SelectAll())
            {
                ChannelPointFormList.Add(new()
                {
                    RewardId = reward.RewardId,
                    DisplayName = $"{reward.Title}（{reward.Cost:N0}pt）"
                });
            }

            foreach (var (header, triggerChannelPointId) in triggerChannelPointIds)
            {
                header.TriggerChannelPointId = triggerChannelPointId;
            }

            RefreshTriggerChannelPointDisplayNames();
        }
        finally
        {
            isReloading = wasReloading;
        }
    }

    /// <summary>
    /// 再読み込み
    /// </summary>
    private void Reload(long selectId = 0)
    {
        isReloading = true;
        try
        {
        // ヘッダーリストの初期化
        HeaderFormList.Clear();

        // ヘッダー情報をDBから呼び出し
        var dbResults = DAO_StreamExpansion.SelectAllHeaders();
        foreach (var x in dbResults)
        {
            HeaderFormList.Add(new()
            {
                HeaderId = x.Id,
                HeaderName = x.Name,
                IsActive = x.IsActive,
                IsRaid = x.IsRaid,
                IsSubscribe = x.IsSubscribe,
                IsBits = x.IsBits,
                IsFirstChat = x.IsFirstChat,
                IsFollow = x.IsFollow,
                IsHourly = x.IsHourly,
                AdAdvanceMinutes = x.AdAdvanceMinutes,
                IsAdUpcoming = x.IsAdUpcoming,
                IsAdEnd = x.IsAdEnd,
                IsAdStart = x.IsAdStart,
                ScheduledMinute = x.ScheduledMinute,
                ScheduledHour = x.ScheduledHour,
                IsScheduledTime = x.IsScheduledTime,
                IsObsStreamStart = x.IsObsStreamStart,
                IsObsStreamStartMain = x.IsObsStreamStartMain,
                IsObsStreamStartSub = x.IsObsStreamStartSub,
                DoShoutout = x.DoShoutout,
                DelaySeconds = x.DelaySeconds,
                TriggerComment = x.TriggerComment,
                ChatPermissionEveryone = x.ChatPermissionEveryone,
                ChatPermissionModerator = x.ChatPermissionModerator,
                ChatPermissionVip = x.ChatPermissionVip,
                ChatPermissionSubscriber = x.ChatPermissionSubscriber,
                TriggerChannelPointId = x.TriggerChannelPointId
            });
        }

        RefreshTriggerChannelPointDisplayNames();

        // 選択しているアイテムを格納
        SelectedHeader = HeaderFormList.FirstOrDefault(x => x.HeaderId == selectId) ?? HeaderFormList.FirstOrDefault();
        StreamExpansionListBox.SelectedItem = SelectedHeader;
        }
        finally
        {
            isReloading = false;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var item = new StreamExpansionHeaderForm { 
            HeaderName = "新規作成", 
            IsActive = true 
        };

        HeaderFormList.Add(item); SelectedHeader = item; StreamExpansionListBox.SelectedItem = item; ClearItemForms();
        SaveCurrent();
    }

    private void OpenTriggerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedHeader is null) return;

        var window = new StreamExpansionTriggerSettingsWindow(SelectedHeader, ChannelPointFormList)
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() == true)
        {
            RefreshTriggerChannelPointDisplayNames();
            SaveCurrent();
        }
    }

    private void RefreshTriggerChannelPointDisplayNames()
    {
        foreach (var header in HeaderFormList)
        {
            var displayName = ChannelPointFormList
                .FirstOrDefault(x => x.RewardId == header.TriggerChannelPointId)?.DisplayName ?? string.Empty;
            header.SetTriggerChannelPointDisplayName(displayName);
        }
    }

    private void OpenExecutionPermissionSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedHeader is null) return;

        var window = new StreamExpansionPermissionSettingsWindow(SelectedHeader)
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() == true)
            SaveCurrent();
    }

    private void OpenPlaceholderHelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (placeholderHelpWindow is { IsLoaded: true })
        {
            placeholderHelpWindow.Activate();
            return;
        }

        placeholderHelpWindow = new StreamExpansionPlaceholderHelpWindow
        {
            Owner = Window.GetWindow(this)
        };
        placeholderHelpWindow.Closed += (_, _) => placeholderHelpWindow = null;
        placeholderHelpWindow.Show();
    }

    private void ImageSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm item)
            ToggleExclusiveSettings(item, nameof(item.IsImageSettingsExpanded));
    }

    private void AudioSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm item)
            ToggleExclusiveSettings(item, nameof(item.IsAudioSettingsExpanded));
    }

    private void ChatSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm item)
            ToggleExclusiveSettings(item, nameof(item.IsChatSettingsExpanded));
    }

    private void TwitchSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm item)
            ToggleExclusiveSettings(item, nameof(item.IsTwitchSettingsExpanded));
    }

    private void ObsSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm item)
            ToggleExclusiveSettings(item, nameof(item.IsObsSettingsExpanded));
    }

    private static void ToggleExclusiveSettings(StreamExpansionItemForm item, string targetProperty)
    {
        var shouldOpen = targetProperty switch
        {
            nameof(item.IsImageSettingsExpanded) => !item.IsImageSettingsExpanded,
            nameof(item.IsAudioSettingsExpanded) => !item.IsAudioSettingsExpanded,
            nameof(item.IsChatSettingsExpanded) => !item.IsChatSettingsExpanded,
            nameof(item.IsTwitchSettingsExpanded) => !item.IsTwitchSettingsExpanded,
            nameof(item.IsObsSettingsExpanded) => !item.IsObsSettingsExpanded,
            _ => false
        };

        item.IsImageSettingsExpanded = false;
        item.IsAudioSettingsExpanded = false;
        item.IsChatSettingsExpanded = false;
        item.IsTwitchSettingsExpanded = false;
        item.IsObsSettingsExpanded = false;

        if (!shouldOpen) return;
        switch (targetProperty)
        {
            case nameof(item.IsImageSettingsExpanded): item.IsImageSettingsExpanded = true; break;
            case nameof(item.IsAudioSettingsExpanded): item.IsAudioSettingsExpanded = true; break;
            case nameof(item.IsChatSettingsExpanded): item.IsChatSettingsExpanded = true; break;
            case nameof(item.IsTwitchSettingsExpanded): item.IsTwitchSettingsExpanded = true; break;
            case nameof(item.IsObsSettingsExpanded): item.IsObsSettingsExpanded = true; break;
        }
    }

    /// <summary>
    /// 左側一覧のチェック操作を、登録済み機能の有効状態へ即時反映する。
    /// </summary>
    private void ActiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.DataContext is not StreamExpansionHeaderForm header || header.HeaderId == 0)
        {
            return;
        }

        DAO_StreamExpansion.UpdateIsActive(header.HeaderId, header.IsActive);
    }


    /// <summary>
    /// 
    /// </summary>
    private void HeaderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        isSwitchingHeader = true;
        try
        {
            ClearItemForms();
            editingHeader = SelectedHeader;

            if (editingHeader is null || editingHeader.HeaderId == 0) return;
            foreach (var group in DAO_StreamExpansion.SelectItems(editingHeader.HeaderId).GroupBy(x => x.SortNumber))
            {
                var form = new StreamExpansionItemForm
                {
                    Weight = group.First().Weight
                };

                foreach (var item in group)
                {
                    switch (item.ActionType)
                    {
                        case "Image":
                            var imageSettings = StreamExpansionImageSettings.Decode(item.Content);
                            form.IsImage = true;
                            form.ImageContent = imageSettings.Path;
                            form.ImageWidth = imageSettings.Width;
                            form.ImageHeight = imageSettings.Height;
                            form.ImageX = imageSettings.X;
                            form.ImageY = imageSettings.Y;
                            form.IsImageRandomPosition = imageSettings.RandomPosition;
                            break;
                        case "Chat":
                            form.IsChat = true;
                            form.ChatContent = item.Content;
                            break;
                        case "ObsText":
                            form.ObsTextForms.Add(new()
                            {
                                IsSubObs = item.IsSubObs,
                                SceneName = item.ObsSceneName,
                                SourceName = item.ObsSourceName,
                                TextTemplate = item.Content
                            });
                            break;
                        default:
                            form.IsAudio = true;
                            form.AudioContent = item.Content;
                            form.AudioVolume = item.Volume;
                            break;
                    }
                }

                AddItemForm(form);
            }

            UpdateProbabilities();
        }
        finally
        {
            isSwitchingHeader = false;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    private void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        AddItemForm(new());
        UpdateProbabilities();
        SaveCurrent();
    }


    /// <summary>
    /// 
    /// </summary>
    private void DeleteItemButton_Click(object sender, RoutedEventArgs e) 
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm x)
        {
            x.PropertyChanged -= StreamExpansionItemForm_PropertyChanged;
            ItemFormList.Remove(x);
            UpdateProbabilities();
            SaveCurrent();
        }
    }

    private void AddItemForm(StreamExpansionItemForm form)
    {
        form.PropertyChanged += StreamExpansionItemForm_PropertyChanged;
        ItemFormList.Add(form);
    }

    private void ClearItemForms()
    {
        foreach (var form in ItemFormList)
        {
            form.PropertyChanged -= StreamExpansionItemForm_PropertyChanged;
        }

        ItemFormList.Clear();
    }

    private void StreamExpansionItemForm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StreamExpansionItemForm.Weight))
        {
            UpdateProbabilities();
        }
    }

    private void IncreaseWeightButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not StreamExpansionItemForm item) return;
        item.Weight++;
        SaveCurrent();
    }

    private void DecreaseWeightButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not StreamExpansionItemForm item) return;
        item.Weight = Math.Max(1, item.Weight - 1);
        SaveCurrent();
    }

    private void UpdateProbabilities()
    {
        var totalWeight = ItemFormList.Sum(form => Math.Max(1, form.Weight));
        foreach (var form in ItemFormList)
        {
            var probability = totalWeight == 0
                ? 0
                : Math.Max(1, form.Weight) * 100d / totalWeight;
            form.ProbabilityText = $"（{probability:000.0}%）";
        }
    }


    /// <summary>
    /// 音声・画像ファイル選択
    /// </summary>
    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StreamExpansionItemForm item)
        {
            return;
        }

        var actionType = (sender as Button)?.Tag?.ToString() ?? "Audio";

        var dialog = new OpenFileDialog
        {
            Filter = actionType == "Image"
                ? "画像ファイル|*.png;*.jpg;*.jpeg;*.gif;*.bmp|すべてのファイル|*.*"
                : "音声ファイル|*.wav;*.mp3;*.aac;*.wma;*.m4a|すべてのファイル|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            if (actionType == "Image")
            {
                item.ImageContent = dialog.FileName;
            }
            else
            {
                item.AudioContent = dialog.FileName;
            }
            FileExeItemListBox.Items.Refresh();
            SaveCurrent();
        }
    }

    private void TestImageButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StreamExpansionItemForm item) return;
        if (!File.Exists(item.ImageContent))
        {
            MessageBox.Show("テストする画像ファイルを選択してください。", "配信拡張");
            return;
        }

        StreamExpansionOverlayService.ShowImage(new StreamExpansionImageSettings(
            item.ImageContent,
            item.ImageWidth,
            item.ImageHeight,
            item.ImageX,
            item.ImageY,
            item.IsImageRandomPosition));
    }

    private async void TestAudioButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StreamExpansionItemForm item) return;
        if (!File.Exists(item.AudioContent))
        {
            MessageBox.Show("テストする音声ファイルを選択してください。", "配信拡張");
            return;
        }

        try
        {
            await streamExpansionService.PlayAudioPreviewAsync(item.AudioContent, item.AudioVolume);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"音声を再生できませんでした。\n{ex.GetBaseException().Message}", "配信拡張");
        }
    }

    private async void TestObsExecutionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StreamExpansionItemForm item ||
            Application.Current.MainWindow is not MainWindow mainWindow) return;

        var configuredItems = item.ObsTextForms
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceName))
            .ToList();
        if (configuredItems.Count == 0)
        {
            MessageBox.Show("テストするOBS実行設定がありません。", "配信拡張");
            return;
        }

        try
        {
            foreach (var obsText in configuredItems)
                await mainWindow.SetObsTextSourceAsync(obsText.IsSubObs, obsText.SourceName, obsText.TextTemplate);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OBS実行をテストできませんでした。\n{ex.GetBaseException().Message}", "OBS連携");
        }
    }


    /// <summary>
    /// 
    /// </summary>
    private void SaveCurrent()
    {
        if (isReloading || isSwitchingHeader || isSaving) return;
        var header = editingHeader;
        if (header is null || string.IsNullOrWhiteSpace(header.HeaderName))
        { 
            //StatusText.Text = "実装名を入力してね"; 
            return; 
        }

        isSaving = true;
        try
        {
        var saveItems = new List<T_StreamExpansionItem>();
        for (var groupIndex = 0; groupIndex < ItemFormList.Count; groupIndex++)
        {
            var form = ItemFormList[groupIndex];
            var imageContent = new StreamExpansionImageSettings(
                form.ImageContent,
                form.ImageWidth,
                form.ImageHeight,
                form.ImageX,
                form.ImageY,
                form.IsImageRandomPosition).Normalize().Encode();
            AddSaveItem(saveItems, form.IsImageConfigured, "Image", imageContent, form.Weight, 100, groupIndex);
            AddSaveItem(saveItems, form.IsAudioConfigured, "Audio", form.AudioContent, form.Weight, form.AudioVolume, groupIndex);
            AddSaveItem(saveItems, form.IsChatConfigured, "Chat", form.ChatContent, form.Weight, 100, groupIndex);
            foreach (var obsText in form.ObsTextForms)
            {
                if (string.IsNullOrWhiteSpace(obsText.SourceName)) continue;
                saveItems.Add(new T_StreamExpansionItem
                {
                    ActionType = "ObsText",
                    Content = obsText.TextTemplate ?? string.Empty,
                    Weight = form.Weight,
                    Volume = 100,
                    SortNumber = groupIndex,
                    IsSubObs = obsText.IsSubObs,
                    ObsSceneName = obsText.SceneName?.Trim() ?? string.Empty,
                    ObsSourceName = obsText.SourceName.Trim(),
                    UpdatedDateTime = DateTime.Now
                });
            }
        }

        var id = DAO_StreamExpansion.Save(new T_StreamExpansionHeader
        {
            Id = header.HeaderId,
            Name = header.HeaderName.Trim(),
            IsActive = header.IsActive,
            IsRaid = header.IsRaid,
            IsSubscribe = header.IsSubscribe,
            IsBits = header.IsBits,
            IsFirstChat = header.IsFirstChat,
            IsFollow = header.IsFollow,
            IsHourly = header.IsHourly,
            AdAdvanceMinutes = header.AdAdvanceMinutes,
            IsAdUpcoming = header.IsAdUpcoming,
            IsAdEnd = header.IsAdEnd,
            IsAdStart = header.IsAdStart,
            ScheduledMinute = header.ScheduledMinute,
            ScheduledHour = header.ScheduledHour,
            IsScheduledTime = header.IsScheduledTime,
            IsObsStreamStart = header.IsObsStreamStartMain || header.IsObsStreamStartSub,
            IsObsStreamStartMain = header.IsObsStreamStartMain,
            IsObsStreamStartSub = header.IsObsStreamStartSub,
            DoShoutout = header.DoShoutout,
            DelaySeconds = Math.Clamp(header.DelaySeconds, 0, 3600),
            TriggerComment = header.TriggerComment?.Trim() ?? "",
            ChatPermissionEveryone = header.ChatPermissionEveryone,
            ChatPermissionModerator = header.ChatPermissionModerator,
            ChatPermissionVip = header.ChatPermissionVip,
            ChatPermissionSubscriber = header.ChatPermissionSubscriber,
            TriggerChannelPointId = header.TriggerChannelPointId?.Trim() ?? "",
            UpdatedDateTime = DateTime.Now, 
            LastUsedDateTime = DateTime.Now
        }, saveItems);
        
        header.HeaderId = id;
        }
        finally
        {
            isSaving = false;
        }
    }


    private void AddObsTextButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is StreamExpansionItemForm item)
        {
            item.ObsTextForms.Add(new());
            SaveCurrent();
        }
    }

    private void DeleteObsTextButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is StreamExpansionItemForm item &&
            (sender as Button)?.DataContext is StreamExpansionObsTextForm card)
        {
            item.ObsTextForms.Remove(card);
            SaveCurrent();
        }
    }

    private void AutoSaveTextBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (e.OriginalSource is not TextBox textBox) return;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        FindVisualParent<ComboBox>(textBox)?.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
        SaveCurrent();
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T result) return result;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void AutoSaveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not CheckBox checkBox) return;
        if (checkBox.DataContext is StreamExpansionHeaderForm header && header != editingHeader) return;
        checkBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateSource();
        SaveCurrent();
    }

    private void AutoSaveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not ComboBox comboBox) return;
        comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
        comboBox.GetBindingExpression(ComboBox.SelectedIndexProperty)?.UpdateSource();
        comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
        SaveCurrent();
    }

    private void AutoSaveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (e.OriginalSource is not Slider slider) return;
        slider.GetBindingExpression(System.Windows.Controls.Primitives.RangeBase.ValueProperty)?.UpdateSource();
        SaveCurrent();
    }

    private async void ReloadObsCandidatesButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StreamExpansionObsTextForm card ||
            Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var controller = await mainWindow.EnsureObsConnectedAsync(card.IsSubObs);
        if (controller is null) return;

        try
        {
            var selectedScene = card.SceneName;
            card.SceneNames.Clear();
            foreach (var name in await Task.Run(controller.GetSceneNames)) card.SceneNames.Add(name);

            if (!string.IsNullOrWhiteSpace(selectedScene))
            {
                if (!card.SceneNames.Contains(selectedScene)) card.SceneNames.Add(selectedScene);
                card.SceneName = selectedScene;
            }
            else
            {
                card.SceneName = card.SceneNames.FirstOrDefault() ?? string.Empty;
            }

            await ReloadSourceNamesAsync(card, controller);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OBS候補を取得できませんでした。\n{ex.GetBaseException().Message}", "OBS連携");
        }
    }

    private async void ObsTargetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            !comboBox.IsKeyboardFocusWithin ||
            comboBox.DataContext is not StreamExpansionObsTextForm card ||
            Application.Current.MainWindow is not MainWindow mainWindow) return;

        card.IsSubObs = comboBox.SelectedIndex == 1;
        var controller = await mainWindow.EnsureObsConnectedAsync(card.IsSubObs);
        if (controller is null) return;
        try
        {
            var selectedScene = card.SceneName;
            card.SceneNames.Clear();
            foreach (var name in await Task.Run(controller.GetSceneNames)) card.SceneNames.Add(name);
            if (!string.IsNullOrWhiteSpace(selectedScene))
            {
                if (!card.SceneNames.Contains(selectedScene)) card.SceneNames.Add(selectedScene);
                card.SceneName = selectedScene;
            }
            else
            {
                card.SceneName = card.SceneNames.FirstOrDefault() ?? string.Empty;
            }
            await ReloadSourceNamesAsync(card, controller);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OBS候補を取得できませんでした。\n{ex.GetBaseException().Message}", "OBS連携");
        }
    }

    private async void ObsSceneSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            !comboBox.IsKeyboardFocusWithin ||
            comboBox.DataContext is not StreamExpansionObsTextForm card ||
            Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var selectedScene = comboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedScene))
            selectedScene = comboBox.Text;
        if (string.IsNullOrWhiteSpace(selectedScene)) return;

        card.SceneName = selectedScene;
        var controller = await mainWindow.EnsureObsConnectedAsync(card.IsSubObs);
        if (controller is not null) await ReloadSourceNamesAsync(card, controller);
    }

    private static async Task ReloadSourceNamesAsync(StreamExpansionObsTextForm card, Utility.ObsController controller)
    {
        if (string.IsNullOrWhiteSpace(card.SceneName))
        {
            card.SourceNames.Clear();
            return;
        }

        var selectedSource = card.SourceName;
        var names = await Task.Run(() => controller.GetTextSourceNames(card.SceneName));
        card.SourceNames.Clear();
        foreach (var name in names) card.SourceNames.Add(name);
        if (!string.IsNullOrWhiteSpace(selectedSource) && !card.SourceNames.Contains(selectedSource))
            card.SourceNames.Add(selectedSource);
        card.SourceName = selectedSource;
    }

    private static void AddSaveItem(
        List<T_StreamExpansionItem> saveItems,
        bool isSelected,
        string actionType,
        string content,
        int weight,
        int volume,
        int groupIndex)
    {
        if (!isSelected || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        saveItems.Add(new T_StreamExpansionItem
        {
            ActionType = actionType,
            Content = content.Trim(),
            Weight = weight,
            Volume = volume,
            SortNumber = groupIndex,
            CreatedDateTime = DateTime.Now,
            UpdatedDateTime = DateTime.Now,
            LastUsedDateTime = DateTime.Now
        });
    }


    /// <summary>
    /// 
    /// </summary>
    private void DeleteHeaderButton_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as Button)?.DataContext as StreamExpansionHeaderForm ?? SelectedHeader;
        if (target is null) return;

        if (target.HeaderId != 0)
        {
            DAO_StreamExpansion.Delete(target.HeaderId);
        }
        
        Reload(); 
        //StatusText.Text = "削除した";
    }
}
