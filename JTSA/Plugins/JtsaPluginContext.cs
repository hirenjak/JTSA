using JTSA.Plugin.Abstractions;
using JTSA.Utility;
using JTSA.Dao;
using System.Windows.Interop;
using System.IO;

namespace JTSA.Plugins;

internal sealed class JtsaPluginContext : IJtsaPluginContext, IJtsaCalendarPluginContext
{
    private readonly MainWindow mainWindow;
    private readonly string pluginId;

    public JtsaPluginContext(MainWindow mainWindow, string pluginDirectory, string pluginId)
    {
        this.mainWindow = mainWindow;
        this.pluginId = pluginId;
        PluginDirectory = pluginDirectory;
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JTSA", "Plugins", pluginId);
        Directory.CreateDirectory(DataDirectory);
    }

    public string PluginDirectory { get; }
    public string DataDirectory { get; }
    public nint MainWindowHandle => new WindowInteropHelper(mainWindow).Handle;
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
