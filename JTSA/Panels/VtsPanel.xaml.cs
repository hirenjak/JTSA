using JTSA.Dao;
using JTSA.Utility;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JTSA.Panels;

public partial class VtsPanel : UserControl
{
    private bool isRestoringRules;
    private readonly ObservableCollection<VtsTriggerRuleForm> triggerRules = [];

    public ObservableCollection<VtsNamedOption> TriggerTypes { get; } = [];
    public ObservableCollection<VtsNamedOption> CommandTypes { get; } = [];
    public ObservableCollection<VtsNamedOption> ChannelPoints { get; } = [];
    public ObservableCollection<VtsNamedOption> ScheduledTimes { get; } = [];
    public ObservableCollection<VtsNamedOption> AdUpcomingMinutes { get; } = [];
    public ObservableCollection<VtsNamedOption> ObsTargets { get; } = [];
    public ObservableCollection<VtsNamedOption> CatalogModels { get; } = [];
    public ObservableCollection<VtsNamedOption> CatalogHotkeys { get; } = [];
    public ObservableCollection<VtsNamedOption> CatalogExpressions { get; } = [];
    public ObservableCollection<VtsNamedOption> CatalogItems { get; } = [];
    public ObservableCollection<VtsNamedOption> CatalogArtMeshes { get; } = [];
    public ObservableCollection<VtsNamedOption> CatalogPostProcessing { get; } = [];

    public VtsPanel()
    {
        InitializeComponent();
        RawJsonTextBox.Text =
            """
            {
              "messageType": "StatisticsRequest"
            }
            """;
        FillStaticOptions();
        TriggerRulesItemsControl.ItemsSource = triggerRules;
    }

    private void FillStaticOptions()
    {
        foreach (var option in new[]
        {
            new VtsNamedOption { Id = VtsTriggerTypes.ChannelPoint, Name = "チャンネルポイント" },
            new VtsNamedOption { Id = VtsTriggerTypes.Chat, Name = "チャット" },
            new VtsNamedOption { Id = VtsTriggerTypes.FirstChat, Name = "初コメント" },
            new VtsNamedOption { Id = VtsTriggerTypes.Follow, Name = "フォロー" },
            new VtsNamedOption { Id = VtsTriggerTypes.Raid, Name = "レイド" },
            new VtsNamedOption { Id = VtsTriggerTypes.Subscribe, Name = "サブスク" },
            new VtsNamedOption { Id = VtsTriggerTypes.Bits, Name = "Bits" },
            new VtsNamedOption { Id = VtsTriggerTypes.Hourly, Name = "毎時" },
            new VtsNamedOption { Id = VtsTriggerTypes.ScheduledTime, Name = "指定時刻" },
            new VtsNamedOption { Id = VtsTriggerTypes.AdStart, Name = "CM開始" },
            new VtsNamedOption { Id = VtsTriggerTypes.AdEnd, Name = "CM終了" },
            new VtsNamedOption { Id = VtsTriggerTypes.AdUpcoming, Name = "CM予告" },
            new VtsNamedOption { Id = VtsTriggerTypes.ObsStreamStart, Name = "OBS配信開始" },
        })
            TriggerTypes.Add(option);

        foreach (var option in new[]
        {
            new VtsNamedOption { Id = VtsTriggerCommands.LoadModel, Name = "モデル変更" },
            new VtsNamedOption { Id = VtsTriggerCommands.TriggerHotkey, Name = "ホットキー" },
            new VtsNamedOption { Id = VtsTriggerCommands.ExpressionOn, Name = "表情ON" },
            new VtsNamedOption { Id = VtsTriggerCommands.ExpressionOff, Name = "表情OFF" },
            new VtsNamedOption { Id = VtsTriggerCommands.LoadItem, Name = "アイテム読込" },
            new VtsNamedOption { Id = VtsTriggerCommands.UnloadItem, Name = "アイテム削除" },
            new VtsNamedOption { Id = VtsTriggerCommands.MoveModel, Name = "モデル移動" },
            new VtsNamedOption { Id = VtsTriggerCommands.Tint, Name = "色調" },
            new VtsNamedOption { Id = VtsTriggerCommands.PostProcessing, Name = "後処理" },
            new VtsNamedOption { Id = VtsTriggerCommands.RawJson, Name = "任意JSON" },
        })
            CommandTypes.Add(option);

        foreach (var (id, name) in VtsTriggerService.ScheduledTimes())
            ScheduledTimes.Add(new VtsNamedOption { Id = id, Name = name });

        for (var minute = 1; minute <= 15; minute++)
            AdUpcomingMinutes.Add(new VtsNamedOption { Id = minute.ToString(), Name = $"{minute}分前" });

        ObsTargets.Add(new VtsNamedOption { Id = "main", Name = "メインOBS" });
        ObsTargets.Add(new VtsNamedOption { Id = "sub", Name = "サブOBS" });
    }

    private void VtsPanel_Loaded(object sender, RoutedEventArgs e)
    {
        ReloadSettings();
        AttachClient();
        RefreshConnectionUi();
        ReloadTriggerRules();
        ReloadChannelPoints();
    }

    public void ReloadSettings()
    {
        VtsUrlTextBox.Text = DAO_Setting.SelectOneById(DAO_Setting.SettingName.VtsWebSocketUrl)?.Value
            ?? VtsProtocol.DefaultWebSocketUrl;
        VtsAutoConnectCheckBox.IsChecked =
            DAO_Setting.SelectOneById(DAO_Setting.SettingName.VtsAutoConnect)?.Value == "1";
    }

    public void RefreshConnectionUi()
    {
        AttachClient();
        var client = ClientOrNull();
        if (client is null)
        {
            SetStatus("未接続", "#FF9A9A9A");
            VtsVersionTextBlock.Text = "";
            return;
        }

        if (client.IsAuthenticated)
        {
            SetStatus("認証済み", "#FF8FE3A1");
            VtsVersionTextBlock.Text = string.IsNullOrEmpty(client.VtsVersion)
                ? ""
                : $"VTube Studio {client.VtsVersion}";
            _ = RefreshTriggerCatalogAsync(showError: false);
        }
        else if (client.IsConnected)
        {
            SetStatus("接続中（未認証）", "#FFE0C36A");
            VtsVersionTextBlock.Text = client.LastError ?? "";
        }
        else
        {
            SetStatus(string.IsNullOrEmpty(client.LastError) ? "未接続" : "失敗",
                string.IsNullOrEmpty(client.LastError) ? "#FF9A9A9A" : "#FFFF9A9F");
            VtsVersionTextBlock.Text = client.LastError ?? "";
        }
    }

    private async void SaveAndConnectButton_Click(object sender, RoutedEventArgs e)
    {
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.VtsWebSocketUrl, VtsUrlTextBox.Text.Trim());
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.VtsAutoConnect,
            VtsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
        var window = Main();
        if (window is null) return;
        try
        {
            SetStatus("接続中", "#FFE0C36A");
            await window.ConnectVtsAsync(forceReconnect: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "VTube Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (VtsAutoConnectCheckBox.IsChecked == true)
                window.StartVtsAutoConnectLoop();
        }
        RefreshConnectionUi();
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        var window = Main();
        if (window is null) return;
        window.StopVtsAutoConnectLoop();
        await window.DisconnectVtsAsync();
        RefreshConnectionUi();
    }

    private void VtsAutoConnectCheckBox_Click(object sender, RoutedEventArgs e)
    {
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.VtsAutoConnect,
            VtsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
        var window = Main();
        if (window is null) return;
        if (VtsAutoConnectCheckBox.IsChecked == true)
            window.StartVtsAutoConnectLoop();
        else
            window.StopVtsAutoConnectLoop();
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var current = await client.GetCurrentModelAsync();
            var currentName = current["data"]?.Value<string>("modelName") ?? "(なし)";
            CurrentModelTextBlock.Text = $"現在: {currentName}";

            var list = await client.GetAvailableModelsAsync();
            ModelsListBox.ItemsSource = (list["data"]?["availableModels"] as JArray)?
                .Select(item => new NamedId(
                    item.Value<string>("modelName") ?? "",
                    item.Value<string>("modelID") ?? ""))
                .ToList();
        });

    private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            if (ModelsListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("読み込むモデルを選択してください。");
            await client.LoadModelAsync(item.Id);
            CurrentModelTextBlock.Text = $"現在: {item.Name}";
        });

    private async void MoveModelButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(client => client.MoveModelAsync(
            ParseDouble(MoveTimeTextBox.Text, 0.5),
            MoveRelativeCheckBox.IsChecked == true,
            ParseDouble(MoveXTextBox.Text, 0),
            ParseDouble(MoveYTextBox.Text, 0),
            ParseDouble(MoveRotationTextBox.Text, 0),
            ParseDouble(MoveSizeTextBox.Text, 0)));

    private async void RefreshHotkeysButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var response = await client.GetHotkeysAsync();
            HotkeysListBox.ItemsSource = (response["data"]?["availableHotkeys"] as JArray)?
                .Select(item => new NamedId(
                    item.Value<string>("name") ?? item.Value<string>("hotkeyID") ?? "",
                    item.Value<string>("hotkeyID") ?? ""))
                .ToList();
        });

    private async void TriggerHotkeyButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            if (HotkeysListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("実行するホットキーを選択してください。");
            await client.TriggerHotkeyAsync(item.Id);
        });

    private async void RefreshExpressionsButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var response = await client.GetExpressionsAsync();
            ExpressionsListBox.ItemsSource = (response["data"]?["expressions"] as JArray)?
                .Select(item =>
                {
                    var file = item.Value<string>("file") ?? "";
                    var active = item.Value<bool?>("active") == true ? "ON" : "OFF";
                    var name = item.Value<string>("name") ?? file;
                    return new NamedId($"{name} ({active})", file);
                })
                .ToList();
        });

    private async void ActivateExpressionButton_Click(object sender, RoutedEventArgs e)
        => await SetExpressionAsync(true);

    private async void DeactivateExpressionButton_Click(object sender, RoutedEventArgs e)
        => await SetExpressionAsync(false);

    private async Task SetExpressionAsync(bool active)
        => await RunAsync(async client =>
        {
            if (ExpressionsListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("表情を選択してください。");
            await client.SetExpressionAsync(item.Id, active);
        });

    private async void RefreshItemsButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var response = await client.GetItemsAsync(false, true, true);
            AvailableItemsListBox.ItemsSource = (response["data"]?["availableItemFiles"] as JArray)?
                .Select(item => new NamedId(
                    item.Value<string>("fileName") ?? "",
                    item.Value<string>("fileName") ?? ""))
                .ToList();
            SceneItemsListBox.ItemsSource = (response["data"]?["itemInstancesInScene"] as JArray)?
                .Select(item => new NamedId(
                    $"{item.Value<string>("fileName")} ({item.Value<string>("instanceID")})",
                    item.Value<string>("instanceID") ?? ""))
                .ToList();
        });

    private async void LoadItemButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            if (AvailableItemsListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("読み込むアイテムを選択してください。");
            await client.LoadItemAsync(item.Id);
        });

    private async void UnloadItemButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            if (SceneItemsListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("削除するインスタンスを選択してください。");
            await client.UnloadItemAsync(item.Id);
        });

    private async void MoveItemButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            if (SceneItemsListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("移動するインスタンスを選択してください。");
            await client.MoveItemAsync(
                item.Id,
                ParseDouble(ItemMoveTimeTextBox.Text, 0.5),
                ParseDouble(ItemMoveXTextBox.Text, 0),
                ParseDouble(ItemMoveYTextBox.Text, 0),
                ParseDouble(ItemMoveRotationTextBox.Text, 0),
                ParseDouble(ItemMoveSizeTextBox.Text, 0.32));
        });

    private async void ControlItemAnimationButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            if (SceneItemsListBox.SelectedItem is not NamedId item)
                throw new InvalidOperationException("操作するインスタンスを選択してください。");
            var frameRate = (int)ParseDouble(ItemFrameRateTextBox.Text, 15);
            await client.ControlItemAnimationAsync(item.Id, frameRate, play: true);
        });

    private async void RefreshArtMeshesButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var response = await client.GetArtMeshesAsync();
            ArtMeshesListBox.ItemsSource = (response["data"]?["artMeshNames"] as JArray)?
                .Select(item => item.Value<string>() ?? "")
                .Where(name => name.Length > 0)
                .ToList();
        });

    private async void TintSelectedButton_Click(object sender, RoutedEventArgs e)
        => await ApplyTintAsync(all: false);

    private async void TintAllButton_Click(object sender, RoutedEventArgs e)
        => await ApplyTintAsync(all: true);

    private async void TintResetButton_Click(object sender, RoutedEventArgs e)
    {
        TintRTextBox.Text = "255";
        TintGTextBox.Text = "255";
        TintBTextBox.Text = "255";
        TintATextBox.Text = "255";
        await ApplyTintAsync(all: true);
    }

    private async Task ApplyTintAsync(bool all)
        => await RunAsync(async client =>
        {
            var names = ArtMeshesListBox.SelectedItems.Cast<string>().ToArray();
            if (!all && names.Length == 0)
                throw new InvalidOperationException("ArtMesh を選択するか、全体に適用してください。");
            await client.TintArtMeshesAsync(
                ParseByte(TintRTextBox.Text),
                ParseByte(TintGTextBox.Text),
                ParseByte(TintBTextBox.Text),
                ParseByte(TintATextBox.Text),
                names,
                colorTintAll: all);
        });

    private async void RefreshPostProcessingButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var response = await client.GetPostProcessingAsync();
            PostProcessingOnCheckBox.IsChecked = response["data"]?.Value<bool?>("postProcessingActive") == true;
            PostProcessingListBox.ItemsSource = (response["data"]?["postProcessingEffects"] as JArray)?
                .SelectMany(effect =>
                {
                    var effectName = effect.Value<string>("enumID") ?? "";
                    if (effect["configEntries"] is JArray configs && configs.Count > 0)
                    {
                        return configs.Select(cfg => new NamedId(
                            $"{effectName} / {cfg.Value<string>("configID")}",
                            cfg.Value<string>("configID") ?? ""));
                    }

                    return
                    [
                        new NamedId(effectName, effect.Value<string>("internalID") ?? "")
                    ];
                })
                .ToList();
        });

    private async void ApplyPostProcessingButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            object[]? values = null;
            if (PostProcessingListBox.SelectedItem is NamedId item && !string.IsNullOrWhiteSpace(item.Id))
            {
                values =
                [
                    new
                    {
                        configID = item.Id,
                        configValue = ParseDouble(PostProcessingValueTextBox.Text, 1)
                    }
                ];
            }

            await client.UpdatePostProcessingAsync(
                setPostProcessingPreset: false,
                postProcessingOn: PostProcessingOnCheckBox.IsChecked == true,
                config: values);
        });

    private async void SendRawJsonButton_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async client =>
        {
            var response = await client.SendRawJsonAsync(RawJsonTextBox.Text);
            AppendLog(response.ToString(Newtonsoft.Json.Formatting.Indented));
        });

    private void AttachClient()
    {
        var client = ClientOrNull();
        if (client is null) return;
        client.StateChanged -= OnClientStateChanged;
        client.StateChanged += OnClientStateChanged;
        client.MessageLogged -= OnClientMessageLogged;
        client.MessageLogged += OnClientMessageLogged;
    }

    private void OnClientStateChanged()
        => Dispatcher.BeginInvoke(RefreshConnectionUi);

    private void OnClientMessageLogged(string message)
        => Dispatcher.BeginInvoke(() => AppendLog(message));

    private void AppendLog(string message)
    {
        if (RawLogTextBox.Text.Length > 80_000)
            RawLogTextBox.Text = RawLogTextBox.Text[^40_000..];
        RawLogTextBox.AppendText(message + Environment.NewLine);
        RawLogTextBox.ScrollToEnd();
    }

    private async Task RunAsync(Func<VtsClient, Task> action)
    {
        var client = ClientOrNull();
        if (client is null || !client.IsAuthenticated)
        {
            MessageBox.Show("先に VTube Studio へ接続してください。", "VTube Studio",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await action(client);
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message);
            MessageBox.Show(ex.Message, "VTube Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private VtsClient? ClientOrNull() => Main()?.VtsClient;

    private static MainWindow? Main() => Application.Current.MainWindow as MainWindow;

    private void SetStatus(string text, string color)
    {
        VtsConnectionStatusTextBlock.Text = text;
        VtsStatusIndicator.Fill = (Brush)new BrushConverter().ConvertFromString(color)!;
    }

    private static double ParseDouble(string text, double fallback)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static int ParseByte(string text)
        => Math.Clamp((int)ParseDouble(text, 255), 0, 255);

    private void VtsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not TabControl)
            return;
        if (TriggerTab?.IsSelected == true)
        {
            ReloadChannelPoints();
            _ = RefreshTriggerCatalogAsync(showError: false);
        }
    }

    private void AddTriggerRuleButton_Click(object sender, RoutedEventArgs e)
    {
        triggerRules.Add(CreateForm(new VtsTriggerRule()));
        SaveTriggerRules();
    }

    private void RemoveTriggerRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VtsTriggerRuleForm form) return;
        triggerRules.Remove(form);
        SaveTriggerRules();
    }

    private void TriggerRuleField_Changed(object sender, RoutedEventArgs e)
    {
        if (!isRestoringRules)
            SaveTriggerRules();
    }

    private async void RefreshTriggerCatalogButton_Click(object sender, RoutedEventArgs e)
    {
        ReloadChannelPoints();
        await RefreshTriggerCatalogAsync(showError: true);
    }

    private void ReloadTriggerRules()
    {
        isRestoringRules = true;
        triggerRules.Clear();
        foreach (var rule in VtsTriggerStore.Load())
            triggerRules.Add(CreateForm(rule));
        isRestoringRules = false;
        EnsureSavedIdsInCatalogs();
    }

    private VtsTriggerRuleForm CreateForm(VtsTriggerRule rule)
    {
        var form = new VtsTriggerRuleForm(rule);
        form.Changed += () =>
        {
            if (!isRestoringRules)
                SaveTriggerRules();
        };
        return form;
    }

    private void SaveTriggerRules()
        => VtsTriggerStore.Save(triggerRules.Select(form => form.Rule));

    private void ReloadChannelPoints()
    {
        var preserved = SnapshotSelections();
        isRestoringRules = true;
        VtsNamedOptionCatalog.ReplaceKeeping(ChannelPoints,
            DAO_ChannelPoint.SelectAll().Select(reward => new VtsNamedOption
            {
                Id = reward.RewardId,
                Name = $"{reward.Title} ({reward.Cost})"
            }),
            preserved.Select(item => item.TriggerValue));
        RestoreSelections(preserved);
        isRestoringRules = false;
    }

    private async Task RefreshTriggerCatalogAsync(bool showError)
    {
        var client = ClientOrNull();
        if (client is null || !client.IsAuthenticated)
        {
            if (showError)
            {
                MessageBox.Show("先に VTube Studio へ接続してください。", "VTube Studio",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            EnsureSavedIdsInCatalogs();
            return;
        }

        var preserved = SnapshotSelections();
        try
        {
            isRestoringRules = true;
            var models = await client.GetAvailableModelsAsync();
            VtsNamedOptionCatalog.ReplaceKeeping(CatalogModels, (models["data"]?["availableModels"] as JArray)?
                .Select(item => new VtsNamedOption
                {
                    Id = item.Value<string>("modelID") ?? "",
                    Name = item.Value<string>("modelName") ?? ""
                }),
                preserved.Select(item => item.CommandValue));

            var hotkeys = await client.GetHotkeysAsync();
            VtsNamedOptionCatalog.ReplaceKeeping(CatalogHotkeys, (hotkeys["data"]?["availableHotkeys"] as JArray)?
                .Select(item => new VtsNamedOption
                {
                    Id = item.Value<string>("hotkeyID") ?? "",
                    Name = item.Value<string>("name") ?? item.Value<string>("hotkeyID") ?? ""
                }),
                preserved.Select(item => item.CommandValue));

            var expressions = await client.GetExpressionsAsync();
            VtsNamedOptionCatalog.ReplaceKeeping(CatalogExpressions, (expressions["data"]?["expressions"] as JArray)?
                .Select(item => new VtsNamedOption
                {
                    Id = item.Value<string>("file") ?? "",
                    Name = item.Value<string>("name") ?? item.Value<string>("file") ?? ""
                }),
                preserved.Select(item => item.CommandValue));

            var items = await client.GetItemsAsync(false, false, true);
            VtsNamedOptionCatalog.ReplaceKeeping(CatalogItems, (items["data"]?["availableItemFiles"] as JArray)?
                .Select(item => new VtsNamedOption
                {
                    Id = item.Value<string>("fileName") ?? "",
                    Name = item.Value<string>("fileName") ?? ""
                }),
                preserved.Select(item => item.CommandValue));

            var meshes = await client.GetArtMeshesAsync();
            VtsNamedOptionCatalog.ReplaceKeeping(CatalogArtMeshes, (meshes["data"]?["artMeshNames"] as JArray)?
                .Select(item => new VtsNamedOption
                {
                    Id = item.Value<string>() ?? "",
                    Name = item.Value<string>() ?? ""
                }),
                preserved.Select(item => item.CommandValue));

            var post = await client.GetPostProcessingAsync();
            VtsNamedOptionCatalog.ReplaceKeeping(CatalogPostProcessing, (post["data"]?["postProcessingEffects"] as JArray)?
                .SelectMany(effect =>
                {
                    var effectName = effect.Value<string>("enumID") ?? "";
                    if (effect["configEntries"] is JArray configs && configs.Count > 0)
                    {
                        return configs.Select(cfg => new VtsNamedOption
                        {
                            Id = cfg.Value<string>("configID") ?? "",
                            Name = $"{effectName} / {cfg.Value<string>("configID")}"
                        });
                    }

                    return
                    [
                        new VtsNamedOption
                        {
                            Id = effect.Value<string>("internalID") ?? "",
                            Name = effectName
                        }
                    ];
                }),
                preserved.Select(item => item.CommandValue));
            RestoreSelections(preserved);
            isRestoringRules = false;
            EnsureSavedIdsInCatalogs();
        }
        catch (Exception ex)
        {
            RestoreSelections(preserved);
            isRestoringRules = false;
            AppendLog(ex.Message);
            if (showError)
                MessageBox.Show(ex.Message, "VTube Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private List<(string Id, string TriggerValue, string CommandValue)> SnapshotSelections()
        => triggerRules.Select(form => (form.Rule.Id, form.TriggerValue, form.CommandValue)).ToList();

    private void RestoreSelections(List<(string Id, string TriggerValue, string CommandValue)> preserved)
    {
        foreach (var form in triggerRules)
        {
            var match = preserved.FirstOrDefault(item => item.Id == form.Rule.Id);
            if (match.Id is null) continue;
            form.TriggerValue = match.TriggerValue;
            form.CommandValue = match.CommandValue;
        }
    }

    private void EnsureSavedIdsInCatalogs()
    {
        isRestoringRules = true;
        foreach (var form in triggerRules)
        {
            if (form.ShowChannelPointDetail)
                VtsNamedOptionCatalog.EnsureOption(ChannelPoints, form.TriggerValue);
            switch (form.CommandType)
            {
                case VtsTriggerCommands.LoadModel:
                    VtsNamedOptionCatalog.EnsureOption(CatalogModels, form.CommandValue); break;
                case VtsTriggerCommands.TriggerHotkey:
                    VtsNamedOptionCatalog.EnsureOption(CatalogHotkeys, form.CommandValue); break;
                case VtsTriggerCommands.ExpressionOn:
                case VtsTriggerCommands.ExpressionOff:
                    VtsNamedOptionCatalog.EnsureOption(CatalogExpressions, form.CommandValue); break;
                case VtsTriggerCommands.LoadItem:
                case VtsTriggerCommands.UnloadItem:
                    VtsNamedOptionCatalog.EnsureOption(CatalogItems, form.CommandValue); break;
                case VtsTriggerCommands.Tint:
                    VtsNamedOptionCatalog.EnsureOption(CatalogArtMeshes, form.CommandValue); break;
                case VtsTriggerCommands.PostProcessing:
                    VtsNamedOptionCatalog.EnsureOption(CatalogPostProcessing, form.CommandValue); break;
            }
        }
        isRestoringRules = false;
    }

    private sealed record NamedId(string Name, string Id)
    {
        public string Display => Name;
    }
}
