using JTSA.Plugin.Abstractions;
using System.Windows;
using System.Windows.Threading;

namespace JTSA.TomozotchiPlugin;

public sealed class TomozotchiPlugin : IJtsaPlugin
{
    private const string OverlayId = "main";
    // ponytail: 全報酬を 10 秒ごとに取得（Twitch API 2 回＋TODO 対象数）。増えたら対象リワードだけに絞る。
    private const int StatusPollSeconds = 10;

    private IJtsaPluginContext? context;
    private DispatcherTimer? timer;
    private TomozotchiWindow? window;
    private int secondsUntilPoll;
    private bool polling;

    public string Id => "jtsa.tomozotchi";
    public string Name => "ともぞっち";
    public string Description => "時間で減るゲージを配信拡張に表示し、チャネポ交換で増減させます。";
    public Version Version => new(1, 0, 0);

    public TomozotchiSettings Settings { get; private set; } = new();
    public TomozotchiGame Game { get; private set; } = null!;
    public ChannelPointBoard Board { get; } = new();
    /// <summary>本体がチャネポ状況 API に対応していなければ null（配布版 v1.5.41 まで）。</summary>
    public ChannelPointStatusSource? StatusSource { get; private set; }

    /// <summary>チャネポ状況（TODO 一覧など）が変わった。</summary>
    public event Action? BoardChanged;

    public void Initialize(IJtsaPluginContext pluginContext)
    {
        context = pluginContext;
        Settings = TomozotchiStore.Load(pluginContext.DataDirectory);
        Game = new TomozotchiGame(Settings.Game);
        Game.Changed += UpdateOverlay;
        Game.ConfigChanged += SaveSettings;
        pluginContext.ChannelPointRedeemed += OnChannelPointRedeemed;
        StatusSource = ChannelPointStatusSource.TryCreate(pluginContext);

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => OnTick();
        timer.Start();
        UpdateOverlay();
        pluginContext.Log("ともぞっちを読み込みました。");
    }

    public void Open()
    {
        if (window is { IsLoaded: true })
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window = new TomozotchiWindow(this, context!);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Shutdown()
    {
        timer?.Stop();
        timer = null;
        if (context is not null)
        {
            context.ChannelPointRedeemed -= OnChannelPointRedeemed;
            context.RemoveExpansionOverlay(OverlayId);
        }
        window?.Close();
        window = null;
    }

    public void SaveSettings()
    {
        if (context is null) return;
        try
        {
            TomozotchiStore.Save(context.DataDirectory, Settings);
        }
        catch (Exception ex)
        {
            context.LogError("ともぞっちの設定を保存できませんでした。", ex);
        }
    }

    private void OnTick()
    {
        Game.Tick();
        if (--secondsUntilPoll <= 0)
        {
            secondsUntilPoll = StatusPollSeconds;
            _ = PollStatusAsync();
        }
        // クールダウンの残り時間を毎秒進める（内容が同じなら本体側で送信を省く）
        UpdateOverlay();
    }

    /// <summary>リワードの状態と、JTSA で作ったリワードの未処理の交換を取り直す。</summary>
    public async Task PollStatusAsync()
    {
        var hooks = Game.Config.ChannelPoints!;
        if (StatusSource is null || polling ||
            hooks.TodoRewardIds.Count + hooks.CooldownRewardIds.Count + hooks.RedeemableRewardIds.Count == 0)
            return;
        polling = true;
        try
        {
            Board.SetStatuses(await StatusSource.GetRewardStatusesAsync());
            foreach (var rewardId in hooks.TodoRewardIds.ToArray())
                if (Board.Find(rewardId) is { IsManageable: true })
                    Board.ReplaceTodos(rewardId, await StatusSource.GetUnfulfilledAsync(rewardId));
            BoardChanged?.Invoke();
            UpdateOverlay();
        }
        catch (Exception ex)
        {
            context?.LogError("チャネポ状況を取得できませんでした。", ex);
        }
        finally
        {
            polling = false;
        }
    }

    /// <summary>TODO を完了（または取り消し）する。JTSA で作ったリワードは Twitch 側も更新する。</summary>
    public async Task<string> CompleteTodoAsync(TodoItem todo, bool fulfilled)
    {
        var manageable = Board.Find(todo.RewardId) is { IsManageable: true };
        if (manageable && StatusSource is not null &&
            !await StatusSource.CompleteAsync(todo.RewardId, todo.Id, fulfilled))
            return "Twitch 側の更新に失敗しました。";
        Board.RemoveTodo(todo.Id);
        BoardChanged?.Invoke();
        UpdateOverlay();
        var action = fulfilled ? "完了" : "キャンセル（ポイント返却）";
        return manageable ? $"{todo.RewardTitle} を{action}しました" : $"{todo.RewardTitle} を一覧から消しました";
    }

    public void UpdateOverlay()
    {
        context?.SetExpansionOverlay(new ExpansionOverlayContent(
            OverlayId,
            TomozotchiOverlay.Render(Game, Board, DateTimeOffset.UtcNow),
            Settings.OverlayX,
            Settings.OverlayY,
            Settings.OverlayWidth,
            Settings.OverlayHeight));
    }

    // イベントは UI スレッド以外から来る可能性があるため、ゲーム操作は UI スレッドへ集める
    private void OnChannelPointRedeemed(ChannelPointRedemptionInfo info) =>
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (timer is null) return;
            AddTodo(info);
            var result = Game.FireReward(info.RewardId);
            if (result is null) return;
            var message = $"チャネポ by {info.UserName}: {result.Message}";
            if (result.Success) context?.Log(message);
            else context?.LogError(message);
        });

    private void AddTodo(ChannelPointRedemptionInfo info)
    {
        if (!Game.Config.ChannelPoints!.TodoRewardIds.Contains(info.RewardId)) return;
        // 古い本体では交換 ID が無いので、一覧から消すための仮 ID を付ける
        var id = ChannelPointStatusSource.RedemptionIdOf(info) ?? Guid.NewGuid().ToString();
        var title = Board.Find(info.RewardId)?.Title ?? RewardTitle(info.RewardId);
        Board.AddTodo(new TodoItem(id, info.RewardId, title, info.UserName, info.UserInput, DateTimeOffset.UtcNow));
        BoardChanged?.Invoke();
        UpdateOverlay();
    }

    private string RewardTitle(string rewardId)
    {
        try
        {
            return context?.GetChannelPointRewards().FirstOrDefault(reward => reward.Id == rewardId)?.Title ?? rewardId;
        }
        catch
        {
            return rewardId;
        }
    }
}
