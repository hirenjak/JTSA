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

/// <summary>チャンネルポイント報酬の利用状況（クールダウン・交換可否）と未処理の交換を扱うプラグイン向けの追加機能。</summary>
/// <remarks>
/// 既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。
/// 呼ぶたびに Twitch API へ問い合わせるため、定期取得は数秒以上の間隔を空けてください。
/// </remarks>
public interface IJtsaChannelPointStatusPluginContext
{
    /// <summary>全報酬の現在の状態を取得します。取得できなかった場合は空です。</summary>
    Task<IReadOnlyList<ChannelPointRewardStatusInfo>> GetChannelPointRewardStatusesAsync();

    /// <summary>報酬の未処理の交換を古い順に取得します。JTSA が作成した報酬のみ取得できます。</summary>
    Task<IReadOnlyList<ChannelPointPendingRedemptionInfo>> GetUnfulfilledRedemptionsAsync(string rewardId);

    /// <summary>交換を完了（true）またはキャンセル（false、ポイント返却）にします。成功したら true。</summary>
    Task<bool> CompleteRedemptionAsync(string rewardId, string redemptionId, bool fulfilled);
}

/// <summary>プラグインのローカル画像・動画・音声を配信拡張で表示するための追加機能。</summary>
/// <remarks>既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。</remarks>
public interface IJtsaExpansionMediaPluginContext
{
    /// <summary>ファイルを配信対象に登録し、配信拡張の HTML から参照できる URL を返します。</summary>
    string GetExpansionMediaUrl(string filePath);
}

/// <summary>配信拡張と同じアクション（チャット・Twitchコマンド・OBS・VTS）を実行するプラグイン向けの追加機能。</summary>
/// <remarks>既存プラグインとのバイナリ互換性を保つため、共通コンテキストとは別インターフェースです。失敗は本体のログに出し、例外は投げません。</remarks>
public interface IJtsaExpansionActionPluginContext
{
    Task SendChatAsync(string message);
    Task ShoutoutAsync(string userLogin);
    Task GrantVipAsync(string userLogin);
    /// <summary>userLogin が {selected_account} なら選択中のアカウントのクリップを再生します。</summary>
    Task PlayClipAsync(string userLogin, int rankingLimit);
    Task SetObsTextAsync(bool isSubObs, string sourceName, string text);
    Task TriggerVtsHotkeyAsync(string hotkeyId);
    /// <summary>VTS の現在のモデルのホットキー（ID, 名前）。未接続なら例外を投げます。</summary>
    Task<IReadOnlyList<ExpansionNamedOption>> GetVtsHotkeysAsync();
    /// <summary>OBS のシーン名。接続できなければ例外を投げます。</summary>
    Task<IReadOnlyList<string>> GetObsScenesAsync(bool isSubObs);
    /// <summary>シーンのソース（グループ内も含む）。textOnly ならテキストソースだけ。接続できなければ例外を投げます。</summary>
    Task<IReadOnlyList<ExpansionObsSource>> GetObsSourcesAsync(bool isSubObs, string sceneName, bool textOnly);
    /// <summary>ソースを表示（true）・非表示（false）・切り替え（null）にします。allScenes なら全シーン・グループの同名ソースをまとめて変えます。</summary>
    Task SetObsSourceVisibleAsync(bool isSubObs, string sceneName, string sourceName, string containerName, bool? visible, bool allScenes);
    /// <summary>音声を持つ入力の名前（シーンに無い入力も含む）。接続できなければ例外を投げます。</summary>
    Task<IReadOnlyList<string>> GetObsAudioInputsAsync(bool isSubObs);
    /// <summary>入力の音量を変えます。volume は 0〜1 の倍率（OBS の % 表示の 1/100）。</summary>
    Task SetObsInputVolumeAsync(bool isSubObs, string inputName, double volume);
}

public sealed record ExpansionNamedOption(string Id, string Name);

/// <summary>OBS のソース。ContainerName はグループ内なら グループ名、シーン直下なら空。</summary>
public sealed record ExpansionObsSource(string SourceName, string ContainerName);

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

/// <summary>チャット画面のToDo管理へ項目を追加するプラグイン向けの追加機能。</summary>
/// <remarks>共通コンテキストとのバイナリ互換性を保つため、別インターフェースとして提供します。</remarks>
public interface IJtsaTodoPluginContext
{
    /// <summary>現在のカテゴリのToDoに追加します。未接続や空文字などで追加できなければ false を返します。</summary>
    bool AddTodo(string text);
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
    string UserInput)
{
    /// <summary>交換ID。未処理の交換を完了・キャンセルするときに使います。古いホストでは空です。</summary>
    public string RedemptionId { get; init; } = "";

    /// <summary>交換したユーザーのログイン名。対応していない本体では空です。</summary>
    public string UserLogin { get; init; } = "";
}

/// <summary>チャンネルポイント報酬の利用状況。</summary>
/// <param name="ImageUrl">配信者が設定した報酬アイコンの URL。未設定（Twitch の既定アイコン）なら空。</param>
/// <param name="GlobalCooldownSeconds">クールダウンの設定秒数。クールダウン無しなら 0。</param>
/// <param name="CooldownExpiresAt">クールダウン中なら終了時刻、そうでなければ null。</param>
/// <param name="RedemptionsRedeemedCurrentStream">今の配信での交換回数。配信していないときは null。</param>
/// <param name="IsManageable">JTSA が作成した報酬か。未処理の交換の取得・完了はこの報酬だけできます。</param>
public sealed record ChannelPointRewardStatusInfo(
    string Id,
    string Title,
    int Cost,
    string ImageUrl,
    bool IsEnabled,
    bool IsPaused,
    bool IsInStock,
    int GlobalCooldownSeconds,
    DateTimeOffset? CooldownExpiresAt,
    int? RedemptionsRedeemedCurrentStream,
    bool IsManageable)
{
    /// <summary>指定時刻にクールダウン中か。</summary>
    public bool IsCoolingDown(DateTimeOffset now) => CooldownExpiresAt > now;

    /// <summary>1配信あたりの交換回数の上限。上限なしなら 0。</summary>
    public int MaxPerStream { get; init; }

    /// <summary>今の配信で交換回数の上限に達しているか。</summary>
    public bool IsMaxPerStreamReached => MaxPerStream > 0 && RedemptionsRedeemedCurrentStream >= MaxPerStream;

    /// <summary>指定時刻に視聴者が交換できるか（有効・一時停止でない・在庫あり・上限未達・クールダウン中でない）。</summary>
    public bool IsRedeemable(DateTimeOffset now) =>
        IsEnabled && !IsPaused && IsInStock && !IsMaxPerStreamReached && !IsCoolingDown(now);
}

/// <summary>未処理（UNFULFILLED）のチャンネルポイント交換。</summary>
public sealed record ChannelPointPendingRedemptionInfo(
    string Id,
    string RewardId,
    string RewardTitle,
    string UserName,
    string UserInput,
    DateTimeOffset RedeemedAt);

public sealed record ExpansionTriggerInfo(
    string TriggerType,
    string Value)
{
    /// <summary>チャット・チャネポなど、きっかけのユーザーのログイン名。分からなければ空です。</summary>
    public string UserLogin { get; init; } = "";
}

/// <summary>既存の配信拡張ブラウザソース上に表示するプラグイン描画。</summary>
public sealed record ExpansionOverlayContent(
    string Id,
    string Html,
    int X,
    int Y,
    int Width,
    int Height);
