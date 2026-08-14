using JTSA.Dao;
using JTSA.Models;
using NAudio.Wave;
using System.IO;
using System.Windows;

namespace JTSA.Utility;

internal enum StreamExpansionTriggerType { Chat, ChannelPoint, Raid, Subscribe, Bits }

internal sealed class StreamExpansionService
{
    private readonly SemaphoreSlim executionLock = new(1, 1);
    private readonly Random random = new();

    public async Task HandleAsync(StreamExpansionTriggerType type, string value)
    {
        // 発火条件に一致するものだけ取得
        var rules = DAO_StreamExpansion.SelectAllHeaders().Where(rule => Matches(rule, type, value)).ToList();

        var raidPlaceholders = type == StreamExpansionTriggerType.Raid && rules.Count > 0
            ? await GetRaidPlaceholderValuesAsync(value)
            : null;

        // Run each matching rule independently so every delay starts at the trigger time.
        await Task.WhenAll(rules.Select(rule => ExecuteRuleAsync(rule, type, value, raidPlaceholders)));
    }

    private async Task ExecuteRuleAsync(
        T_StreamExpansionHeader rule,
        StreamExpansionTriggerType type,
        string value,
        RaidPlaceholderValues? raidPlaceholders)
    {
        if (rule.DelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(rule.DelaySeconds, 0, 3600)));
        }

        var groups = DAO_StreamExpansion.SelectItems(rule.Id)
            .GroupBy(item => item.SortNumber)
            .Select(group => group.ToList())
            .ToList();

        if (groups.Count > 0)
        {
            var selectedGroup = ChooseByWeight(groups);
            await Task.WhenAll(selectedGroup.Select(item => ExecuteAsync(item, raidPlaceholders)));
        }

        if (type == StreamExpansionTriggerType.Raid && rule.DoShoutout && !string.IsNullOrWhiteSpace(value))
        {
            var raider = await TwitchHelper.GetBroadcasterIdAsync(value);
            if (!string.IsNullOrWhiteSpace(raider?.UserId))
            {
                await TwitchHelper.SendShoutout(raider.UserId);
            }
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
    private static bool Matches(T_StreamExpansionHeader rule, StreamExpansionTriggerType type, string value)
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

            case StreamExpansionTriggerType.Subscribe:
                return rule.IsSubscribe;

            case StreamExpansionTriggerType.Bits:
                return rule.IsBits;
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
    private async Task ExecuteAsync(T_StreamExpansionItem item, RaidPlaceholderValues? raidPlaceholders)
    {
        if (string.IsNullOrWhiteSpace(item.Content)) return;
        switch (item.ActionType)
        {
            case "Chat":
                await TwitchHelper.SendChat(StreamExpansionPlaceholderReplacer.Replace(item.Content, raidPlaceholders));
                break;

            case "Image":
                StreamExpansionOverlayService.ShowImage(item.Content);
                break;

            default:
                if (File.Exists(item.Content)) await PlayAudioAsync(item.Content, item.Volume);
                break;
        }
    }


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
