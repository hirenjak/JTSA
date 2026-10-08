namespace JTSA.Plugin.Abstractions;

/// <summary>JTSA が読み込む拡張機能の共通契約。</summary>
public interface IJtsaPlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Version Version { get; }

    void Initialize(IJtsaPluginContext context);
    void Open();
    void Shutdown();
}

/// <summary>JTSA から拡張機能へ渡す、UI 実装に依存しない機能。</summary>
public interface IJtsaPluginContext
{
    string PluginDirectory { get; }
    string DataDirectory { get; }
    nint MainWindowHandle { get; }
    IReadOnlyList<ChannelPointRewardInfo> GetChannelPointRewards();
    event Action<ChannelPointRedemptionInfo>? ChannelPointRedeemed;

    void Log(string message);
    void LogError(string message, Exception? exception = null);
    void SetExpansionOverlay(ExpansionOverlayContent content);
    void RemoveExpansionOverlay(string id);
}

/// <summary>配信拡張のイベント（チャット・チャネポ・フォローなど）を受け取るプラグイン向けの追加機能。</summary>
/// <remarks>既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。</remarks>
public interface IJtsaExpansionTriggerPluginContext
{
    /// <summary>配信拡張ルールの一致判定より前に、すべてのイベントで発火します。</summary>
    event Action<ExpansionTriggerInfo>? ExpansionTriggered;
}

/// <summary>プラグインのローカル画像・動画・音声を配信拡張で表示するための追加機能。</summary>
/// <remarks>既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。</remarks>
public interface IJtsaExpansionMediaPluginContext
{
    /// <summary>ファイルを配信対象に登録し、配信拡張の HTML から参照できる URL を返します。</summary>
    string GetExpansionMediaUrl(string filePath);
}

/// <summary>JTSA のカレンダー予定を参照するプラグイン向けの追加機能。</summary>
/// <remarks>
/// 既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。
/// 利用側は <c>context is IJtsaCalendarPluginContext</c> で対応状況を確認してください。
/// </remarks>
public interface IJtsaCalendarPluginContext
{
    IReadOnlyList<CalendarEntryInfo> GetCalendarEntries(DateTime from, DateTime toExclusive);
}

/// <summary>JTSA で編集中の配信情報を参照するプラグイン向けの追加機能。</summary>
/// <remarks>
/// 既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。
/// </remarks>
public interface IJtsaStreamMetadataPluginContext
{
    StreamMetadataInfo GetCurrentStreamMetadata();
}

/// <summary>外部サービスのコメントを JTSA のチャット欄へ渡すプラグイン向けの追加機能。</summary>
public interface IJtsaExternalChatPluginContext
{
    void AddExternalChatMessage(ExternalChatMessageInfo message);
}

/// <summary>スマホ画面に独自のパネルを追加するプラグイン向けの追加機能。</summary>
/// <remarks>共通コンテキストとのバイナリ互換性を保つため、別インターフェースとして提供します。</remarks>
public interface IJtsaRemotePanelPluginContext
{
    /// <summary>IDが同じパネルは更新します。内容はスマホ画面の次回更新時に反映されます。</summary>
    void SetRemotePanel(RemotePluginPanelContent panel);
    /// <summary>このプラグインが登録したパネルを取り除きます。</summary>
    void RemoveRemotePanel(string id);
    /// <summary>スマホからの操作と現在状態を持つパネルを登録します。コールバックはUIスレッドで実行されます。</summary>
    void SetInteractiveRemotePanel(RemotePluginPanelContent panel, Func<string, string?, bool> onAction, Func<string> getState);
}

/// <summary>スマホ画面で表示するプラグインのパネル。HTMLは隔離されたiframe内に表示されます。</summary>
public sealed record RemotePluginPanelContent(string Id, string Title, string Html);

public sealed record StreamMetadataInfo(
    string Title,
    string CategoryId,
    string CategoryName);

public sealed record ExternalChatMessageInfo(
    string Platform,
    string MessageId,
    string UserId,
    string UserName,
    string DisplayName,
    string Message,
    string ProfileImageUrl = "",
    string UserColor = "#FFFFFF");

public sealed record CalendarEntryInfo(
    long Id,
    DateTime Date,
    TimeSpan StartTime,
    string Content,
    string TitlePlaceholder,
    string CategoryName,
    string CategoryBoxArtUrl,
    string ResolvedTitle);

public sealed record ChannelPointRewardInfo(
    string Id,
    string Title,
    bool IsUserInputRequired);

public sealed record ChannelPointRedemptionInfo(
    string RewardId,
    string UserName,
    string UserInput);

public sealed record ExpansionTriggerInfo(
    string TriggerType,
    string Value);

/// <summary>既存の配信拡張ブラウザソース上に表示するプラグイン描画。</summary>
public sealed record ExpansionOverlayContent(
    string Id,
    string Html,
    int X,
    int Y,
    int Width,
    int Height);
