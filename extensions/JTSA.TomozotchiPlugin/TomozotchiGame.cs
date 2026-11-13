namespace JTSA.TomozotchiPlugin;

public sealed record TomozotchiNotice(string Message, string Kind, DateTime StartedAtUtc);

public sealed record TomozotchiResult(bool Success, string Message);

/// <summary>ゲージの値・時間減少・チャネポアクションを扱う。UI スレッドから呼ぶ前提でロックしない。</summary>
public sealed class TomozotchiGame
{
    public static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(3.3);

    private readonly Dictionary<string, int> values = [];
    private readonly Dictionary<string, (int Interval, DateTime Next)> decay = [];
    private readonly Func<DateTime> clock;

    public TomozotchiGame(GameConfig config, Func<DateTime>? clock = null)
    {
        Config = config;
        Config.ChannelPoints ??= new();
        this.clock = clock ?? (() => DateTime.UtcNow);
        Sync();
    }

    public GameConfig Config { get; }
    public TomozotchiNotice? Notice { get; private set; }

    /// <summary>ゲージ定義かチャネポ設定が変わった（保存が必要）。</summary>
    public event Action? ConfigChanged;
    /// <summary>表示内容が変わった。</summary>
    public event Action? Changed;

    public List<RewardAction> RewardActions => Config.ChannelPoints!.RewardActions;

    public int ValueOf(string name) => values.TryGetValue(name, out var value) ? value : 0;

    public StatDefinition? Find(string name) => Config.Stats.FirstOrDefault(stat => stat.Name == name);

    public void Tick()
    {
        var now = clock();
        var changed = false;
        foreach (var stat in Config.Stats)
        {
            if (!decay.TryGetValue(stat.Name, out var entry)) continue;
            var period = TimeSpan.FromMinutes(entry.Interval);
            while (now >= entry.Next)
            {
                if (values[stat.Name] > 0)
                {
                    values[stat.Name]--;
                    changed = true;
                }
                entry.Next += period;
            }
            decay[stat.Name] = entry;
        }

        if (Notice is not null && now - Notice.StartedAtUtc >= NoticeDuration)
        {
            Notice = null;
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    /// <param name="fromReward">チャネポ由来なら回復/減少の通知、手動なら変更後の値を通知する。</param>
    public TomozotchiResult Change(string name, int amount, bool fromReward = false)
    {
        var stat = Find(name);
        if (stat is null) return new(false, $"対象ゲージ \"{name}\" が存在しません");
        var old = values[name];
        values[name] = Math.Clamp(old + amount, 0, stat.MaxHearts);
        var actual = values[name] - old;
        if (fromReward)
        {
            if (actual > 0) Notify($"{name} +{actual} 回復！", "recover");
            else if (actual < 0) Notify($"{name} {actual} 減少…", "modify");
        }
        else
        {
            Notify($"{name} {(actual >= 0 ? "+" : "")}{actual} → {values[name]}/{stat.MaxHearts}", "modify");
        }
        Changed?.Invoke();
        var text = amount > 0 ? $"{amount}回復" : $"{Math.Abs(amount)}減少";
        return new(true, $"{name}を{text}");
    }

    public TomozotchiResult SetValue(string name, int value)
    {
        var stat = Find(name);
        if (stat is null) return new(false, $"ゲージ \"{name}\" が見つかりません");
        values[name] = Math.Clamp(value, 0, stat.MaxHearts);
        Notify($"{name} → {values[name]}/{stat.MaxHearts}", "set");
        Changed?.Invoke();
        return new(true, $"{name}を{values[name]}にしました");
    }

    public void Reset(string name)
    {
        var stat = Find(name);
        if (stat is null) return;
        values[name] = stat.Initial;
        Notify($"{name} リセット！", "reset");
        Changed?.Invoke();
    }

    public void ResetAll()
    {
        foreach (var stat in Config.Stats) values[stat.Name] = stat.Initial;
        Notify("全ゲージ リセット！", "reset");
        Changed?.Invoke();
    }

    public TomozotchiResult AddStat(StatDefinition definition)
    {
        var error = definition.Validate();
        if (error is not null) return new(false, error);
        if (Find(definition.Name) is not null) return new(false, $"ゲージ \"{definition.Name}\" は既に存在します");
        Config.Stats.Add(definition.Clone());
        ConfigUpdated();
        return new(true, $"ゲージ \"{definition.Name}\" を追加しました");
    }

    public TomozotchiResult UpdateStat(string oldName, StatDefinition definition)
    {
        var index = Config.Stats.FindIndex(stat => stat.Name == oldName);
        if (index < 0) return new(false, $"ゲージ \"{oldName}\" が見つかりません");
        var error = definition.Validate();
        if (error is not null) return new(false, error);
        if (definition.Name != oldName && Find(definition.Name) is not null)
            return new(false, $"ゲージ \"{definition.Name}\" は既に存在します");

        Config.Stats[index] = definition.Clone();
        if (definition.Name != oldName)
        {
            values[definition.Name] = values[oldName];
            values.Remove(oldName);
            if (decay.Remove(oldName, out var entry)) decay[definition.Name] = entry;
            foreach (var action in RewardActions.Where(action => action.Stat == oldName))
                action.Stat = definition.Name;
        }
        ConfigUpdated();
        return new(true, $"ゲージ \"{definition.Name}\" を更新しました");
    }

    /// <summary>チャネポの割り当ては残す（再追加で再び効くように。元アプリと同じ）。</summary>
    public TomozotchiResult RemoveStat(string name)
    {
        if (Config.Stats.RemoveAll(stat => stat.Name == name) == 0)
            return new(false, $"ゲージ \"{name}\" が見つかりません");
        ConfigUpdated();
        return new(true, $"ゲージ \"{name}\" を削除しました");
    }

    public void Move(string name, int delta)
    {
        var index = Config.Stats.FindIndex(stat => stat.Name == name);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Config.Stats.Count) return;
        (Config.Stats[index], Config.Stats[target]) = (Config.Stats[target], Config.Stats[index]);
        ConfigUpdated(notify: false);
    }

    /// <summary>プリセット等の読み込み。同名ゲージの値は引き継ぐ。</summary>
    public void ReplaceStats(IEnumerable<StatDefinition> stats, IEnumerable<RewardAction>? rewardActions = null)
    {
        Config.Stats.Clear();
        Config.Stats.AddRange(stats
            .Where(stat => stat.Validate() is null)
            .GroupBy(stat => stat.Name)
            .Select(group => group.First().Clone()));
        if (rewardActions is not null)
        {
            RewardActions.Clear();
            RewardActions.AddRange(rewardActions);
        }
        ConfigUpdated();
    }

    /// <summary>リワード 1 件につきアクションは 1 件。同じリワードは置き換える。</summary>
    public void SetRewardAction(RewardAction action)
    {
        RewardActions.RemoveAll(item => item.RewardId == action.RewardId);
        RewardActions.Add(action);
        ConfigChanged?.Invoke();
    }

    public void RemoveRewardAction(string rewardId)
    {
        if (RewardActions.RemoveAll(item => item.RewardId == rewardId) > 0)
            ConfigChanged?.Invoke();
    }

    /// <returns>アクションが未設定なら null。</returns>
    public TomozotchiResult? FireReward(string rewardId)
    {
        var action = RewardActions.FirstOrDefault(item => item.RewardId == rewardId);
        if (action is null) return null;
        return action.Action switch
        {
            RewardActionTypes.Modify when action.Amount is { } amount and not 0 && !string.IsNullOrEmpty(action.Stat)
                => Change(action.Stat, amount, fromReward: true),
            RewardActionTypes.AddStat when action.StatDef is not null => AddStat(action.StatDef),
            RewardActionTypes.RemoveStat when !string.IsNullOrEmpty(action.Stat) => RemoveStat(action.Stat),
            _ => new(false, $"アクションの設定が不正です: {action.Action}")
        };
    }

    private void ConfigUpdated(bool notify = true)
    {
        Sync();
        if (notify) Notify("設定が更新されました！", "config");
        ConfigChanged?.Invoke();
        Changed?.Invoke();
    }

    private void Notify(string message, string kind) => Notice = new(message, kind, clock());

    private void Sync()
    {
        var now = clock();
        var names = Config.Stats.Select(stat => stat.Name).ToHashSet();
        foreach (var name in values.Keys.Where(name => !names.Contains(name)).ToArray()) values.Remove(name);
        foreach (var name in decay.Keys.Where(name => !names.Contains(name)).ToArray()) decay.Remove(name);

        foreach (var stat in Config.Stats)
        {
            values[stat.Name] = values.TryGetValue(stat.Name, out var value)
                ? Math.Min(value, stat.MaxHearts)
                : stat.Initial;

            if (stat.DecreaseInterval <= 0)
                decay.Remove(stat.Name);
            else if (!decay.TryGetValue(stat.Name, out var entry) || entry.Interval != stat.DecreaseInterval)
                decay[stat.Name] = (stat.DecreaseInterval, now.AddMinutes(stat.DecreaseInterval));
        }
    }
}
