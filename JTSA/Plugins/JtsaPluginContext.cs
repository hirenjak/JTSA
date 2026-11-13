using JTSA.Plugin.Abstractions;
using JTSA.Utility;
using JTSA.Dao;
using System.Windows.Interop;
using System.IO;
using Newtonsoft.Json.Linq;

namespace JTSA.Plugins;

internal sealed class JtsaPluginContext :
    IJtsaPluginContext,
    IJtsaCalendarPluginContext,
    IJtsaStreamMetadataPluginContext,
    IJtsaExternalChatPluginContext,
    IJtsaRemotePanelPluginContext,
    IJtsaTodoPluginContext,
    IJtsaExpansionTriggerPluginContext,
    IJtsaExpansionMediaPluginContext,
    IJtsaChannelPointStatusPluginContext,
    IJtsaExpansionActionPluginContext
{
    private readonly MainWindow mainWindow;
    private readonly string pluginId;

    public JtsaPluginContext(MainWindow mainWindow, string pluginDirectory, string pluginId)
    {
        this.mainWindow = mainWindow;
        this.pluginId = pluginId;
        PluginDirectory = pluginDirectory;
        DataDirectory = PluginStorage.GetDataDirectory(pluginId);
    }

    public string PluginDirectory { get; }
    public string DataDirectory { get; }
    public nint MainWindowHandle => new WindowInteropHelper(mainWindow).Handle;
    public StreamMetadataInfo GetCurrentStreamMetadata() => new(
        mainWindow.CurrentTitleText,
        mainWindow.CurrentCategoryId,
        mainWindow.CurrentCategoryName);

    public void AddExternalChatMessage(ExternalChatMessageInfo message) =>
        mainWindow.ChatPanel.AddExternalChatMessage(message);

    public void SetRemotePanel(RemotePluginPanelContent panel) =>
        RemotePanelRegistry.Set(pluginId, panel);

    public void RemoveRemotePanel(string id) =>
        RemotePanelRegistry.Remove(pluginId, id);

    public void SetInteractiveRemotePanel(RemotePluginPanelContent panel, Func<string, string?, bool> onAction, Func<string> getState) =>
        RemotePanelRegistry.Set(pluginId, panel, onAction, getState);

    public bool AddTodo(string text) =>
        mainWindow.Dispatcher.Invoke(() => mainWindow.ChatPanel.ApplyTodoChange(new TodoChange("add", Text: text)));

    public IReadOnlyList<ChannelPointRewardInfo> GetChannelPointRewards() =>
        mainWindow.ChannelPointPanel.ChannelPointRewardFormList
            .Select(reward => new ChannelPointRewardInfo(
                reward.RewardId,
                reward.Title,
                reward.IsUserInputRequired))
            .ToArray();

    public IReadOnlyList<CalendarEntryInfo> GetCalendarEntries(DateTime from, DateTime toExclusive)
    {
        var start = from.Date;
        var end = toExclusive.Date;
        if (end <= start) return [];

        var titleTags = DAO_TitleTag.SelectAllOrderbyLastUser();
        var friendPrefix = DAO_Setting.SelectOneById(DAO_Setting.SettingName.FriendPrefixWord)?.Value ?? string.Empty;

        return DAO_Calendar.SelectAll()
            .Where(entry => entry.CalendarDate >= start && entry.CalendarDate < end)
            .Select(entry => new CalendarEntryInfo(
                entry.Id,
                entry.CalendarDate.Date,
                entry.StartTime,
                entry.Content,
                entry.TitlePlaceholder,
                entry.CategoryName,
                entry.CategoryBoxArtUrl,
                ResolveCalendarTitle(entry, titleTags, friendPrefix)))
            .ToArray();
    }

    private static string ResolveCalendarTitle(
        Models.T_CalendarEntry entry,
        IReadOnlyList<M_TitleTag> titleTags,
        string friendPrefix)
    {
        var category = DAO_Category.SelectOneById(entry.CategoryId);
        var japaneseCategoryName = string.IsNullOrWhiteSpace(category?.JapaneseDisplayName)
            ? entry.CategoryName
            : category.JapaneseDisplayName;
        var template = string.IsNullOrWhiteSpace(entry.TitlePlaceholder)
            ? TitlePlaceholderReplacer.TitlePlaceholder
            : entry.TitlePlaceholder;
        var title = TitlePlaceholderReplacer.ReplaceTitle(entry.Content, template, japaneseCategoryName);
        var friendText = string.IsNullOrWhiteSpace(entry.SelectedFriendNames)
            ? string.Empty
            : string.IsNullOrWhiteSpace(friendPrefix)
                ? entry.SelectedFriendNames
                : $"{friendPrefix} {entry.SelectedFriendNames}";
        title = title.Replace("${friend}", friendText, StringComparison.OrdinalIgnoreCase);
        title = TitlePlaceholderReplacer.ReplaceDate(title, entry.CalendarDate);
        foreach (var titleTag in titleTags)
            title = title.Replace($"${{{titleTag.Id}}}", titleTag.DisplayName, StringComparison.OrdinalIgnoreCase);
        return title.Trim();
    }

    public event Action<ChannelPointRedemptionInfo>? ChannelPointRedeemed
    {
        add => PluginChannelPointEventHub.ChannelPointRedeemed += value;
        remove => PluginChannelPointEventHub.ChannelPointRedeemed -= value;
    }

    public Task<IReadOnlyList<ChannelPointRewardStatusInfo>> GetChannelPointRewardStatusesAsync() =>
        PluginChannelPointStatus.GetRewardStatusesAsync();

    public Task<IReadOnlyList<ChannelPointPendingRedemptionInfo>> GetUnfulfilledRedemptionsAsync(string rewardId) =>
        PluginChannelPointStatus.GetUnfulfilledRedemptionsAsync(rewardId);

    public Task<bool> CompleteRedemptionAsync(string rewardId, string redemptionId, bool fulfilled) =>
        TwitchHelper.UpdateRedemptionStatusAsync(rewardId, redemptionId, fulfilled);

    public string GetExpansionMediaUrl(string filePath) =>
        StreamExpansionOverlayService.RegisterMediaFile(filePath);

    public event Action<ExpansionTriggerInfo>? ExpansionTriggered
    {
        add => PluginExpansionTriggerHub.ExpansionTriggered += value;
        remove => PluginExpansionTriggerHub.ExpansionTriggered -= value;
    }

    public Task SendChatAsync(string message) => RunActionAsync("チャット送信", async () =>
    {
        var account = StreamExpansionService.GetSelectedAccountContext();
        await TwitchHelper.SendChat(message, account.BroadcasterId, account.AccessToken);
    });

    public Task ShoutoutAsync(string userLogin) => RunActionAsync("Shoutout", () =>
    {
        var account = StreamExpansionService.GetSelectedAccountContext();
        return StreamExpansionService.SendRaidShoutoutAsync(userLogin, account.BroadcasterId, account.AccessToken);
    });

    public Task GrantVipAsync(string userLogin) => RunActionAsync("VIP付与", () =>
    {
        var account = StreamExpansionService.GetSelectedAccountContext();
        return StreamExpansionService.GrantChatUserVipAsync(userLogin, account.BroadcasterId, account.AccessToken);
    });

    public Task PlayClipAsync(string userLogin, int rankingLimit) =>
        RunActionAsync("クリップ再生", () => StreamExpansionService.PlayClipPreviewAsync(userLogin, rankingLimit));

    public Task SetObsTextAsync(bool isSubObs, string sourceName, string text) =>
        RunActionAsync($"OBSテキストソース変更（{sourceName}）",
            () => StreamExpansionService.SetObsTextOnUiThreadAsync(isSubObs, sourceName, text));

    public Task TriggerVtsHotkeyAsync(string hotkeyId) =>
        RunActionAsync($"VTSホットキー実行（{hotkeyId}）", () => VtsTriggerService.ExecuteHotkeyAsync(hotkeyId));

    public async Task<IReadOnlyList<ExpansionNamedOption>> GetVtsHotkeysAsync()
    {
        var client = await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.VtsClient);
        if (!client.IsAuthenticated) throw new InvalidOperationException("VTSに接続されていません。");
        var response = await client.GetHotkeysAsync();
        return (response["data"]?["availableHotkeys"] as JArray)?
            .OfType<JObject>()
            .Select(item => new ExpansionNamedOption(
                item.Value<string>("hotkeyID") ?? "",
                item.Value<string>("name") ?? item.Value<string>("hotkeyID") ?? ""))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyList<string>> GetObsScenesAsync(bool isSubObs)
    {
        var controller = await GetObsAsync(isSubObs);
        return await Task.Run(controller.GetSceneNames);
    }

    public async Task<IReadOnlyList<ExpansionObsSource>> GetObsSourcesAsync(bool isSubObs, string sceneName, bool textOnly)
    {
        var controller = await GetObsAsync(isSubObs);
        return await Task.Run(() => textOnly
            ? controller.GetTextSourceNames(sceneName).Select(name => new ExpansionObsSource(name, "")).ToArray()
            : controller.GetSceneSources(sceneName).Select(source => new ExpansionObsSource(source.SourceName, source.ContainerName)).ToArray());
    }

    public Task SetObsSourceVisibleAsync(bool isSubObs, string sceneName, string sourceName, string containerName, bool? visible, bool allScenes) =>
        RunActionAsync($"OBSソース表示切替（{sourceName}）", async () =>
        {
            var controller = await GetObsAsync(isSubObs);
            await Task.Run(() =>
            {
                var next = visible ?? !controller.GetSceneSourceEnabled(sceneName, sourceName, containerName);
                if (allScenes) controller.SetInputVisibleAcrossScenes(sourceName, next);
                else controller.SetSceneSourceEnabled(sceneName, sourceName, next, containerName);
            });
            // 本体の「ソース表示切替」ボタンの状態も合わせる
            await mainWindow.Dispatcher.InvokeAsync(() =>
                mainWindow.ObsSettingPanel.RefreshSourceVisibilityStatesAsync(mainWindow.SelectedTargetAccountId, isSubObs)).Task.Unwrap();
        });

    public async Task<IReadOnlyList<string>> GetObsAudioInputsAsync(bool isSubObs)
    {
        var controller = await GetObsAsync(isSubObs);
        return await Task.Run(controller.GetAudioInputNames);
    }

    public Task SetObsInputVolumeAsync(bool isSubObs, string inputName, double volume) =>
        RunActionAsync($"OBS音量変更（{inputName}）", async () =>
        {
            var controller = await GetObsAsync(isSubObs);
            await Task.Run(() => controller.SetInputVolume(inputName, volume));
        });

    private async Task<ObsController> GetObsAsync(bool isSubObs) =>
        await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.EnsureObsConnectedAsync(isSubObs)).Task.Unwrap()
        ?? throw new InvalidOperationException("OBSに接続できませんでした。");

    /// <summary>失敗はログに出し、プラグインへは例外を返さない。</summary>
    private async Task RunActionAsync(string name, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogError($"{name}に失敗しました。", ex);
        }
    }

    public void Log(string message) =>
        mainWindow.AppLogPanel.Success("Extension", message);

    public void LogError(string message, Exception? exception = null) =>
        mainWindow.AppLogPanel.Error(
            "Extension",
            exception is null ? message : $"{message} {exception.GetBaseException().Message}");

    public void SetExpansionOverlay(ExpansionOverlayContent content) =>
        StreamExpansionOverlayService.SetPluginOverlay(pluginId, content);

    public void RemoveExpansionOverlay(string id) =>
        StreamExpansionOverlayService.RemovePluginOverlay(pluginId, id);
}
