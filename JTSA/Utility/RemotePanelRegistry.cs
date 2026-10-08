using JTSA.Plugin.Abstractions;

namespace JTSA.Utility;

internal sealed record RemotePanelInfo(string Id, string Title, string Html, bool IsPlugin, string? State = null);

internal static class RemotePanelRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<(string PluginId, string PanelId), RemotePanelInfo> PluginPanels = new();
    private static readonly Dictionary<string, (Func<string, string?, bool> Action, Func<string> State)> Handlers = new();
    private static readonly RemotePanelInfo[] BuiltInPanels =
    [
        new("obs", "OBS", string.Empty, false),
        new("chat", "チャット", string.Empty, false),
        new("todo", "ToDo", string.Empty, false)
    ];

    public static event Action? Changed;
    private static string[] panelOrder = [];

    public static void SetOrder(IEnumerable<string> ids)
    {
        lock (Sync) panelOrder = ids.Distinct(StringComparer.Ordinal).ToArray();
        Changed?.Invoke();
    }

    public static IReadOnlyList<RemotePanelInfo> GetPanels()
    {
        RemotePanelInfo[] panels;
        Dictionary<string, Func<string>> states;
        lock (Sync)
        {
            panels = BuiltInPanels.Concat(PluginPanels.Values
                .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase))
                .OrderBy(item =>
            {
                var index = Array.IndexOf(panelOrder, item.Id);
                return index < 0 ? int.MaxValue : index;
            }).ToArray();
            states = Handlers.ToDictionary(item => item.Key, item => item.Value.State);
        }
        return panels.Select(item => states.TryGetValue(item.Id, out var state)
            ? item with { State = state() } : item).ToArray();
    }

    public static void Set(string pluginId, RemotePluginPanelContent content,
        Func<string, string?, bool>? onAction = null, Func<string>? getState = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(content.Id) || content.Id.Length > 64 ||
            !content.Id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'))
            throw new ArgumentException("パネルIDは英数字・ハイフン・アンダースコアで64文字以内にしてください。", nameof(content));
        if (string.IsNullOrWhiteSpace(content.Title) || content.Title.Length > 60)
            throw new ArgumentException("パネル名は1～60文字にしてください。", nameof(content));
        if (content.Html is null || content.Html.Length > 100_000)
            throw new ArgumentException("パネルHTMLは100,000文字以内にしてください。", nameof(content));

        lock (Sync)
        {
            PluginPanels[(pluginId, content.Id)] = new RemotePanelInfo(
                $"plugin:{pluginId}:{content.Id}", content.Title.Trim(), content.Html, true);
            var id = $"plugin:{pluginId}:{content.Id}";
            if (onAction != null && getState != null) Handlers[id] = (onAction, getState);
            else Handlers.Remove(id);
        }
        Changed?.Invoke();
    }

    public static bool ApplyAction(string id, string action, string? value)
    {
        Func<string, string?, bool>? callback;
        lock (Sync) callback = Handlers.TryGetValue(id, out var handler) ? handler.Action : null;
        return callback?.Invoke(action, value) == true;
    }

    public static void Remove(string pluginId, string panelId)
    {
        bool removed;
        lock (Sync)
        {
            removed = PluginPanels.Remove((pluginId, panelId));
            Handlers.Remove($"plugin:{pluginId}:{panelId}");
        }
        if (removed) Changed?.Invoke();
    }

    public static void RemoveAll(string pluginId)
    {
        bool removed;
        lock (Sync)
        {
            var keys = PluginPanels.Keys.Where(key => key.PluginId == pluginId).ToArray();
            foreach (var key in keys)
            {
                PluginPanels.Remove(key);
                Handlers.Remove($"plugin:{key.PluginId}:{key.PanelId}");
            }
            removed = keys.Length > 0;
        }
        if (removed) Changed?.Invoke();
    }
}
