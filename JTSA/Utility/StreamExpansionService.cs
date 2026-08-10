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

        // 発火条件一致のしたものの内容を実行
        foreach (var rule in rules)
        {
            var groups = DAO_StreamExpansion.SelectItems(rule.Id)
                .GroupBy(item => item.SortNumber)
                .Select(group => group.ToList())
                .ToList();

            if (groups.Count == 0) continue;

            var selectedGroup = ChooseByWeight(groups);
            await Task.WhenAll(selectedGroup.Select(ExecuteAsync));
        }
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
    private async Task ExecuteAsync(T_StreamExpansionItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Content)) return;
        switch (item.ActionType)
        {
            case "Chat":
                await TwitchHelper.SendChat(item.Content);
                break;

            case "Image":
                if (File.Exists(item.Content)) return;

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
