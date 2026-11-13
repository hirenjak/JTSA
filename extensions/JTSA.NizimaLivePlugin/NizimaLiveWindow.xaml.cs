using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public partial class NizimaLiveWindow : Window
{
    private readonly NizimaLivePlugin plugin;
    private readonly IJtsaPluginContext context;
    private readonly NizimaLiveSettings settings;
    private readonly bool channelPointOnly;
    private readonly ObservableCollection<RuleListItem> rules = [];
    private readonly ObservableCollection<NizimaNamedOption> expressions = [];
    private readonly ObservableCollection<NizimaNamedOption> motions = [];
    private readonly ObservableCollection<NizimaNamedOption> expressionModels = [];
    private readonly ObservableCollection<NizimaNamedOption> motionModels = [];
    private readonly ObservableCollection<NizimaNamedOption> live2DItems = [];
    private readonly ObservableCollection<NizimaNamedOption> itemExpressions = [];
    private readonly ObservableCollection<ChannelPointRewardInfo> channelPoints = [];
    private NizimaRuleCatalogs ruleCatalogs = NizimaRuleCatalogs.Empty;
    private bool loading;
    private bool catalogsBusy;

    public NizimaLiveWindow(
        NizimaLivePlugin plugin,
        IJtsaPluginContext context,
        NizimaLiveSettings settings,
        bool channelPointOnly)
    {
        this.plugin = plugin;
        this.context = context;
        this.settings = settings;
        this.channelPointOnly = channelPointOnly;
        InitializeComponent();
        UrlTextBox.Text = settings.WebSocketUrl;
        loading = true;
        AutoConnectCheckBox.IsChecked = settings.AutoConnect;
        loading = false;
        RulesListBox.ItemsSource = rules;
        ExpressionComboBox.ItemsSource = expressions;
        MotionComboBox.ItemsSource = motions;
        ExpressionModelComboBox.ItemsSource = expressionModels;
        MotionModelComboBox.ItemsSource = motionModels;
        ItemComboBox.ItemsSource = live2DItems;
        ItemExpressionComboBox.ItemsSource = itemExpressions;
        ReloadRules();
        ReloadChannelPointsForRules();
        plugin.Client.StatusChanged += OnClientStatusChanged;
        plugin.Client.CatalogChanged += OnClientStatusChanged;
        Closed += (_, _) =>
        {
            plugin.Client.StatusChanged -= OnClientStatusChanged;
            plugin.Client.CatalogChanged -= OnClientStatusChanged;
        };
        RefreshConnectionUi();
        _ = RefreshCatalogsAsync();
    }

    public void RefreshConnectionUi()
    {
        StatusText.Text = plugin.Client.StatusText;
        LoopbackWarning.Text = NizimaProtocol.IsNonLoopbackUrl(UrlTextBox.Text)
            ? "localhost 以外です。公式 API はループバック限定です。"
            : "";
    }

    private void OnClientStatusChanged() =>
        Dispatcher.BeginInvoke(() =>
        {
            RefreshConnectionUi();
            _ = RefreshCatalogsAsync();
        });

    private async Task RefreshCatalogsAsync()
    {
        if (catalogsBusy)
            return;
        catalogsBusy = true;
        try
        {
            if (!plugin.Client.CanSendMethods)
            {
                ruleCatalogs = NizimaRuleCatalogs.Empty;
                expressions.Clear();
                motions.Clear();
                expressionModels.Clear();
                motionModels.Clear();
                live2DItems.Clear();
                itemExpressions.Clear();
                return;
            }

            ruleCatalogs = await plugin.Client.GetRuleCatalogsAsync(null, CancellationToken.None);
            Replace(expressionModels, NizimaRuleCatalogs.WithCurrentModelOption(ruleCatalogs.ModelsOnScreen));
            Replace(motionModels, NizimaRuleCatalogs.WithCurrentModelOption(ruleCatalogs.ModelsOnScreen));
            Replace(live2DItems, ruleCatalogs.Live2DItems);
            await ReloadExpressionListAsync();
            await ReloadMotionListAsync();
            await ReloadItemExpressionListAsync();
            ReloadRules();
        }
        catch (Exception ex)
        {
            ManualStatus.Text = $"一覧の取得に失敗: {ex.Message}";
        }
        finally
        {
            catalogsBusy = false;
        }
    }

    private static void Replace(ObservableCollection<NizimaNamedOption> target, IReadOnlyList<NizimaNamedOption> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private async void ExpressionModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        await ReloadExpressionListAsync();

    private async void MotionModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        await ReloadMotionListAsync();

    private async void ItemComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        await ReloadItemExpressionListAsync();

    private async Task ReloadExpressionListAsync()
    {
        if (!plugin.Client.CanSendMethods)
            return;
        var selected = SelectedPath(ExpressionComboBox);
        var list = await plugin.Client.GetExpressionsAsync(SelectedPath(ExpressionModelComboBox), CancellationToken.None);
        Replace(expressions, list);
        NizimaNamedOptionCatalog.SelectComboValue(ExpressionComboBox, expressions, selected);
    }

    private async Task ReloadMotionListAsync()
    {
        if (!plugin.Client.CanSendMethods)
            return;
        var selected = SelectedPath(MotionComboBox);
        var list = await plugin.Client.GetMotionsAsync(SelectedPath(MotionModelComboBox), CancellationToken.None);
        Replace(motions, list);
        NizimaNamedOptionCatalog.SelectComboValue(MotionComboBox, motions, selected);
    }

    private async Task ReloadItemExpressionListAsync()
    {
        if (!plugin.Client.CanSendMethods)
            return;
        var itemId = SelectedPath(ItemComboBox);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemExpressions.Clear();
            return;
        }

        var selected = SelectedPath(ItemExpressionComboBox);
        var list = await plugin.Client.GetExpressionsAsync(itemId, CancellationToken.None);
        Replace(itemExpressions, list);
        NizimaNamedOptionCatalog.SelectComboValue(ItemExpressionComboBox, itemExpressions, selected);
    }

    private string SelectedPath(System.Windows.Controls.ComboBox combo) =>
        combo.SelectedValue as string ?? combo.Text?.Trim() ?? "";

    private void RefreshCatalogButton_Click(object sender, RoutedEventArgs e) =>
        _ = RefreshCatalogsAsync();

    private void ReloadRules()
    {
        rules.Clear();
        foreach (var rule in VisibleRules())
            rules.Add(new RuleListItem(rule, NizimaTriggerRuleSummary.Format(rule, ruleCatalogs, channelPoints)));
    }

    private void ReloadChannelPointsForRules()
    {
        var keepIds = settings.Rules
            .Where(rule => rule.TriggerType == NizimaTriggerTypes.ChannelPoint)
            .Select(rule => rule.TriggerValue);
        NizimaChannelPointCatalog.ReplaceKeeping(channelPoints, context.GetChannelPointRewards(), keepIds);
        ReloadRules();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        settings.WebSocketUrl = UrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(settings.WebSocketUrl))
            settings.WebSocketUrl = NizimaProtocol.DefaultWebSocketUrl;
        plugin.SaveSettings();
        RefreshConnectionUi();
        try
        {
            await plugin.ConnectNowAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            context.LogError("nizima LIVE 接続に失敗しました。", ex);
            ManualStatus.Text = ex.Message;
        }
        RefreshConnectionUi();
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        plugin.StopAutoConnect();
        AutoConnectCheckBox.IsChecked = false;
        await plugin.Client.DisconnectAsync();
        plugin.SaveSettings();
        RefreshConnectionUi();
    }

    private void AutoConnectCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (loading)
            return;
        if (AutoConnectCheckBox.IsChecked == true)
            plugin.StartAutoConnect();
        else
            plugin.StopAutoConnect();
        plugin.SaveSettings();
    }

    private void AddRuleButton_Click(object sender, RoutedEventArgs e) => EditRule(new NizimaTriggerRule());

    private void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleListItem item)
            return;
        settings.Rules.Remove(item.Rule);
        plugin.SaveSettings();
        ReloadChannelPointsForRules();
    }

    private void RulesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RulesListBox.SelectedItem is RuleListItem item)
            EditRule(item.Rule);
    }

    private void EditRule(NizimaTriggerRule rule)
    {
        var keepIds = settings.Rules
            .Where(item => item.TriggerType == NizimaTriggerTypes.ChannelPoint)
            .Select(item => item.TriggerValue)
            .Append(rule.TriggerValue);
        var dialog = new NizimaRuleDialog(rule, context, ruleCatalogs, keepIds, channelPointOnly, plugin.Client)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;
        if (!settings.Rules.Contains(rule))
            settings.Rules.Add(rule);
        plugin.SaveSettings();
        ReloadChannelPointsForRules();
    }

    private IEnumerable<NizimaTriggerRule> VisibleRules() =>
        channelPointOnly
            ? settings.Rules.Where(rule => rule.TriggerType == NizimaTriggerTypes.ChannelPoint)
            : settings.Rules;

    private Task RunManualAsync(NizimaTriggerRule rule) =>
        RunSafeAsync(() => NizimaTriggerExecutor.ExecuteAsync(
            plugin.Client, rule, CancellationToken.None, (message, exception) => context.LogError(message, exception)));

    private async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
            ManualStatus.Text = "実行しました。";
        }
        catch (Exception ex)
        {
            ManualStatus.Text = ex.Message;
            context.LogError(ex.Message, ex);
        }
    }

    private NizimaTriggerRule BaseManual(string command, string value) => new()
    {
        CommandType = command,
        CommandValue = value,
        ModelId = ModelIdTextBox.Text.Trim(),
        Extra = new NizimaTriggerCommandExtra { ModelPath = ModelPathTextBox.Text.Trim() }
    };

    private void ChangeModelButton_Click(object sender, RoutedEventArgs e)
    {
        var rule = BaseManual(NizimaTriggerCommands.ChangeModel, ModelPathTextBox.Text.Trim());
        _ = RunManualAsync(rule);
    }

    private void ExpressionOnButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(ManualFor(NizimaTriggerCommands.ExpressionOn, SelectedPath(ExpressionComboBox), SelectedPath(ExpressionModelComboBox)));

    private void ExpressionOffButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(ManualFor(NizimaTriggerCommands.ExpressionOff, SelectedPath(ExpressionComboBox), SelectedPath(ExpressionModelComboBox)));

    private void MotionOnButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(ManualFor(NizimaTriggerCommands.StartMotion, SelectedPath(MotionComboBox), SelectedPath(MotionModelComboBox)));

    private void MotionOffButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(ManualFor(NizimaTriggerCommands.StopMotion, SelectedPath(MotionComboBox), SelectedPath(MotionModelComboBox)));

    private void ItemExpressionOnButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(ManualFor(NizimaTriggerCommands.ItemExpressionOn, SelectedPath(ItemExpressionComboBox), SelectedPath(ItemComboBox)));

    private void ItemExpressionOffButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(ManualFor(NizimaTriggerCommands.ItemExpressionOff, SelectedPath(ItemExpressionComboBox), SelectedPath(ItemComboBox)));

    private static NizimaTriggerRule ManualFor(string command, string value, string targetId) => new()
    {
        CommandType = command,
        CommandValue = value,
        ModelId = targetId
    };
}

public sealed class RuleListItem(NizimaTriggerRule rule, string summary)
{
    public NizimaTriggerRule Rule { get; } = rule;
    public string Summary { get; } = summary;
}
