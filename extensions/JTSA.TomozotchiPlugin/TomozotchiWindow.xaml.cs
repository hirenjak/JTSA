using System.IO;
using System.Windows;
using System.Windows.Controls;
using JTSA.Plugin.Abstractions;
using Microsoft.Win32;

namespace JTSA.TomozotchiPlugin;

public partial class TomozotchiWindow : Window
{
    private sealed record Row(string Key, string Label);

    private readonly TomozotchiPlugin plugin;
    private readonly IJtsaPluginContext context;
    private IReadOnlyList<ChannelPointRewardInfo> rewards = [];
    private bool reloadingStats;

    private TomozotchiGame Game => plugin.Game;

    public TomozotchiWindow(TomozotchiPlugin plugin, IJtsaPluginContext context)
    {
        this.plugin = plugin;
        this.context = context;
        InitializeComponent();
        ActionComboBox.SelectedIndex = 0;
        OverlayXTextBox.Text = plugin.Settings.OverlayX.ToString();
        OverlayYTextBox.Text = plugin.Settings.OverlayY.ToString();
        OverlayWidthTextBox.Text = plugin.Settings.OverlayWidth.ToString();
        OverlayHeightTextBox.Text = plugin.Settings.OverlayHeight.ToString();
        FillStatForm(null);
        ReloadRewards();
        ReloadStats();
        ReloadActions();
        ReloadPresets();
        Game.Changed += ReloadStats;
        Game.ConfigChanged += ReloadActions;
        Closed += (_, _) =>
        {
            Game.Changed -= ReloadStats;
            Game.ConfigChanged -= ReloadActions;
        };
    }

    // ===== ゲージ =====

    private string? SelectedStat => (StatsListBox.SelectedItem as Row)?.Key;

    private void ReloadStats()
    {
        var selected = SelectedStat;
        reloadingStats = true;
        StatsListBox.ItemsSource = Game.Config.Stats.Select(stat => new Row(
            stat.Name,
            $"{stat.Name}　{Game.ValueOf(stat.Name)}/{stat.MaxHearts}　{stat.MarkFull}　"
            + (stat.DecreaseInterval == 0 ? "減少しない" : $"{stat.DecreaseInterval}分ごとに減少"))).ToList();
        StatsListBox.SelectedItem = StatsListBox.Items.Cast<Row>().FirstOrDefault(row => row.Key == selected);
        reloadingStats = false;
    }

    private void StatsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 値の変化で一覧を作り直したときに入力中のフォームを上書きしない
        if (reloadingStats) return;
        if (SelectedStat is { } name) FillStatForm(Game.Find(name));
    }

    private void FillStatForm(StatDefinition? stat)
    {
        NameTextBox.Text = stat?.Name ?? "";
        MaxTextBox.Text = (stat?.MaxHearts ?? 3).ToString();
        InitialTextBox.Text = stat?.InitialHearts?.ToString() ?? "";
        IntervalTextBox.Text = (stat?.DecreaseInterval ?? 30).ToString();
        MarkTextBox.Text = stat?.MarkFull ?? "♥";
    }

    private StatDefinition? ReadStatForm(TextBox name, TextBox max, TextBox initial, TextBox interval, TextBox mark)
    {
        if (!int.TryParse(max.Text, out var maxValue)) { Status("最大値は整数で入力してください"); return null; }
        int? initialValue = null;
        if (!string.IsNullOrWhiteSpace(initial.Text))
        {
            if (!int.TryParse(initial.Text, out var parsed)) { Status("初期値は整数で入力してください"); return null; }
            initialValue = parsed;
        }
        if (!int.TryParse(interval.Text, out var intervalValue)) { Status("減少間隔は整数で入力してください"); return null; }
        var stat = new StatDefinition
        {
            Name = name.Text.Trim(),
            MaxHearts = maxValue,
            InitialHearts = initialValue,
            DecreaseInterval = intervalValue,
            MarkFull = string.IsNullOrWhiteSpace(mark.Text) ? "♥" : mark.Text.Trim()
        };
        if (stat.Validate() is { } error) { Status(error); return null; }
        return stat;
    }

    private void AddStatButton_Click(object sender, RoutedEventArgs e)
    {
        if (ReadStatForm(NameTextBox, MaxTextBox, InitialTextBox, IntervalTextBox, MarkTextBox) is { } stat)
            Status(Game.AddStat(stat));
    }

    private void UpdateStatButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStat is not { } name) { Status("更新するゲージを選んでください"); return; }
        if (ReadStatForm(NameTextBox, MaxTextBox, InitialTextBox, IntervalTextBox, MarkTextBox) is { } stat)
            Status(Game.UpdateStat(name, stat));
    }

    private void RemoveStatButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStat is { } name) Status(Game.RemoveStat(name));
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e) { if (SelectedStat is { } name) Game.Move(name, -1); }
    private void MoveDownButton_Click(object sender, RoutedEventArgs e) { if (SelectedStat is { } name) Game.Move(name, 1); }
    private void MinusButton_Click(object sender, RoutedEventArgs e) { if (SelectedStat is { } name) Game.Change(name, -1); }
    private void PlusButton_Click(object sender, RoutedEventArgs e) { if (SelectedStat is { } name) Game.Change(name, 1); }
    private void ResetButton_Click(object sender, RoutedEventArgs e) { if (SelectedStat is { } name) Game.Reset(name); }
    private void ResetAllButton_Click(object sender, RoutedEventArgs e) => Game.ResetAll();

    private void SetValueButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStat is not { } name) return;
        if (int.TryParse(ValueTextBox.Text, out var value)) Status(Game.SetValue(name, value));
        else Status("値は整数で入力してください");
    }

    // ===== プリセット =====

    private void ReloadPresets() =>
        PresetComboBox.ItemsSource = TomozotchiStore.ListPresets(context.DataDirectory);

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetComboBox.Text.Trim();
        try
        {
            TomozotchiStore.SavePreset(context.DataDirectory, name, Game.Config.Stats);
            ReloadPresets();
            PresetComboBox.Text = name;
            Status($"プリセット \"{name}\" を保存しました");
        }
        catch (Exception ex)
        {
            Status($"保存できませんでした: {ex.Message}");
        }
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e) =>
        LoadFile(TomozotchiStore.PresetPath(context.DataDirectory, PresetComboBox.Text.Trim()));

    private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetComboBox.Text.Trim();
        var path = TomozotchiStore.PresetPath(context.DataDirectory, name);
        if (!File.Exists(path)) { Status($"プリセット \"{name}\" が見つかりません"); return; }
        if (MessageBox.Show(this, $"プリセット \"{name}\" を削除しますか？", "ともぞっち", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            return;
        File.Delete(path);
        ReloadPresets();
        Status($"プリセット \"{name}\" を削除しました");
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) == true) LoadFile(dialog.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            var config = TomozotchiStore.ReadGameConfig(path);
            // 割り当てのない古い savedata で今の割り当てを消さない
            var actions = config.ChannelPoints?.RewardActions is { Count: > 0 } list ? list : null;
            Game.ReplaceStats(config.Stats, actions);
            Status($"{Path.GetFileName(path)} を読み込みました（ゲージ {Game.Config.Stats.Count} 件"
                + (actions is null ? "）" : $"、チャネポ割り当て {actions.Count} 件）"));
        }
        catch (Exception ex)
        {
            Status($"読み込めませんでした: {ex.Message}");
        }
    }

    // ===== チャネポ =====

    private string? SelectedActionRewardId => (ActionsListBox.SelectedItem as Row)?.Key;

    private void ReloadRewards()
    {
        try
        {
            rewards = context.GetChannelPointRewards();
        }
        catch (Exception ex)
        {
            context.LogError("チャネポ一覧を取得できませんでした。", ex);
        }
        var selected = RewardComboBox.SelectedValue as string;
        // JTSA 側でまだ取得していないリワードも、割り当て済みなら ID で表示する
        RewardComboBox.ItemsSource = rewards
            .Concat(Game.RewardActions
                .Where(action => rewards.All(reward => reward.Id != action.RewardId))
                .Select(action => new ChannelPointRewardInfo(action.RewardId, action.RewardId, false)))
            .ToList();
        RewardComboBox.SelectedValue = selected;
    }

    private string TitleOf(string rewardId) =>
        rewards.FirstOrDefault(reward => reward.Id == rewardId)?.Title ?? rewardId;

    private void ReloadActions()
    {
        var target = TargetComboBox.Text;
        TargetComboBox.ItemsSource = Game.Config.Stats.Select(stat => stat.Name).ToList();
        TargetComboBox.Text = target;
        var selected = SelectedActionRewardId;
        ActionsListBox.ItemsSource = Game.RewardActions.Select(action => new Row(
            action.RewardId,
            $"{TitleOf(action.RewardId)} → " + action.Action switch
            {
                RewardActionTypes.Modify => $"{action.Stat} を {(action.Amount > 0 ? "+" : "")}{action.Amount}",
                RewardActionTypes.AddStat => $"ゲージ「{action.StatDef?.Name}」を追加",
                RewardActionTypes.RemoveStat => $"ゲージ「{action.Stat}」を削除",
                _ => action.Action
            })).ToList();
        ActionsListBox.SelectedItem = ActionsListBox.Items.Cast<Row>().FirstOrDefault(row => row.Key == selected);
    }

    private void RefreshRewardsButton_Click(object sender, RoutedEventArgs e)
    {
        ReloadRewards();
        ReloadActions();
        Status($"リワード {rewards.Count} 件");
    }

    private void ActionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var action = Game.RewardActions.FirstOrDefault(item => item.RewardId == SelectedActionRewardId);
        if (action is null) return;
        RewardComboBox.SelectedValue = action.RewardId;
        ActionComboBox.SelectedItem = ActionComboBox.Items.Cast<ComboBoxItem>().First(item => (string)item.Tag == action.Action);
        TargetComboBox.Text = action.Stat ?? "";
        AmountTextBox.Text = action.Amount?.ToString() ?? "";
        var def = action.StatDef;
        AddNameTextBox.Text = def?.Name ?? "";
        AddMaxTextBox.Text = (def?.MaxHearts ?? 3).ToString();
        AddInitialTextBox.Text = def?.InitialHearts?.ToString() ?? "";
        AddIntervalTextBox.Text = (def?.DecreaseInterval ?? 30).ToString();
        AddMarkTextBox.Text = def?.MarkFull ?? "♥";
    }

    private string SelectedActionType => (string)((ComboBoxItem)ActionComboBox.SelectedItem).Tag;

    private void ActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetPanel is null) return; // InitializeComponent 中
        var type = SelectedActionType;
        TargetPanel.Visibility = type == RewardActionTypes.AddStat ? Visibility.Collapsed : Visibility.Visible;
        AmountPanel.Visibility = type == RewardActionTypes.Modify ? Visibility.Visible : Visibility.Collapsed;
        AddStatPanel.Visibility = type == RewardActionTypes.AddStat ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (RewardComboBox.SelectedValue is not string rewardId) { Status("リワードを選んでください"); return; }
        var action = new RewardAction { RewardId = rewardId, Action = SelectedActionType };
        switch (action.Action)
        {
            case RewardActionTypes.Modify:
                if (!int.TryParse(AmountTextBox.Text, out var amount) || amount == 0) { Status("増減量は 0 以外の整数で入力してください"); return; }
                action.Amount = amount;
                goto case RewardActionTypes.RemoveStat;
            case RewardActionTypes.RemoveStat:
                action.Stat = TargetComboBox.Text.Trim();
                if (action.Stat.Length == 0) { Status("対象ゲージを入力してください"); return; }
                break;
            case RewardActionTypes.AddStat:
                action.StatDef = ReadStatForm(AddNameTextBox, AddMaxTextBox, AddInitialTextBox, AddIntervalTextBox, AddMarkTextBox);
                if (action.StatDef is null) return;
                break;
        }
        Game.SetRewardAction(action);
        ActionsListBox.SelectedItem = ActionsListBox.Items.Cast<Row>().FirstOrDefault(row => row.Key == rewardId);
        Status($"{TitleOf(rewardId)} の割り当てを保存しました");
    }

    private void RemoveActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionRewardId is { } rewardId) Game.RemoveRewardAction(rewardId);
    }

    private void TestActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (RewardComboBox.SelectedValue is not string rewardId) { Status("リワードを選んでください"); return; }
        Status(Game.FireReward(rewardId) ?? new(false, "このリワードには動作が割り当てられていません（保存してから実行してください）"));
    }

    // ===== 表示 =====

    private void ApplyOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(OverlayXTextBox.Text, out var x) || !int.TryParse(OverlayYTextBox.Text, out var y)
            || !int.TryParse(OverlayWidthTextBox.Text, out var width) || !int.TryParse(OverlayHeightTextBox.Text, out var height))
        {
            Status("位置と大きさは整数で入力してください");
            return;
        }
        plugin.Settings.OverlayX = Math.Clamp(x, 0, 1920);
        plugin.Settings.OverlayY = Math.Clamp(y, 0, 1080);
        plugin.Settings.OverlayWidth = Math.Clamp(width, 1, 1920);
        plugin.Settings.OverlayHeight = Math.Clamp(height, 1, 1080);
        plugin.SaveSettings();
        plugin.UpdateOverlay();
        Status("表示位置を保存しました");
    }

    private void Status(TomozotchiResult result) => Status(result.Message);
    private void Status(string message) => StatusText.Text = message;
}
