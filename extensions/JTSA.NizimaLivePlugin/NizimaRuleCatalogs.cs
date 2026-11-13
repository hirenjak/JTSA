using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace JTSA.NizimaLivePlugin;

public sealed class NizimaRuleCatalogs
{
    public static NizimaRuleCatalogs Empty { get; } = new();

    public IReadOnlyList<NizimaNamedOption> ModelsOnScreen { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> RegisteredModels { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> Expressions { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> Motions { get; init; } = [];
    public IReadOnlyList<NizimaNamedOption> Live2DItems { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<NizimaNamedOption>> ItemExpressions { get; init; } =
        new Dictionary<string, IReadOnlyList<NizimaNamedOption>>();

    public static IReadOnlyList<NizimaNamedOption> WithCurrentModelOption(IEnumerable<NizimaNamedOption> items) =>
    [
        new NizimaNamedOption("（現在のモデル）", ""),
        .. items
    ];
}

internal static class NizimaCommandUi
{
    public static bool ShowsModelTarget(string? commandType) =>
        commandType switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff
                or NizimaTriggerCommands.ExpressionToggle
                or NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion
                or NizimaTriggerCommands.ChangeModel => true,
            _ => false
        };

    public static bool ShowsItemTarget(string? commandType) =>
        commandType is NizimaTriggerCommands.ItemExpressionOn
            or NizimaTriggerCommands.ItemExpressionOff
            or NizimaTriggerCommands.ItemExpressionToggle;

    public static bool ModelTargetRequired(string? commandType) =>
        commandType == NizimaTriggerCommands.ChangeModel;
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

    public static void SelectComboValue(
        ComboBox combo,
        ObservableCollection<NizimaNamedOption> list,
        string? value,
        string? fallbackName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            combo.SelectedValue = "";
            return;
        }

        EnsureOption(list, value, string.IsNullOrWhiteSpace(fallbackName) ? null : fallbackName);
        combo.SelectedItem = list.First(item => string.Equals(item.Path, value, StringComparison.OrdinalIgnoreCase));
        if (combo.IsEditable && combo.SelectedItem is NizimaNamedOption selected)
            combo.Text = selected.Label;
    }
}
