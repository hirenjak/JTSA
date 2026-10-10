using System.Collections;
using System.Globalization;
using System.Reflection;

namespace JTSA.TomozotchiPlugin;

public sealed record RewardStatus(
    string Id,
    string Title,
    string ImageUrl,
    bool IsEnabled,
    bool IsPaused,
    bool IsInStock,
    DateTimeOffset? CooldownExpiresAt,
    bool IsManageable)
{
    public bool IsCoolingDown(DateTimeOffset now) => CooldownExpiresAt > now;
    public bool IsRedeemable(DateTimeOffset now) => IsEnabled && !IsPaused && IsInStock && !IsCoolingDown(now);
}

public sealed record TodoItem(string Id, string RewardId, string RewardTitle, string UserName, string UserInput, DateTimeOffset RedeemedAt);

/// <summary>
/// 本体の IJtsaChannelPointStatusPluginContext をリフレクションで呼ぶ。
/// 配布版の JTSA には型が無く、直接参照すると読み込みに失敗するため。
/// </summary>
public sealed class ChannelPointStatusSource
{
    private readonly object context;
    private readonly MethodInfo getStatuses;
    private readonly MethodInfo getUnfulfilled;
    private readonly MethodInfo complete;

    private ChannelPointStatusSource(object context, MethodInfo getStatuses, MethodInfo getUnfulfilled, MethodInfo complete)
    {
        this.context = context;
        this.getStatuses = getStatuses;
        this.getUnfulfilled = getUnfulfilled;
        this.complete = complete;
    }

    /// <summary>本体が対応していなければ null。</summary>
    public static ChannelPointStatusSource? TryCreate(object context)
    {
        var type = context.GetType();
        return type.GetMethod("GetChannelPointRewardStatusesAsync", Type.EmptyTypes) is { } statuses &&
               type.GetMethod("GetUnfulfilledRedemptionsAsync", [typeof(string)]) is { } unfulfilled &&
               type.GetMethod("CompleteRedemptionAsync", [typeof(string), typeof(string), typeof(bool)]) is { } complete
            ? new ChannelPointStatusSource(context, statuses, unfulfilled, complete)
            : null;
    }

    public async Task<IReadOnlyList<RewardStatus>> GetRewardStatusesAsync() =>
        Items(await Invoke(getStatuses)).Select(item => new RewardStatus(
            Read<string>(item, "Id") ?? "",
            Read<string>(item, "Title") ?? "",
            Read<string>(item, "ImageUrl") ?? "",
            Read<bool>(item, "IsEnabled"),
            Read<bool>(item, "IsPaused"),
            Read<bool>(item, "IsInStock"),
            Read<DateTimeOffset?>(item, "CooldownExpiresAt"),
            Read<bool>(item, "IsManageable"))).ToArray();

    public async Task<IReadOnlyList<TodoItem>> GetUnfulfilledAsync(string rewardId) =>
        Items(await Invoke(getUnfulfilled, rewardId)).Select(item => new TodoItem(
            Read<string>(item, "Id") ?? "",
            Read<string>(item, "RewardId") ?? "",
            Read<string>(item, "RewardTitle") ?? "",
            Read<string>(item, "UserName") ?? "",
            Read<string>(item, "UserInput") ?? "",
            Read<DateTimeOffset>(item, "RedeemedAt"))).ToArray();

    public async Task<bool> CompleteAsync(string rewardId, string redemptionId, bool fulfilled) =>
        await Invoke(complete, rewardId, redemptionId, fulfilled) is true;

    private async Task<object?> Invoke(MethodInfo method, params object[] args)
    {
        var task = (Task)method.Invoke(context, args)!;
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static IEnumerable<object> Items(object? list) => (list as IEnumerable)?.Cast<object>() ?? [];

    private static T? Read<T>(object item, string name) =>
        item.GetType().GetProperty(name)?.GetValue(item) is T value ? value : default;

    /// <summary>新しい本体では交換イベントに交換 ID が付く。古い本体では null。</summary>
    public static string? RedemptionIdOf(object redemption) =>
        redemption.GetType().GetProperty("RedemptionId")?.GetValue(redemption) as string is { Length: > 0 } id ? id : null;
}

/// <summary>配信拡張に出すチャネポ状況（クールダウン・交換可能・TODO）。UI スレッドから使う。</summary>
public sealed class ChannelPointBoard
{
    private readonly Dictionary<string, TodoItem> todos = [];
    private readonly Dictionary<string, DateTimeOffset> firstSeen = [];
    private IReadOnlyList<RewardStatus> statuses = [];

    public IReadOnlyList<RewardStatus> Statuses => statuses;
    public bool HasStatuses { get; private set; }

    public void SetStatuses(IReadOnlyList<RewardStatus> value)
    {
        statuses = value;
        HasStatuses = true;
    }

    public RewardStatus? Find(string rewardId) => statuses.FirstOrDefault(status => status.Id == rewardId);

    /// <summary>API で取れた未処理一覧で、そのリワードの TODO を置き換える（他で完了したものが消える）。</summary>
    public void ReplaceTodos(string rewardId, IEnumerable<TodoItem> items)
    {
        foreach (var id in todos.Values.Where(item => item.RewardId == rewardId).Select(item => item.Id).ToArray())
            todos.Remove(id);
        foreach (var item in items) todos[item.Id] = item;
    }

    public void AddTodo(TodoItem item) => todos[item.Id] = item;

    public bool RemoveTodo(string id) => todos.Remove(id);

    public IReadOnlyList<TodoItem> Todos => todos.Values.OrderBy(item => item.RedeemedAt).ToArray();

    public IReadOnlyList<RewardStatus> Cooldowns(ChannelPointHooks hooks, DateTimeOffset now) =>
        statuses.Where(status => hooks.CooldownRewardIds.Contains(status.Id) && status.IsCoolingDown(now)).ToArray();

    public IReadOnlyList<RewardStatus> Redeemables(ChannelPointHooks hooks, DateTimeOffset now) =>
        statuses.Where(status => hooks.RedeemableRewardIds.Contains(status.Id) && status.IsRedeemable(now)).ToArray();

    /// <summary>
    /// 項目が最初に表示された時刻。描画のたびに innerHTML が差し替わっても
    /// 登場アニメーションをやり直さないよう data-jtsa-animation-start に使う。
    /// </summary>
    public DateTimeOffset FirstSeen(string key, DateTimeOffset now)
    {
        if (!firstSeen.TryGetValue(key, out var time)) firstSeen[key] = time = now;
        return time;
    }

    /// <summary>今回表示しなかった項目の登場時刻を忘れる（次に出たときに再びアニメーションする）。</summary>
    public void ForgetExcept(IReadOnlySet<string> shownKeys)
    {
        foreach (var key in firstSeen.Keys.Where(key => !shownKeys.Contains(key)).ToArray())
            firstSeen.Remove(key);
    }

    /// <summary>残り時間を「4m05s」「12s」の形にする。</summary>
    public static string FormatRemaining(TimeSpan remaining)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        return seconds >= 60
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}m{seconds % 60:00}s")
            : $"{seconds}s";
    }
}
