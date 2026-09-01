using JTSA.Dao;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.ComponentModel;
using JTSA.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;

namespace JTSA.Panels;

public partial class ObsSettingPanel : UserControl
{
    private readonly ObservableCollection<ObsTextSourceCard> textSourceCards = [];
    private readonly ObservableCollection<SceneSwitchPreset> sceneSwitchPresets = [];
    private readonly ObservableCollection<SourceSwitchPreset> sourceSwitchPresets = [];
    private readonly ObservableCollection<CaptureSourceRegistration> captureSourceRegistrations = [];
    private CaptureSourceRegistration? selectedCaptureSourceRegistration;
    private string selectedCaptureCategoryId = string.Empty;
    private string restoreCaptureDestinationValue = string.Empty;
    private string pendingCaptureDestinationValue = string.Empty;
    private readonly TaskCompletionSource<bool> panelLoaded =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool isRestoringCards;
    private bool cardsLoaded;
    private bool isLoadingScenes;
    private Point scenePresetDragStart;
    private SceneSwitchPreset? draggedScenePreset;
    private Point sourcePresetDragStart;
    private SourceSwitchPreset? draggedSourcePreset;
    private bool isLoadingSources;
    private bool isLoadingCaptureSources;
    private bool isLoadingCaptureDestinations;
    private bool captureCategoryHasRule;

    public ObsSettingPanel()
    {
        InitializeComponent();
        TextSourceCardsItemsControl.ItemsSource = textSourceCards;
        var accounts = DAO_TwitchAccount.SelectAll();
        MainTwitchAccountComboBox.ItemsSource = accounts;
        SubTwitchAccountComboBox.ItemsSource = accounts;
        ReloadSettings();
        RestoreTextSourceCards();
        RestoreSceneSwitchPresets();
        RestoreSourceSwitchPresets();
        MigrateLegacyCaptureSettings();
        RestoreCaptureSourceRegistrations();
        RestoreSelectedCaptureSource();
    }

    public void ShowSceneSwitchSettings()
    {
        var accountId = ((MainWindow)Application.Current.MainWindow).SelectedTargetAccountId;
        RefreshSceneSwitchPresetFilter(accountId);
        ShowStandaloneSwitchSettings(SceneSwitchTab);
    }

    public void ShowSourceSwitchSettings()
    {
        var accountId = ((MainWindow)Application.Current.MainWindow).SelectedTargetAccountId;
        RefreshSourceSwitchPresetFilter(accountId);
        ShowStandaloneSwitchSettings(SourceSwitchTab);
        _ = RefreshSourceVisibilityStatesAsync(accountId);
    }

    public void ShowCaptureDestinationSettings(string categoryId, string categoryName, string boxArtUrl)
    {
        selectedCaptureCategoryId = categoryId;
        var rule = LoadCategoryCaptureRules().FirstOrDefault(rule => rule.CategoryId == categoryId);
        captureCategoryHasRule = rule is not null;
        if (rule is not null)
        {
            NoCaptureSourceRadioButton.IsChecked = false;
            selectedCaptureSourceRegistration = new CaptureSourceRegistration
            {
                IsSub = rule.IsSub,
                InputName = rule.InputName
            };
            restoreCaptureDestinationValue = rule.DestinationValue;
            pendingCaptureDestinationValue = rule.DestinationValue;
            SelectedCaptureSourceTextBlock.Text = rule.InputName;
            SelectedCaptureDestinationTextBlock.Text = string.IsNullOrWhiteSpace(rule.DestinationValue)
                ? "キャプチャ先：未設定"
                : $"キャプチャ先：{rule.DestinationValue}";
        }
        else
        {
            NoCaptureSourceRadioButton.IsChecked = true;
            pendingCaptureDestinationValue = string.Empty;
            SelectedCaptureSourceTextBlock.Text = "キャプチャソース：未設定";
            SelectedCaptureTypeTextBlock.Text = string.Empty;
            SelectedCaptureDestinationTextBlock.Text = "キャプチャ先：未設定";
        }
        ShowStandaloneSwitchSettings(CaptureDestinationTab);
        ShowSelectedCaptureCategory(categoryId, categoryName, boxArtUrl);
        _ = RefreshCaptureSourcesAsync();
    }

    /// <summary>別ウィンドウで保存されたOBSショートカット設定を再読み込みする。</summary>
    public void ReloadSwitchPresets()
    {
        sceneSwitchPresets.Clear();
        sourceSwitchPresets.Clear();
        RestoreSceneSwitchPresets();
        RestoreSourceSwitchPresets();

        var accountId = ((MainWindow)Application.Current.MainWindow).SelectedTargetAccountId;
        RefreshSceneSwitchPresetFilter(accountId);
        RefreshSourceSwitchPresetFilter(accountId);
    }

    private void ShowStandaloneSwitchSettings(TabItem targetTab)
    {
        var targetContent = targetTab.Content;
        targetTab.Content = null;
        StandaloneSwitchContentHost.Content = targetContent;
        ObsSettingsTabControl.Visibility = Visibility.Collapsed;
        StandaloneSwitchContentHost.Visibility = Visibility.Visible;
    }

    public void ReloadSettings()
    {
        ObsUrlTextBox.Text = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketUrl)?.Value
            ?? MainWindow.DefaultObsWebSocketUrl;
        ObsPasswordBox.Password = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsWebSocketPassword)?.Value ?? "";
        var legacyAutoConnect = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsAutoConnect)?.Value;
        MainObsAutoConnectCheckBox.IsChecked =
            (DAO_Setting.SelectOneById(DAO_Setting.SettingName.MainObsAutoConnect)?.Value ?? legacyAutoConnect) == "1";
        SubObsAutoConnectCheckBox.IsChecked =
            (DAO_Setting.SelectOneById(DAO_Setting.SettingName.SubObsAutoConnect)?.Value ?? legacyAutoConnect) == "1";
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

    public void MoveConnectionSettingsTo(Panel host)
    {
        if (ReferenceEquals(ObsConnectionSettingsSection.Parent, host)) return;
        if (ObsConnectionSettingsSection.Parent is Panel currentParent)
            currentParent.Children.Remove(ObsConnectionSettingsSection);
        host.Children.Add(ObsConnectionSettingsSection);
        ObsSettingsTab.Visibility = Visibility.Collapsed;
    }

    private void TipsLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private async void SceneSwitchTab_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (SceneSwitchTab.IsSelected && SceneSelectionComboBox.Items.Count == 0)
            await RefreshSceneSwitchListAsync();
    }

    private async void RefreshScenesButton_Click(object sender, RoutedEventArgs e)
        => await RefreshSceneSwitchListAsync();

    private async void SourceSwitchTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (SourceSwitchTab.IsSelected && SourceSceneSelectionComboBox.Items.Count == 0)
            await RefreshSourceSwitchListAsync();
    }

    private async void RefreshSourcesButton_Click(object sender, RoutedEventArgs e)
        => await RefreshSourceSwitchListAsync();

    private async void CaptureDestinationTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (CaptureDestinationTab.IsSelected && CaptureSourcesListBox.Items.Count == 0)
            await RefreshCaptureSourcesAsync();
    }

    private async void RefreshCaptureSourcesButton_Click(object sender, RoutedEventArgs e)
        => await RefreshCaptureSourcesAsync();

    private async Task RefreshCaptureSourcesAsync()
    {
        if (isLoadingCaptureSources) return;
        isLoadingCaptureSources = true;
        try
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var sources = new List<CaptureSourceChoice>();
            var connectedObsCount = 0;
            foreach (var isSub in new[] { false, true })
            {
                var controller = mainWindow.GetConnectedObsController(isSub);
                if (controller is null) continue;
                connectedObsCount++;
                var inputs = await Task.Run(controller.GetCaptureSources);
                sources.AddRange(inputs
                    .Where(input => captureSourceRegistrations.Any(registration =>
                        registration.IsSub == isSub &&
                        string.Equals(registration.InputName, input.InputName, StringComparison.OrdinalIgnoreCase)))
                    .Select(input => new CaptureSourceChoice(isSub, input)));
            }
            CaptureSourcesListBox.ItemsSource = sources;
            var restoredSource = sources.FirstOrDefault(source =>
                selectedCaptureSourceRegistration is not null &&
                source.IsSub == selectedCaptureSourceRegistration.IsSub &&
                string.Equals(source.Source.InputName, selectedCaptureSourceRegistration.InputName,
                    StringComparison.OrdinalIgnoreCase));
            CaptureSourcesListBox.SelectedItem = captureCategoryHasRule
                ? restoredSource
                : selectedCaptureCategoryId.Length == 0 ? restoredSource ?? sources.FirstOrDefault() : null;
            LogCaptureStatus(connectedObsCount == 0
                ? "接続済みのOBSがありません"
                : sources.Count == 0
                ? "接続済みOBSにキャプチャソースが見つかりませんでした"
                : $"{sources.Count}件のキャプチャソースを読み込みました");
        }
        catch (Exception ex)
        {
            LogCaptureStatus($"読込失敗: {ex.GetBaseException().Message}", isError: true);
        }
        finally
        {
            isLoadingCaptureSources = false;
        }
    }

    private async void RegisterCaptureSourceButton_Click(object sender, RoutedEventArgs e)
    {
        async Task<IReadOnlyList<ObsCaptureSourceSelectionItem>> LoadCandidatesAsync()
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var candidates = new List<ObsCaptureSourceSelectionItem>();
            foreach (var isSub in new[] { false, true })
            {
                var controller = mainWindow.GetConnectedObsController(isSub);
                if (controller is null) continue;
                var inputs = await Task.Run(controller.GetCaptureSources);
                candidates.AddRange(inputs.Select(input => new ObsCaptureSourceSelectionItem(isSub, input)));
            }
            return candidates;
        }

        var window = new ObsCaptureSourceSelectionWindow(LoadCandidatesAsync)
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() != true || window.SelectedSource is null) return;

        var selected = window.SelectedSource;
        if (!captureSourceRegistrations.Any(registration =>
                registration.IsSub == selected.IsSub &&
                string.Equals(registration.InputName, selected.Source.InputName, StringComparison.OrdinalIgnoreCase)))
        {
            captureSourceRegistrations.Add(new CaptureSourceRegistration
            {
                IsSub = selected.IsSub,
                InputName = selected.Source.InputName
            });
            SaveCaptureSourceRegistrations();
        }
        await RefreshCaptureSourcesAsync();
        LogCaptureStatus($"{selected.DisplayName} を登録しました");
    }

    private async void DeleteCaptureSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CaptureSourceChoice choice) return;

        var registration = captureSourceRegistrations.FirstOrDefault(item =>
            item.IsSub == choice.IsSub &&
            string.Equals(item.InputName, choice.Source.InputName, StringComparison.OrdinalIgnoreCase));
        if (registration is null) return;

        captureSourceRegistrations.Remove(registration);
        SaveCaptureSourceRegistrations();
        await RefreshCaptureSourcesAsync();
        LogCaptureStatus($"{choice.Source.InputName} の登録を削除しました");
    }

    private void RestoreCaptureSourceRegistrations()
    {
        captureSourceRegistrations.Clear();
        foreach (var source in DAO_ObsCaptureSetting.SelectSources())
            captureSourceRegistrations.Add(new CaptureSourceRegistration
            {
                IsSub = source.IsSubObs,
                InputName = source.InputName
            });
    }

    private static void MigrateLegacyCaptureSettings()
    {
        if (DAO_ObsCaptureSetting.SelectSources().Count == 0)
        {
            try
            {
                var sourcesJson = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsCaptureSources)?.Value;
                var selectedJson = DAO_Setting.SelectOneById(DAO_Setting.SettingName.SelectedObsCaptureSource)?.Value;
                var legacySources = string.IsNullOrWhiteSpace(sourcesJson)
                    ? []
                    : JsonSerializer.Deserialize<List<CaptureSourceRegistration>>(sourcesJson) ?? [];
                var legacySelected = string.IsNullOrWhiteSpace(selectedJson)
                    ? null
                    : JsonSerializer.Deserialize<CaptureSourceRegistration>(selectedJson);

                if (legacySources.Count > 0)
                    DAO_ObsCaptureSetting.ReplaceSources(legacySources.Select(source => new M_ObsCaptureSource
                    {
                        UpdatedDateTime = DateTime.Now,
                        IsSubObs = source.IsSub,
                        InputName = source.InputName,
                        IsSelected = legacySelected is not null && source.IsSub == legacySelected.IsSub &&
                            string.Equals(source.InputName, legacySelected.InputName,
                                StringComparison.OrdinalIgnoreCase)
                    }));
                DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ObsCaptureSources, string.Empty);
                DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SelectedObsCaptureSource, string.Empty);
            }
            catch
            {
                // 旧設定が壊れている場合は空の新テーブルから開始する。
            }
        }

        if (DAO_ObsCaptureSetting.SelectRules().Count != 0) return;
        try
        {
            var rulesJson = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsCategoryCaptureRules)?.Value;
            var legacyRules = string.IsNullOrWhiteSpace(rulesJson)
                ? []
                : JsonSerializer.Deserialize<List<CategoryCaptureRule>>(rulesJson) ?? [];
            foreach (var rule in legacyRules)
                DAO_ObsCaptureSetting.UpsertRule(new M_ObsCategoryCaptureRule
                {
                    UpdatedDateTime = DateTime.Now,
                    CategoryId = rule.CategoryId,
                    IsSubObs = rule.IsSub,
                    InputName = rule.InputName,
                    DestinationValue = rule.DestinationValue
                });
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ObsCategoryCaptureRules, string.Empty);
        }
        catch
        {
            // 旧設定が壊れている場合は空の新テーブルから開始する。
        }
    }

    private void SaveCaptureSourceRegistrations() => DAO_ObsCaptureSetting.ReplaceSources(
        captureSourceRegistrations.Select(registration => new M_ObsCaptureSource
        {
            UpdatedDateTime = DateTime.Now,
            IsSubObs = registration.IsSub,
            InputName = registration.InputName,
            IsSelected = selectedCaptureSourceRegistration is not null &&
                registration.IsSub == selectedCaptureSourceRegistration.IsSub &&
                string.Equals(registration.InputName, selectedCaptureSourceRegistration.InputName,
                    StringComparison.OrdinalIgnoreCase)
        }));

    private void RestoreSelectedCaptureSource()
    {
        var selected = DAO_ObsCaptureSetting.SelectSources().FirstOrDefault(source => source.IsSelected);
        selectedCaptureSourceRegistration = selected is null
            ? null
            : new CaptureSourceRegistration { IsSub = selected.IsSubObs, InputName = selected.InputName };
    }

    private void SaveSelectedCaptureSource(CaptureSourceChoice choice)
    {
        selectedCaptureSourceRegistration = new CaptureSourceRegistration
        {
            IsSub = choice.IsSub,
            InputName = choice.Source.InputName
        };
        SaveCaptureSourceRegistrations();
    }

    private async void CaptureSourcesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureSourcesListBox.SelectedItem is CaptureSourceChoice)
            NoCaptureSourceRadioButton.IsChecked = false;
        if (!isLoadingCaptureSources)
        {
            pendingCaptureDestinationValue = string.Empty;
            SelectedCaptureDestinationTextBlock.Text = "キャプチャ先：未選択";
        }
        await ReloadCaptureDestinationsAsync();
    }

    private void NoCaptureSourceRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (CaptureSourcesListBox is null || CaptureDestinationListBox is null) return;
        pendingCaptureDestinationValue = string.Empty;
        CaptureSourcesListBox.SelectedIndex = -1;
        CaptureDestinationListBox.ItemsSource = null;
        CaptureDestinationListBox.IsEnabled = false;
        SelectedCaptureSourceTextBlock.Text = "キャプチャソース：未選択";
        SelectedCaptureTypeTextBlock.Text = string.Empty;
        SelectedCaptureDestinationTextBlock.Text = "キャプチャ先：未選択";
    }

    private async void RefreshCaptureDestinationsButton_Click(object sender, RoutedEventArgs e)
    {
        restoreCaptureDestinationValue = CaptureDestinationListBox.SelectedValue as string ?? string.Empty;
        await ReloadCaptureDestinationsAsync();
    }

    private async Task ReloadCaptureDestinationsAsync()
    {
        if (CaptureSourcesListBox.SelectedItem is not CaptureSourceChoice choice) return;
        SelectedCaptureSourceTextBlock.Text = choice.Source.InputName;
        SelectedCaptureTypeTextBlock.Text = $"{choice.ObsDisplayName} / {choice.Source.TypeName}";
        CaptureDestinationListBox.IsEnabled = false;
        isLoadingCaptureDestinations = true;
        try
        {
            var controller = ((MainWindow)Application.Current.MainWindow).GetConnectedObsController(choice.IsSub);
            if (controller is null)
            {
                LogCaptureStatus($"{choice.ObsDisplayName}は未接続です", isError: true);
                return;
            }
            var settings = await Task.Run(() => controller.GetCaptureSettings(choice.Source));
            CaptureDestinationListBox.ItemsSource = settings.Destinations;
            CaptureDestinationListBox.SelectedIndex = -1;
            if (!string.IsNullOrWhiteSpace(restoreCaptureDestinationValue))
            {
                var savedDestination = settings.Destinations.FirstOrDefault(destination =>
                    destination.Value == restoreCaptureDestinationValue);
                SelectedCaptureDestinationTextBlock.Text = savedDestination is null
                    ? "キャプチャ先：保存済みの対象が見つかりません"
                    : $"キャプチャ先：{savedDestination.Name}";
            }
            CaptureDestinationListBox.IsEnabled = settings.Destinations.Count > 0;
            LogCaptureStatus(settings.Destinations.Count == 0
                ? "選択できるキャプチャ先がありません"
                : $"{settings.Destinations.Count}件のキャプチャ先を取得しました");
        }
        catch (Exception ex)
        {
            LogCaptureStatus($"キャプチャ先取得失敗: {ex.GetBaseException().Message}", isError: true);
        }
        finally
        {
            isLoadingCaptureDestinations = false;
            restoreCaptureDestinationValue = string.Empty;
        }
    }

    private void CaptureDestinationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoadingCaptureDestinations) return;
        if (CaptureDestinationListBox.SelectedValue is not string value) return;
        var selectedDestinationName = (CaptureDestinationListBox.SelectedItem as ObsCaptureDestination)?.Name ?? value;
        pendingCaptureDestinationValue = value;
        SelectedCaptureDestinationTextBlock.Text = $"キャプチャ先：{selectedDestinationName}";
    }

    private void ClearCaptureDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        pendingCaptureDestinationValue = string.Empty;
        CaptureDestinationListBox.SelectedIndex = -1;
        SelectedCaptureDestinationTextBlock.Text = "キャプチャ先：未設定";
    }

    private async void ApplyCaptureDestinationAndCloseButton_Click(object sender, RoutedEventArgs e)
    {
        var captureWindow = Window.GetWindow(this) as ObsCaptureDestinationWindow;
        if (NoCaptureSourceRadioButton.IsChecked == true)
        {
            try
            {
                DAO_ObsCaptureSetting.DeleteRule(selectedCaptureCategoryId);
                selectedCaptureSourceRegistration = null;
                SaveCaptureSourceRegistrations();
                await HideAllRegisteredCaptureSourcesAsync();
                captureCategoryHasRule = false;
                LogCaptureStatus("キャプチャソースを未選択にしました");
            }
            catch (Exception ex)
            {
                LogCaptureStatus($"変更失敗: {ex.GetBaseException().Message}", isError: true);
            }
            finally
            {
                captureWindow?.Close();
            }
            return;
        }

        if (CaptureSourcesListBox.SelectedItem is not CaptureSourceChoice choice)
        {
            LogCaptureStatus("キャプチャソースを選択してください", isError: true);
            captureWindow?.Close();
            return;
        }

        try
        {
            var controller = ((MainWindow)Application.Current.MainWindow).GetConnectedObsController(choice.IsSub);
            if (controller is null)
            {
                LogCaptureStatus($"{choice.ObsDisplayName}は未接続です", isError: true);
                return;
            }

            SaveSelectedCaptureSource(choice);
            SaveCategoryCaptureRule(choice, pendingCaptureDestinationValue);
            await ApplyCaptureRuleForCategoryAsync(selectedCaptureCategoryId);
        }
        catch (Exception ex)
        {
            LogCaptureStatus($"変更失敗: {ex.GetBaseException().Message}", isError: true);
        }
        finally
        {
            captureWindow?.Close();
        }
    }

    private async Task HideAllRegisteredCaptureSourcesAsync()
    {
        var mainWindow = (MainWindow)Application.Current.MainWindow;
        foreach (var group in captureSourceRegistrations.GroupBy(registration => registration.IsSub))
        {
            var controller = mainWindow.GetConnectedObsController(group.Key);
            if (controller is null) continue;
            foreach (var registration in group)
                await Task.Run(() => controller.SetInputVisibleAcrossScenes(registration.InputName, false));
        }
    }

    private void SelectCaptureCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new PlaylistCategorySelectionWindow(selectionOnly: true)
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.SelectedCategoryId))
            return;

        var category = DAO_Category.SelectOneById(window.SelectedCategoryId);
        ShowSelectedCaptureCategory(
            window.SelectedCategoryId,
            category?.DisplayName ?? "未選択",
            category?.BoxArtUrl ?? string.Empty);
    }

    private void ShowSelectedCaptureCategory(string categoryId, string categoryName, string boxArtUrl)
    {
        selectedCaptureCategoryId = categoryId;
        SelectedCaptureCategoryTextBlock.Text = categoryName;
        SelectedCaptureCategoryBoxArt.Source = null;
        if (!string.IsNullOrWhiteSpace(boxArtUrl))
        {
            try
            {
                SelectedCaptureCategoryBoxArt.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri(boxArtUrl, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                LogCaptureStatus($"カテゴリBoxArt表示失敗: {ex.GetBaseException().Message}", isError: true);
            }
        }
    }

    private void LogCaptureStatus(string message, bool isError = false)
    {
        var appLog = ((MainWindow)Application.Current.MainWindow).AppLogPanel;
        if (isError)
            appLog.Error(GetType().Name, $"OBSキャプチャ先変更：{message}");
        else
            appLog.Success(GetType().Name, $"OBSキャプチャ先変更：{message}");
    }

    private void SaveCategoryCaptureRule(CaptureSourceChoice choice, string destinationValue)
    {
        if (string.IsNullOrWhiteSpace(selectedCaptureCategoryId))
            return;

        DAO_ObsCaptureSetting.UpsertRule(new M_ObsCategoryCaptureRule
        {
            UpdatedDateTime = DateTime.Now,
            CategoryId = selectedCaptureCategoryId,
            IsSubObs = choice.IsSub,
            InputName = choice.Source.InputName,
            DestinationValue = destinationValue
        });
    }

    public async Task ApplyCaptureRuleForCategoryAsync(string categoryId)
    {
        RestoreCaptureSourceRegistrations();
        var rule = LoadCategoryCaptureRules().FirstOrDefault(rule => rule.CategoryId == categoryId);
        if (rule is null) return;

        var mainWindow = (MainWindow)Application.Current.MainWindow;
        try
        {
            var selectedController = mainWindow.GetConnectedObsController(rule.IsSub);
            if (selectedController is null)
            {
                LogCaptureStatus($"カテゴリルール適用対象の{(rule.IsSub ? "サブ" : "メイン")}OBSは未接続です", true);
                return;
            }

            var selectedSource = (await Task.Run(selectedController.GetCaptureSources)).FirstOrDefault(source =>
                string.Equals(source.InputName, rule.InputName, StringComparison.OrdinalIgnoreCase));
            if (selectedSource is null)
            {
                LogCaptureStatus($"カテゴリルールのソース「{rule.InputName}」が見つかりません", true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(rule.DestinationValue))
                await Task.Run(() => selectedController.SetCaptureDestination(selectedSource, rule.DestinationValue));

            foreach (var group in captureSourceRegistrations.GroupBy(registration => registration.IsSub))
            {
                var controller = mainWindow.GetConnectedObsController(group.Key);
                if (controller is null) continue;
                foreach (var registration in group)
                {
                    var visible = registration.IsSub == rule.IsSub &&
                        string.Equals(registration.InputName, rule.InputName, StringComparison.OrdinalIgnoreCase);
                    await Task.Run(() => controller.SetInputVisibleAcrossScenes(registration.InputName, visible));
                }
            }
            LogCaptureStatus($"カテゴリ「{categoryId}」のキャプチャ設定を適用しました");
        }
        catch (Exception ex)
        {
            LogCaptureStatus($"カテゴリルール適用失敗: {ex.GetBaseException().Message}", true);
        }
    }

    private static List<CategoryCaptureRule> LoadCategoryCaptureRules()
    {
        return DAO_ObsCaptureSetting.SelectRules().Select(rule => new CategoryCaptureRule
        {
            CategoryId = rule.CategoryId,
            IsSub = rule.IsSubObs,
            InputName = rule.InputName,
            DestinationValue = rule.DestinationValue
        }).ToList();
    }

    private sealed record CaptureSourceChoice(bool IsSub, ObsCaptureSource Source)
    {
        public string ObsDisplayName => IsSub ? "サブOBS" : "メインOBS";
        public string DisplayName => $"{ObsDisplayName}｜{Source.InputName}\n{Source.TypeName}";
    }

    private sealed class CaptureSourceRegistration
    {
        public bool IsSub { get; set; }
        public string InputName { get; set; } = string.Empty;
    }

    private sealed class CategoryCaptureRule
    {
        public string CategoryId { get; set; } = string.Empty;
        public bool IsSub { get; set; }
        public string InputName { get; set; } = string.Empty;
        public string DestinationValue { get; set; } = string.Empty;
    }

    private async Task RefreshSourceSwitchListAsync()
    {
        if (isLoadingSources) return;
        isLoadingSources = true;
        SourceSwitchStatusTextBlock.Text = "メイン・サブOBSからソースを読み込んでいます...";
        try
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var choices = new List<SourceSceneChoice>();
            var connectedObsCount = 0;
            foreach (var isSub in new[] { false, true })
            {
                var controller = mainWindow.GetConnectedObsController(isSub);
                if (controller is null) continue;
                connectedObsCount++;
                var scenes = await Task.Run(controller.GetSceneNames);
                choices.AddRange(scenes.Select(scene => new SourceSceneChoice { IsSub = isSub, SceneName = scene }));
            }
            SourceSceneSelectionComboBox.ItemsSource = choices;
            SourceSceneSelectionComboBox.SelectedItem = choices.FirstOrDefault();
            await LoadSourcesForSelectedSceneAsync();
            await RefreshSourceVisibilityStatesAsync(mainWindow.SelectedTargetAccountId, onlyConnected: true);
            SourceSwitchStatusTextBlock.Text = connectedObsCount == 0
                ? "接続済みのOBSがありません"
                : choices.Count == 0
                ? "接続済みOBSにシーンがありませんでした"
                : $"{choices.Count}件のシーンを読み込みました";
        }
        catch (Exception ex)
        {
            SourceSwitchStatusTextBlock.Text = $"読込失敗: {ex.GetBaseException().Message}";
        }
        finally
        {
            isLoadingSources = false;
        }
    }

    private async void SourceSceneSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await LoadSourcesForSelectedSceneAsync();

    private async Task LoadSourcesForSelectedSceneAsync()
    {
        if (SourceSceneSelectionComboBox.SelectedItem is not SourceSceneChoice choice)
        {
            SourceSelectionComboBox.ItemsSource = null;
            return;
        }
        var controller = ((MainWindow)Application.Current.MainWindow).GetConnectedObsController(choice.IsSub);
        if (controller is null) return;
        var sources = await Task.Run(() => controller.GetSceneSources(choice.SceneName));
        SourceSelectionComboBox.ItemsSource = sources;
        SourceSelectionComboBox.SelectedItem = sources.FirstOrDefault();
    }

    private void AddSourceSwitchPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceSceneSelectionComboBox.SelectedItem is not SourceSceneChoice scene ||
            SourceSelectionComboBox.SelectedItem is not ObsSceneSource source) return;
        var mainWindow = (MainWindow)Application.Current.MainWindow;
        var accountId = mainWindow.SelectedTargetAccountId;
        if (accountId is null)
        {
            SourceSwitchStatusTextBlock.Text = "右上の保存先アカウントを選択してください";
            return;
        }
        if (sourceSwitchPresets.Any(x => x.AccountId == accountId && x.IsSub == scene.IsSub &&
            string.Equals(x.SceneName, scene.SceneName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SourceName, source.SourceName, StringComparison.OrdinalIgnoreCase)))
        {
            SourceSwitchStatusTextBlock.Text = "そのソースはすでに登録されています";
            return;
        }
        sourceSwitchPresets.Add(new SourceSwitchPreset
        {
            AccountId = accountId.Value,
            IsSub = scene.IsSub,
            SceneName = scene.SceneName,
            SourceName = source.SourceName,
            IsVisible = source.IsEnabled
        });
        SaveSourceSwitchPresets();
        RefreshSourceSwitchPresetFilter(accountId);
        mainWindow.RefreshObsSourceShortcutButtons();
        SourceSwitchStatusTextBlock.Text = $"{source.SourceName} の表示切替ボタンを追加しました";
    }

    private async void ToggleSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SourceSwitchPreset preset)
            await ToggleSourceAsync(preset);
    }

    private async Task ToggleSourceAsync(SourceSwitchPreset preset)
    {
        SourceSwitchStatusTextBlock.Text = $"{preset.SourceName} を切り替えています...";
        try
        {
            var controller = await ((MainWindow)Application.Current.MainWindow).EnsureObsConnectedAsync(preset.IsSub);
            if (controller is null) throw new InvalidOperationException("OBSに接続できませんでした");
            var current = await Task.Run(() => controller.GetSceneSourceEnabled(preset.SceneName, preset.SourceName));
            await Task.Run(() => controller.SetSceneSourceEnabled(preset.SceneName, preset.SourceName, !current));
            preset.IsVisible = !current;
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            RefreshSourceSwitchPresetFilter(mainWindow.SelectedTargetAccountId);
            mainWindow.RefreshObsSourceShortcutButtons();
            SourceSwitchStatusTextBlock.Text = $"{preset.SourceName} を{(preset.IsVisible ? "表示" : "非表示")}にしました";
        }
        catch (Exception ex)
        {
            SourceSwitchStatusTextBlock.Text = $"切替失敗: {ex.GetBaseException().Message}";
        }
    }

    private void RemoveSourceSwitchPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SourceSwitchPreset preset) return;
        sourceSwitchPresets.Remove(preset);
        SaveSourceSwitchPresets();
        var mainWindow = (MainWindow)Application.Current.MainWindow;
        RefreshSourceSwitchPresetFilter(mainWindow.SelectedTargetAccountId);
        mainWindow.RefreshObsSourceShortcutButtons();
    }

    private async Task RefreshSceneSwitchListAsync()
    {
        if (isLoadingScenes) return;
        isLoadingScenes = true;
        SceneSwitchStatusTextBlock.Text = "接続済みOBSからシーンを読み込んでいます...";
        try
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var targets = new[] { (IsSub: false, Name: "メイン"), (IsSub: true, Name: "サブ") }
                .Select(target => (target, Controller: mainWindow.GetConnectedObsController(target.IsSub)))
                .Where(item => item.Controller is not null)
                .ToList();

            var results = await Task.WhenAll(targets.Select(async item =>
            {
                var target = item.target;
                try
                {
                    var controller = item.Controller!;
                    var scenes = await Task.Run(controller.GetSceneNames);
                    var current = await Task.Run(controller.GetCurrentProgramScene);
                    return (target, Scenes: scenes.ToArray(), Current: current, Error: "");
                }
                catch (Exception ex)
                {
                    return (target, Scenes: Array.Empty<string>(), Current: "",
                        Error: ex.GetBaseException().Message);
                }
            }));

            var choices = results
                .SelectMany(result => result.Scenes.Select(scene => new SceneChoice
                {
                    IsSub = result.target.IsSub,
                    SceneName = scene
                }))
                .ToList();
            SceneSelectionComboBox.ItemsSource = choices;
            SceneSelectionComboBox.SelectedItem = choices.FirstOrDefault();
            CurrentProgramSceneTextBlock.Text = string.Join(" / ", results
                .Where(result => string.IsNullOrEmpty(result.Error))
                .Select(result => $"{result.target.Name}: {result.Current}"));
            foreach (var preset in sceneSwitchPresets)
            {
                var result = results.FirstOrDefault(item => item.target.IsSub == preset.IsSub);
                preset.IsCurrentScene = string.IsNullOrEmpty(result.Error) &&
                    string.Equals(preset.SceneName, result.Current, StringComparison.OrdinalIgnoreCase);
            }
            RefreshSceneSwitchPresetFilter(mainWindow.SelectedTargetAccountId);
            mainWindow.RefreshObsSceneShortcutButtons();

            var errors = results.Where(result => !string.IsNullOrEmpty(result.Error)).ToList();
            SceneSwitchStatusTextBlock.Text = targets.Count == 0
                ? "接続済みのOBSがありません"
                : choices.Count == 0
                ? "接続済みOBSにシーンがありませんでした"
                : errors.Count == 0
                    ? $"接続済みOBSから{choices.Count}件のシーンを読み込みました"
                    : $"{choices.Count}件を読み込みました（{string.Join("、", errors.Select(x => x.target.Name + "OBS取得失敗"))}）";
        }
        catch (Exception ex)
        {
            SceneSwitchStatusTextBlock.Text = $"読込失敗: {ex.GetBaseException().Message}";
        }
        finally
        {
            isLoadingScenes = false;
        }
    }

    private async void SwitchSceneButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SceneSwitchPreset preset) return;
        await SwitchSceneAsync(preset);
    }

    private async Task SwitchSceneAsync(SceneSwitchPreset preset)
    {
        if (isLoadingScenes) return;
        var sceneName = preset.SceneName;
        isLoadingScenes = true;
        SceneSwitchStatusTextBlock.Text = $"{sceneName} へ切り替えています...";
        try
        {
            var isSub = preset.IsSub;
            var controller = await ((MainWindow)Application.Current.MainWindow)
                .EnsureObsConnectedAsync(isSub);
            if (controller is null)
            {
                SceneSwitchStatusTextBlock.Text = "OBSに接続できませんでした";
                return;
            }

            await Task.Run(() => controller.SetCurrentProgramScene(sceneName));
            foreach (var item in sceneSwitchPresets.Where(item => item.IsSub == preset.IsSub))
                item.IsCurrentScene = string.Equals(
                    item.SceneName, sceneName, StringComparison.OrdinalIgnoreCase);
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            RefreshSceneSwitchPresetFilter(mainWindow.SelectedTargetAccountId);
            mainWindow.RefreshObsSceneShortcutButtons();
            CurrentProgramSceneTextBlock.Text = sceneName;
            SceneSwitchStatusTextBlock.Text = $"{sceneName} に切り替えました";
        }
        catch (Exception ex)
        {
            SceneSwitchStatusTextBlock.Text = $"切替失敗: {ex.GetBaseException().Message}";
        }
        finally
        {
            isLoadingScenes = false;
        }
    }

    private void AddSceneSwitchPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (SceneSelectionComboBox.SelectedItem is not SceneChoice choice) return;
        var sceneName = choice.SceneName;
        var mainWindow = (MainWindow)Application.Current.MainWindow;
        var accountId = mainWindow.SelectedTargetAccountId;
        if (accountId is null)
        {
            SceneSwitchStatusTextBlock.Text = "右上の保存先アカウントを選択してください";
            return;
        }
        var isSub = choice.IsSub;
        if (sceneSwitchPresets.Any(x => x.AccountId == accountId.Value &&
            x.IsSub == isSub &&
            string.Equals(x.SceneName, sceneName, StringComparison.OrdinalIgnoreCase)))
        {
            SceneSwitchStatusTextBlock.Text = "そのシーンはすでに登録されています";
            return;
        }

        sceneSwitchPresets.Add(new SceneSwitchPreset
        {
            AccountId = accountId.Value,
            AccountDisplayName = DAO_TwitchAccount.SelectById(accountId.Value)?.UserName ?? $"ID: {accountId.Value}",
            IsSub = isSub,
            SceneName = sceneName
        });
        SaveSceneSwitchPresets();
        RefreshSceneSwitchPresetFilter(accountId.Value);
        mainWindow.RefreshObsSceneShortcutButtons();
        SceneSwitchStatusTextBlock.Text = $"右上の選択アカウントに {sceneName} の切替ボタンを追加しました";
    }

    private void RemoveSceneSwitchPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SceneSwitchPreset preset) return;
        sceneSwitchPresets.Remove(preset);
        SaveSceneSwitchPresets();
        var mainWindow = (MainWindow)Application.Current.MainWindow;
        RefreshSceneSwitchPresetFilter(mainWindow.SelectedTargetAccountId);
        mainWindow.RefreshObsSceneShortcutButtons();
    }

    private void ScenePresetCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        scenePresetDragStart = e.GetPosition(this);
        draggedScenePreset = (sender as FrameworkElement)?.Tag as SceneSwitchPreset;
    }

    private void ScenePresetCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedScenePreset is null) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - scenePresetDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - scenePresetDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var preset = draggedScenePreset;
        draggedScenePreset = null;
        DragDrop.DoDragDrop((DependencyObject)sender, preset, DragDropEffects.Move);
    }

    private void ScenePresetCard_Drop(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SceneSwitchPreset target ||
            e.Data.GetData(typeof(SceneSwitchPreset)) is not SceneSwitchPreset source ||
            ReferenceEquals(source, target) || source.AccountId != target.AccountId) return;

        sceneSwitchPresets.Remove(source);
        var targetIndex = sceneSwitchPresets.IndexOf(target);
        sceneSwitchPresets.Insert(targetIndex, source);
        SaveSceneSwitchPresets();
        RefreshSceneSwitchPresetFilter(target.AccountId);
        ((MainWindow)Application.Current.MainWindow).RefreshObsSceneShortcutButtons();
        SceneSwitchStatusTextBlock.Text = "ボタンの順番を変更しました";
        e.Handled = true;
    }

    public IReadOnlyList<SceneSwitchPreset> GetSceneSwitchPresets(long? accountId = null)
        => sceneSwitchPresets
            .Where(preset => accountId is null || preset.AccountId == accountId)
            .ToList();

    public void RefreshSceneSwitchPresetFilter(long? accountId)
    {
        var presets = accountId is null
            ? []
            : sceneSwitchPresets.Where(preset => preset.AccountId == accountId.Value).ToList();
        var mainPresets = presets.Where(preset => !preset.IsSub).ToList();
        var subPresets = presets.Where(preset => preset.IsSub).ToList();
        MainSceneSwitchPresetsItemsControl.ItemsSource = mainPresets;
        SubSceneSwitchPresetsItemsControl.ItemsSource = subPresets;
        MainSceneSwitchPresetPanel.Visibility = mainPresets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        SubSceneSwitchPresetPanel.Visibility = subPresets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public async Task ExecuteSceneSwitchPresetAsync(SceneSwitchPreset preset)
    {
        if (sceneSwitchPresets.Contains(preset))
            await SwitchSceneAsync(preset);
    }

    public async Task RefreshSceneShortcutStatesAsync(long? accountId, bool? isSub = null)
    {
        var presets = GetSceneSwitchPresets(accountId)
            .Where(preset => isSub is null || preset.IsSub == isSub.Value)
            .ToList();

        foreach (var group in presets.GroupBy(preset => preset.IsSub))
        {
            try
            {
                var controller = await ((MainWindow)Application.Current.MainWindow)
                    .EnsureObsConnectedAsync(group.Key);
                if (controller is null) continue;

                var currentScene = await Task.Run(controller.GetCurrentProgramScene);
                foreach (var preset in group)
                    preset.IsCurrentScene = string.Equals(
                        preset.SceneName, currentScene, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                foreach (var preset in group)
                    preset.IsCurrentScene = false;
            }
        }

        RefreshSceneSwitchPresetFilter(accountId);
        ((MainWindow)Application.Current.MainWindow).RefreshObsSceneShortcutButtons();
    }

    public IReadOnlyList<SourceSwitchPreset> GetSourceSwitchPresets(long? accountId = null)
        => sourceSwitchPresets
            .Where(preset => accountId is null || preset.AccountId == accountId)
            .ToList();

    public void RefreshSourceSwitchPresetFilter(long? accountId)
    {
        var presets = accountId is null
            ? []
            : sourceSwitchPresets.Where(preset => preset.AccountId == accountId.Value).ToList();
        var mainPresets = presets.Where(preset => !preset.IsSub).ToList();
        var subPresets = presets.Where(preset => preset.IsSub).ToList();
        MainSourceSwitchPresetsItemsControl.ItemsSource = mainPresets;
        SubSourceSwitchPresetsItemsControl.ItemsSource = subPresets;
        MainSourceSwitchPresetPanel.Visibility = mainPresets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        SubSourceSwitchPresetPanel.Visibility = subPresets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public async Task RefreshSourceVisibilityStatesAsync(
        long? accountId,
        bool? isSub = null,
        bool onlyConnected = false)
    {
        var presets = GetSourceSwitchPresets(accountId)
            .Where(preset => isSub is null || preset.IsSub == isSub.Value)
            .ToList();
        foreach (var group in presets.GroupBy(preset => preset.IsSub))
        {
            try
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                var controller = onlyConnected
                    ? mainWindow.GetConnectedObsController(group.Key)
                    : await mainWindow.EnsureObsConnectedAsync(group.Key);
                if (controller is null) continue;
                foreach (var preset in group)
                {
                    try
                    {
                        preset.IsVisible = await Task.Run(() =>
                            controller.GetSceneSourceEnabled(preset.SceneName, preset.SourceName));
                    }
                    catch
                    {
                        preset.IsVisible = false;
                    }
                }
            }
            catch { }
        }
        RefreshSourceSwitchPresetFilter(accountId);
        ((MainWindow)Application.Current.MainWindow).RefreshObsSourceShortcutButtons();
    }

    public async Task ExecuteSourceSwitchPresetAsync(SourceSwitchPreset preset)
    {
        if (sourceSwitchPresets.Contains(preset))
            await ToggleSourceAsync(preset);
    }

    private void SourcePresetCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        sourcePresetDragStart = e.GetPosition(this);
        draggedSourcePreset = (sender as FrameworkElement)?.Tag as SourceSwitchPreset;
    }

    private void SourcePresetCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedSourcePreset is null) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - sourcePresetDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - sourcePresetDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var preset = draggedSourcePreset;
        draggedSourcePreset = null;
        DragDrop.DoDragDrop((DependencyObject)sender, preset, DragDropEffects.Move);
    }

    private void SourcePresetCard_Drop(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SourceSwitchPreset target ||
            e.Data.GetData(typeof(SourceSwitchPreset)) is not SourceSwitchPreset source ||
            ReferenceEquals(source, target) || source.AccountId != target.AccountId) return;
        sourceSwitchPresets.Remove(source);
        sourceSwitchPresets.Insert(sourceSwitchPresets.IndexOf(target), source);
        SaveSourceSwitchPresets();
        RefreshSourceSwitchPresetFilter(target.AccountId);
        ((MainWindow)Application.Current.MainWindow).RefreshObsSourceShortcutButtons();
        SourceSwitchStatusTextBlock.Text = "ボタンの順番を変更しました";
        e.Handled = true;
    }

    private void RestoreSourceSwitchPresets()
    {
        try
        {
            var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ObsSourceSwitchPresets)?.Value;
            if (string.IsNullOrWhiteSpace(json)) return;
            foreach (var preset in JsonSerializer.Deserialize<List<SourceSwitchPreset>>(json) ?? [])
                if (!string.IsNullOrWhiteSpace(preset.SceneName) && !string.IsNullOrWhiteSpace(preset.SourceName))
                    sourceSwitchPresets.Add(preset);
        }
        catch
        {
            SourceSwitchStatusTextBlock.Text = "保存済みのソース切替ボタンを読み込めませんでした";
        }
    }

    private void SaveSourceSwitchPresets() => DAO_Setting.InsertUpdate(
        DAO_Setting.SettingName.ObsSourceSwitchPresets,
        JsonSerializer.Serialize(sourceSwitchPresets));

    private void RestoreSceneSwitchPresets()
    {
        try
        {
            var json = DAO_Setting.SelectOneById(
                DAO_Setting.SettingName.ObsSceneSwitchPresets)?.Value;
            if (string.IsNullOrWhiteSpace(json)) return;
            foreach (var preset in JsonSerializer.Deserialize<List<SceneSwitchPreset>>(json) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(preset.SceneName))
                {
                    // 旧形式は接続先OBSだけを保存していたため、初回読込時に紐づくアカウントへ移行する。
                    if (preset.AccountId <= 0)
                        preset.AccountId = GetObsAccountId(preset.IsSub);
                    preset.AccountDisplayName = DAO_TwitchAccount.SelectById(preset.AccountId)?.UserName
                        ?? $"ID: {preset.AccountId}";
                    sceneSwitchPresets.Add(preset);
                }
            }
            SaveSceneSwitchPresets();
        }
        catch
        {
            SceneSwitchStatusTextBlock.Text = "保存済みの切替ボタンを読み込めませんでした";
        }
    }

    private void SaveSceneSwitchPresets()
    {
        DAO_Setting.InsertUpdate(
            DAO_Setting.SettingName.ObsSceneSwitchPresets,
            JsonSerializer.Serialize(sceneSwitchPresets));
    }

    public sealed class SceneSwitchPreset
    {
        public long AccountId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string AccountDisplayName { get; set; } = string.Empty;
        public bool IsSub { get; set; }
        public string SceneName { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ObsDisplayName => IsSub ? "サブOBS" : "メインOBS";
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsCurrentScene { get; set; }
        public string DisplayName => $"{(IsSub ? "サブ" : "メイン")}｜{SceneName}";
        public string ShortcutDisplayName => SceneName;
    }

    private sealed class SceneChoice
    {
        public bool IsSub { get; init; }
        public string SceneName { get; init; } = string.Empty;
        public string DisplayName => $"{(IsSub ? "サブ" : "メイン")}｜{SceneName}";
    }

    public sealed class SourceSwitchPreset
    {
        public long AccountId { get; set; }
        public bool IsSub { get; set; }
        public string SceneName { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsVisible { get; set; }
        public string DisplayName => $"{(IsSub ? "サブ" : "メイン")}｜{SourceName}";
        public string ShortcutDisplayName => SourceName;
        public string DetailText => $"{SceneName} / {SourceName}";
    }

    private sealed class SourceSceneChoice
    {
        public bool IsSub { get; init; }
        public string SceneName { get; init; } = string.Empty;
        public string DisplayName => $"{(IsSub ? "サブ" : "メイン")}｜{SceneName}";
    }

    private static long GetObsAccountId(bool isSub)
    {
        var setting = isSub
            ? DAO_Setting.SettingName.SubObsTwitchAccountId
            : DAO_Setting.SettingName.MainObsTwitchAccountId;
        return long.TryParse(DAO_Setting.SelectOneById(setting)?.Value, out var accountId)
            ? accountId
            : 0;
    }

    private void MainObsAutoConnectCheckBox_Click(object sender, RoutedEventArgs e)
    {
        DAO_Setting.InsertUpdate(
            DAO_Setting.SettingName.MainObsAutoConnect,
            MainObsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
    }

    private void SubObsAutoConnectCheckBox_Click(object sender, RoutedEventArgs e)
    {
        DAO_Setting.InsertUpdate(
            DAO_Setting.SettingName.SubObsAutoConnect,
            SubObsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
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
            card.Status = (card.IsSub ? SubObsAutoConnectCheckBox : MainObsAutoConnectCheckBox).IsChecked == true
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
        private string displayName = "";
        private string? selectedScene;
        private string? selectedSource;
        private bool isTextLoaded;
        public string DisplayName
        {
            get => displayName;
            set
            {
                if (displayName == value) return;
                displayName = value;
                Notify();
            }
        }
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
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.MainObsAutoConnect,
            MainObsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
        if (MainTwitchAccountComboBox.SelectedValue is long accountId)
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.MainObsTwitchAccountId, accountId.ToString());

        var mainWindow = (MainWindow)Application.Current.MainWindow;
        await mainWindow.ConnectObsAsync(forceReconnect: connectionSettingsChanged);
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
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SubObsAutoConnect,
            SubObsAutoConnectCheckBox.IsChecked == true ? "1" : "0");
        if (SubTwitchAccountComboBox.SelectedValue is long accountId)
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.SubObsTwitchAccountId, accountId.ToString());

        var mainWindow = (MainWindow)Application.Current.MainWindow;
        await mainWindow.ConnectObsAsync(forceReconnect: changed, isSub: true);
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
