using JTSA.Dao;
using JTSA.Models;
using NAudio.Wave;
using System.IO;
using System.Windows;

namespace JTSA.Utility;

internal enum StreamExpansionTriggerType { Chat, FirstChat, Follow, ChannelPoint, Raid, Subscribe, Bits, ObsStreamStart }

internal sealed class StreamExpansionService
{
    private readonly SemaphoreSlim executionLock = new(1, 1);
    private readonly Random random = new();

    public async Task HandleAsync(
        StreamExpansionTriggerType type,
        string value,
        ChatPlaceholderValues? chatPlaceholders = null,
        string triggerObs = "",
        string broadcasterId = "",
        string accessToken = "",
        string channelPointInput = "")
    {
        try
        {
            // 発火条件に一致するものだけ取得
            var rules = DAO_StreamExpansion.SelectAllHeaders().Where(rule => Matches(rule, type, value)).ToList();

            if (type == StreamExpansionTriggerType.Raid)
            {
                LogSuccess($"レイド通知受信：{value}（一致ルール {rules.Count}件）");
            }

            var raidPlaceholders = type == StreamExpansionTriggerType.Raid && rules.Count > 0
                ? await GetRaidPlaceholderValuesAsync(value)
                : null;

            // Run each matching rule independently so every delay starts at the trigger time.
            await Task.WhenAll(rules.Select(rule =>
                ExecuteRuleAsync(rule, type, value, raidPlaceholders, chatPlaceholders,
                    triggerObs, broadcasterId, accessToken, channelPointInput)));
        }
        catch (Exception ex)
        {
            LogError($"配信拡張の実行失敗（{type}）：{ex.Message}");
        }
    }

    private async Task ExecuteRuleAsync(
        T_StreamExpansionHeader rule,
        StreamExpansionTriggerType type,
        string value,
        RaidPlaceholderValues? raidPlaceholders,
        ChatPlaceholderValues? chatPlaceholders,
        string triggerObs,
        string broadcasterId,
        string accessToken,
        string channelPointInput)
    {
        if (rule.DelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(rule.DelaySeconds, 0, 3600)));
        }

        var groups = DAO_StreamExpansion.SelectItems(rule.Id)
            .GroupBy(item => item.SortNumber)
            .Select(group => group.ToList())
            .ToList();

        var tasks = new List<Task>();

        if (groups.Count > 0)
        {
            var selectedGroup = ChooseByWeight(groups);
            var triggerValues = await CreateTriggerValuesAsync(
                selectedGroup, type, value, triggerObs, broadcasterId, accessToken, channelPointInput);
            tasks.AddRange(selectedGroup.Where(item => item.ActionType != "ObsText").Select(item =>
                ExecuteAsync(item, raidPlaceholders, chatPlaceholders, triggerValues)));
            foreach (var item in selectedGroup.Where(item => item.ActionType == "ObsText"))
                await ExecuteAsync(item, raidPlaceholders, chatPlaceholders, triggerValues);
        }

        if (type == StreamExpansionTriggerType.Raid && rule.DoShoutout && !string.IsNullOrWhiteSpace(value))
        {
            tasks.Add(SendRaidShoutoutAsync(value));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task SendRaidShoutoutAsync(string userName)
    {
        LogSuccess($"自動Shoutout対象ユーザーを検索：{userName}");
        var raider = await TwitchHelper.GetBroadcasterIdAsync(userName);
        if (string.IsNullOrWhiteSpace(raider?.UserId))
        {
            LogError($"自動Shoutout中断：Twitchユーザーを特定できませんでした（{userName}）");
            return;
        }

        LogSuccess($"自動Shoutout送信開始：{raider.DisplayName}（ID: {raider.UserId}）");
        var succeeded = await TwitchHelper.SendShoutout(raider.UserId);
        if (!succeeded)
        {
            LogError($"自動Shoutout送信失敗：{raider.DisplayName}。直前のShoutout失敗ログを確認してください");
        }
    }

    private static void LogSuccess(string message)
    {
        var application = Application.Current;
        if (application?.Dispatcher == null)
        {
            return;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => LogSuccess(message));
            return;
        }

        if (application.MainWindow is MainWindow mainWindow)
        {
            mainWindow.AppLogPanel.Success(nameof(StreamExpansionService), message);
        }
    }

    private static void LogError(string message)
    {
        var application = Application.Current;
        if (application?.Dispatcher == null)
        {
            return;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => LogError(message));
            return;
        }

        if (application.MainWindow is MainWindow mainWindow)
        {
            mainWindow.AppLogPanel.Error(nameof(StreamExpansionService), message);
        }
    }

    private static async Task<RaidPlaceholderValues> GetRaidPlaceholderValuesAsync(string userName)
    {
        var raider = await TwitchHelper.GetBroadcasterIdAsync(userName);
        if (string.IsNullOrWhiteSpace(raider?.UserId))
        {
            return new RaidPlaceholderValues(userName, string.Empty, string.Empty);
        }

        var channel = await TwitchHelper.GetTwitchStreamInfo(raider.UserId);
        return new RaidPlaceholderValues(
            string.IsNullOrWhiteSpace(raider.DisplayName) ? userName : raider.DisplayName,
            channel?.title ?? string.Empty,
            channel?.gameName ?? string.Empty);
    }


    /// <summary>
    /// 発火条件の確認
    /// </summary>
    internal static bool Matches(T_StreamExpansionHeader rule, StreamExpansionTriggerType type, string value)
    {
        if (!rule.IsActive)
        {
            return false;
        }

        switch (type)
        {
            case StreamExpansionTriggerType.Chat:
                if (!string.IsNullOrWhiteSpace(rule.TriggerComment) &&
                    value.Contains(rule.TriggerComment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                break;

            case StreamExpansionTriggerType.ChannelPoint:
                if (!string.IsNullOrWhiteSpace(rule.TriggerChannelPointId)
                && string.Equals(value, rule.TriggerChannelPointId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                break;

            case StreamExpansionTriggerType.Raid:
                return rule.IsRaid;

            case StreamExpansionTriggerType.FirstChat:
                return rule.IsFirstChat;

            case StreamExpansionTriggerType.Follow:
                return rule.IsFollow;

            case StreamExpansionTriggerType.Subscribe:
                return rule.IsSubscribe;

            case StreamExpansionTriggerType.Bits:
                return rule.IsBits;

            case StreamExpansionTriggerType.ObsStreamStart:
                if (!rule.IsObsStreamStart) return false;
                return string.Equals(value, "sub", StringComparison.OrdinalIgnoreCase)
                    ? rule.IsObsStreamStartSub
                    : rule.IsObsStreamStartMain;
        }

        return false;
    }


    /// <summary>
    /// 重みづけランダム抽選処理
    /// </summary>
    private List<T_StreamExpansionItem> ChooseByWeight(List<List<T_StreamExpansionItem>> groups)
    {
        var total = groups.Sum(group => Math.Max(1, group[0].Weight));
        var selected = random.Next(total);
        foreach (var group in groups)
        {
            selected -= Math.Max(1, group[0].Weight);
            if (selected < 0) return group;
        }
        return groups[^1];
    }


    /// <summary>
    /// 選択処理の実行処理
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    private async Task ExecuteAsync(
        T_StreamExpansionItem item,
        RaidPlaceholderValues? raidPlaceholders,
        ChatPlaceholderValues? chatPlaceholders,
        StreamExpansionTriggerValues triggerValues)
    {
        if (string.IsNullOrWhiteSpace(item.Content) && item.ActionType != "ObsText") return;
        switch (item.ActionType)
        {
            case "Chat":
                await TwitchHelper.SendChat(StreamExpansionPlaceholderReplacer.Replace(
                    item.Content,
                    raidPlaceholders,
                    chatPlaceholders,
                    triggerValues));
                break;

            case "ObsText":
                try
                {
                    var text = StreamExpansionPlaceholderReplacer.Replace(
                        item.Content, raidPlaceholders, chatPlaceholders, triggerValues);
                    await SetObsTextOnUiThreadAsync(item.IsSubObs, item.ObsSourceName, text);
                }
                catch (Exception ex)
                {
                    LogError($"OBSテキストソース変更失敗（{item.ObsSourceName}）：{ex.GetBaseException().Message}");
                }
                break;

            case "Image":
                StreamExpansionOverlayService.ShowImage(item.Content);
                break;

            default:
                if (File.Exists(item.Content)) await PlayAudioAsync(item.Content, item.Volume);
                break;
        }
    }

    private static async Task SetObsTextOnUiThreadAsync(bool isSubObs, string sourceName, string text)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("アプリケーションを取得できませんでした。");
        await application.Dispatcher.InvokeAsync(async () =>
        {
            if (application.MainWindow is not MainWindow mainWindow)
                throw new InvalidOperationException("メインウィンドウを取得できませんでした。");
            await mainWindow.SetObsTextSourceAsync(isSubObs, sourceName, text);
        }).Task.Unwrap();
    }


    private static async Task<StreamExpansionTriggerValues> CreateTriggerValuesAsync(
        IReadOnlyCollection<T_StreamExpansionItem> items,
        StreamExpansionTriggerType type,
        string value,
        string triggerObs,
        string broadcasterId,
        string accessToken,
        string channelPointInput)
    {
        var needsStreamInfo = items.Any(item => item.ActionType == "ObsText" &&
            (item.Content.Contains(StreamExpansionPlaceholderReplacer.StreamTitlePlaceholder, StringComparison.OrdinalIgnoreCase) ||
             item.Content.Contains(StreamExpansionPlaceholderReplacer.StreamCategoryPlaceholder, StringComparison.OrdinalIgnoreCase)));
        var title = string.Empty;
        var category = string.Empty;
        if (needsStreamInfo)
        {
            try
            {
                var id = string.IsNullOrWhiteSpace(broadcasterId) ? TwitchHelper.BroadcasterId : broadcasterId;
                var token = string.IsNullOrWhiteSpace(accessToken) ? TwitchHelper.AccessToken : accessToken;
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(token))
                {
                    var info = await TwitchHelper.GetTwitchStreamInfo(id, token);
                    title = info?.title ?? string.Empty;
                    category = info?.gameName ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogError($"配信情報プレースホルダー取得失敗：{ex.GetBaseException().Message}");
            }
        }

        return new StreamExpansionTriggerValues(
            ToTriggerName(type), value, triggerObs, title, category, channelPointInput);
    }

    private static string ToTriggerName(StreamExpansionTriggerType type) => type switch
    {
        StreamExpansionTriggerType.FirstChat => "first_chat",
        StreamExpansionTriggerType.ChannelPoint => "channel_point",
        StreamExpansionTriggerType.ObsStreamStart => "obs_stream_start",
        _ => type.ToString().ToLowerInvariant()
    };


    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private async Task PlayAudioAsync(string path, int volume)
    {
        await executionLock.WaitAsync();
        try
        {
            using var reader = new AudioFileReader(path);
            reader.Volume = Math.Clamp(volume, 0, 100) / 100f;
            using var output = new WaveOutEvent();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            output.PlaybackStopped += (_, _) => completion.TrySetResult();
            output.Init(reader);
            output.Play();
            await completion.Task;
        }
        finally { executionLock.Release(); }
    }
}
