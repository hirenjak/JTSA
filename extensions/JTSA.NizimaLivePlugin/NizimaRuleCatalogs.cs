using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace JTSA.NizimaLivePlugin;

public sealed class NizimaRuleCatalogs
{
    public static NizimaRuleCatalogs Empty { get; } = new();

    public IReadOnlyList<NizimaNamedOption> ModelsOnScreen { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> RegisteredModels { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> Scenes { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> RegisteredItems { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> ItemsOnScreen { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> EffectGroups { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> Expressions { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> Motions { get; init; } = [];

    public static IReadOnlyList<NizimaNamedOption> WithCurrentModelOption(IEnumerable<NizimaNamedOption> items) =>
    [
        new NizimaNamedOption("（現在のモデル）", ""),
        .. items
    ];

    public static IReadOnlyList<NizimaNamedOption> WithNewWindowSceneOption(IEnumerable<NizimaNamedOption> items) =>
    [
        new NizimaNamedOption("（新規ウィンドウ）", ""),
        .. items
    ];
}

internal static class NizimaCommandUi
{
    public static bool UsesHotkey(string? commandType) =>
        commandType == NizimaTriggerCommands.TriggerHotkey;

    public static bool ShowsCommandValue(string? commandType) =>
        commandType switch
        {
            NizimaTriggerCommands.MoveModel or NizimaTriggerCommands.SetModelColor or NizimaTriggerCommands.RawJson => false,
            _ => true
        };

    public static bool ShowsModelTarget(string? commandType) =>
        commandType switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff
                or NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion
                or NizimaTriggerCommands.TriggerHotkey or NizimaTriggerCommands.ChangeModel
                or NizimaTriggerCommands.MoveModel or NizimaTriggerCommands.SetModelColor => true,
            _ => false
        };

    public static bool ModelTargetRequired(string? commandType) =>
        commandType == NizimaTriggerCommands.ChangeModel;

    public static bool ShowsScene(string? commandType) =>
        commandType is NizimaTriggerCommands.AddModel or NizimaTriggerCommands.AddItem;

    public static bool SceneRequired(string? commandType) =>
        commandType == NizimaTriggerCommands.AddItem;
}

internal static class NizimaNamedOptionCatalog
{
    public static void EnsureOption(ObservableCollection<NizimaNamedOption> list, string path, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (list.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
            return;
        list.Add(new NizimaNamedOption(name ?? path, path));
    }

    public static void ReplaceAll(ObservableCollection<NizimaNamedOption> target, IEnumerable<NizimaNamedOption> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    public static void SelectComboValue(ComboBox combo, ObservableCollection<NizimaNamedOption> list, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            combo.SelectedValue = "";
            return;
        }

        EnsureOption(list, value);
        combo.SelectedValue = value;
        if (combo.SelectedIndex < 0)
            combo.Text = value;
    }
}
