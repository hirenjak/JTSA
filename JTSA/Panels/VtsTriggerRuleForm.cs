using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using JTSA.Utility;

namespace JTSA.Panels;

public sealed class VtsNamedOption
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

internal static class VtsNamedOptionCatalog
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

    public static void ReplaceKeeping(
        ObservableCollection<VtsNamedOption> target,
        IEnumerable<VtsNamedOption>? items,
        IEnumerable<string> keepIds)
    {
        var next = (items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, IdComparer)
            .Select(group => group.First())
            .ToList();
        var keep = keepIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(IdComparer);

        foreach (var id in keep)
        {
            if (next.Any(item => IdComparer.Equals(item.Id, id)))
                continue;
            var existing = target.FirstOrDefault(item => IdComparer.Equals(item.Id, id));
            next.Add(existing ?? new VtsNamedOption { Id = id, Name = id });
        }

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (next.All(item => !IdComparer.Equals(item.Id, target[index].Id)))
                target.RemoveAt(index);
        }

        foreach (var item in next)
        {
            var currentIndex = IndexOfId(target, item.Id);
            if (currentIndex < 0)
                target.Add(item);
            else if (target[currentIndex].Name != item.Name || target[currentIndex].Id != item.Id)
                target[currentIndex] = item;
        }
    }

    public static void EnsureOption(ObservableCollection<VtsNamedOption> list, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || IndexOfId(list, id) >= 0)
            return;
        list.Add(new VtsNamedOption { Id = id, Name = id });
    }

    private static int IndexOfId(ObservableCollection<VtsNamedOption> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (IdComparer.Equals(list[index].Id, id))
                return index;
        }

        return -1;
    }
}

internal sealed class VtsTriggerRuleForm : INotifyPropertyChanged
{
    public VtsTriggerRule Rule { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    public VtsTriggerRuleForm(VtsTriggerRule rule)
    {
        Rule = rule;
        Rule.Extra ??= new VtsTriggerCommandExtra();
    }

    public bool IsEnabled
    {
        get => Rule.IsEnabled;
        set { Rule.IsEnabled = value; Notify(); }
    }

    public string TriggerType
    {
        get => Rule.TriggerType;
        set
        {
            if (Rule.TriggerType == value) return;
            Rule.TriggerType = value;
            Notify();
            Notify(nameof(ShowChannelPointDetail));
            Notify(nameof(ShowChatDetail));
            Notify(nameof(ShowScheduledTimeDetail));
            Notify(nameof(ShowAdUpcomingDetail));
            Notify(nameof(ShowObsDetail));
            Notify(nameof(ShowNoTriggerDetail));
        }
    }

    public string TriggerValue
    {
        get => Rule.TriggerValue;
        set { Rule.TriggerValue = value ?? ""; Notify(); }
    }

    public string CommandType
    {
        get => Rule.CommandType;
        set
        {
            if (Rule.CommandType == value) return;
            Rule.CommandType = value;
            Notify();
            Notify(nameof(ShowMoveDetail));
            Notify(nameof(ShowTintDetail));
            Notify(nameof(ShowPostProcessingDetail));
            Notify(nameof(ShowRawJsonDetail));
            Notify(nameof(ShowModelDetail));
            Notify(nameof(ShowHotkeyDetail));
            Notify(nameof(ShowExpressionDetail));
            Notify(nameof(ShowItemDetail));
        }
    }

    public string CommandValue
    {
        get => Rule.CommandValue;
        set { Rule.CommandValue = value ?? ""; Notify(); }
    }

    public bool ShowChannelPointDetail => TriggerType == VtsTriggerTypes.ChannelPoint;
    public bool ShowChatDetail => TriggerType == VtsTriggerTypes.Chat;
    public bool ShowScheduledTimeDetail => TriggerType == VtsTriggerTypes.ScheduledTime;
    public bool ShowAdUpcomingDetail => TriggerType == VtsTriggerTypes.AdUpcoming;
    public bool ShowObsDetail => TriggerType == VtsTriggerTypes.ObsStreamStart;
    public bool ShowNoTriggerDetail =>
        !ShowChannelPointDetail && !ShowChatDetail && !ShowScheduledTimeDetail &&
        !ShowAdUpcomingDetail && !ShowObsDetail;

    public bool ShowModelDetail => CommandType == VtsTriggerCommands.LoadModel;
    public bool ShowHotkeyDetail => CommandType == VtsTriggerCommands.TriggerHotkey;
    public bool ShowExpressionDetail => CommandType is
        VtsTriggerCommands.ExpressionOn or VtsTriggerCommands.ExpressionOff;
    public bool ShowItemDetail => CommandType is
        VtsTriggerCommands.LoadItem or VtsTriggerCommands.UnloadItem;
    public bool ShowMoveDetail => CommandType == VtsTriggerCommands.MoveModel;
    public bool ShowTintDetail => CommandType == VtsTriggerCommands.Tint;
    public bool ShowPostProcessingDetail => CommandType == VtsTriggerCommands.PostProcessing;
    public bool ShowRawJsonDetail => CommandType == VtsTriggerCommands.RawJson;

    public double ExtraTime
    {
        get => Rule.Extra.Time;
        set { Rule.Extra.Time = value; Notify(); }
    }

    public double ExtraX
    {
        get => Rule.Extra.X;
        set { Rule.Extra.X = value; Notify(); }
    }

    public double ExtraY
    {
        get => Rule.Extra.Y;
        set { Rule.Extra.Y = value; Notify(); }
    }

    public double ExtraRotation
    {
        get => Rule.Extra.Rotation;
        set { Rule.Extra.Rotation = value; Notify(); }
    }

    public double ExtraSize
    {
        get => Rule.Extra.Size;
        set { Rule.Extra.Size = value; Notify(); }
    }

    public bool ExtraRelative
    {
        get => Rule.Extra.Relative;
        set { Rule.Extra.Relative = value; Notify(); }
    }

    public int ExtraR
    {
        get => Rule.Extra.R;
        set { Rule.Extra.R = value; Notify(); }
    }

    public int ExtraG
    {
        get => Rule.Extra.G;
        set { Rule.Extra.G = value; Notify(); }
    }

    public int ExtraB
    {
        get => Rule.Extra.B;
        set { Rule.Extra.B = value; Notify(); }
    }

    public int ExtraA
    {
        get => Rule.Extra.A;
        set { Rule.Extra.A = value; Notify(); }
    }

    public bool ExtraTintAll
    {
        get => Rule.Extra.TintAll;
        set { Rule.Extra.TintAll = value; Notify(); }
    }

    public bool ExtraPostProcessingOn
    {
        get => Rule.Extra.PostProcessingOn;
        set { Rule.Extra.PostProcessingOn = value; Notify(); }
    }

    public double ExtraPostProcessingValue
    {
        get => Rule.Extra.PostProcessingValue;
        set { Rule.Extra.PostProcessingValue = value; Notify(); }
    }

    public string ExtraRawJson
    {
        get => Rule.Extra.RawJson;
        set { Rule.Extra.RawJson = value ?? ""; Notify(); }
    }

    private void Notify([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Changed?.Invoke();
    }
}
