using System.Text.Json;

namespace JTSA.Plugin.Abstractions;

/// <summary>JTSA が接続している OBS を操作するプラグイン向けの追加機能。</summary>
/// <remarks>未接続時や OBS が操作を拒否した場合、タスクは例外で終了します。</remarks>
public interface IJtsaObsPluginContext
{
    bool IsObsConnected(bool isSub = false);

    /// <summary>接続中の OBS のシーン名を名前順で取得します。</summary>
    Task<IReadOnlyList<string>> GetObsSceneNamesAsync(bool isSub = false);

    /// <summary>OBS 全体で共有されるテキストソースの文言を更新します。</summary>
    Task SetObsTextAsync(string sourceName, string text, bool isSub = false);

    /// <summary>シーンまたはグループ内のアイテムと ID を取得します。</summary>
    Task<IReadOnlyList<ObsSceneItemInfo>> GetObsSceneItemsAsync(
        string sceneOrGroupName, bool isGroup = false, bool isSub = false);

    /// <summary>アイテムの X、Y 座標を OBS のキャンバス単位で更新します。</summary>
    Task SetObsSceneItemPositionAsync(
        string sceneOrGroupName, int sceneItemId, double x, double y, bool isSub = false);

    /// <summary>新規ソースをシーンに追加し、そのシーン内のアイテム ID を返します。</summary>
    /// <param name="sceneName">追加先のシーン名。</param>
    /// <param name="sourceName">OBS 内で一意の新規ソース名。</param>
    /// <param name="inputKind">OBS のソース種別 ID。</param>
    /// <param name="settings">種別固有の設定を表す JSON オブジェクト。省略すると既定値を使用します。</param>
    /// <param name="enabled">追加直後の表示状態。</param>
    /// <param name="isSub">サブ OBS を操作する場合は true。</param>
    Task<int> CreateObsSourceAsync(
        string sceneName, string sourceName, string inputKind, JsonElement? settings = null,
        bool enabled = true, bool isSub = false);
}

public sealed record ObsSceneItemInfo(int SceneItemId, string SourceName)
{
    /// <summary>OBS のソース分類。例: OBS_SOURCE_TYPE_INPUT、OBS_SOURCE_TYPE_SCENE。</summary>
    public string? SourceType { get; init; }

    /// <summary>入力ソースの種類 ID。例: browser_source。シーンやグループでは null。</summary>
    public string? InputKind { get; init; }
}
