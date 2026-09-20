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
                or NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion
                or NizimaTriggerCommands.ChangeModel => true,
            _ => false
        };

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
