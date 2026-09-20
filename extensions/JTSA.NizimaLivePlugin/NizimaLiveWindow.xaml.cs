using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public partial class NizimaLiveWindow : Window
{
    private readonly NizimaLivePlugin plugin;
    private readonly IJtsaPluginContext context;
    private readonly NizimaLiveSettings settings;
    private readonly ObservableCollection<RuleListItem> rules = [];
    private readonly ObservableCollection<NizimaNamedOption> expressions = [];
    private readonly ObservableCollection<NizimaNamedOption> motions = [];
    private readonly ObservableCollection<ChannelPointRewardInfo> channelPoints = [];
    private bool loading;
    private bool catalogsBusy;

    public NizimaLiveWindow(NizimaLivePlugin plugin, IJtsaPluginContext context, NizimaLiveSettings settings)
    {
        this.plugin = plugin;
        this.context = context;
        this.settings = settings;
        InitializeComponent();
        UrlTextBox.Text = settings.WebSocketUrl;
        loading = true;
        AutoConnectCheckBox.IsChecked = settings.AutoConnect;
        loading = false;
        RulesListBox.ItemsSource = rules;
        ExpressionComboBox.ItemsSource = expressions;
        MotionComboBox.ItemsSource = motions;
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
                expressions.Clear();
                motions.Clear();
                return;
            }

            var modelId = ModelIdTextBox.Text.Trim();
            var expressionList = await plugin.Client.GetExpressionsAsync(modelId, CancellationToken.None);
            var motionList = await plugin.Client.GetMotionsAsync(modelId, CancellationToken.None);
            Replace(expressions, expressionList);
            Replace(motions, motionList);
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

    private string SelectedPath(System.Windows.Controls.ComboBox combo) =>
        combo.SelectedValue as string ?? combo.Text?.Trim() ?? "";

    private void RefreshCatalogButton_Click(object sender, RoutedEventArgs e) =>
        _ = RefreshCatalogsAsync();

    private void ReloadRules()
    {
        rules.Clear();
        foreach (var rule in settings.Rules)
            rules.Add(new RuleListItem(rule, NizimaTriggerRuleSummary.Format(rule, expressions, motions, channelPoints)));
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
        var dialog = new NizimaRuleDialog(rule, context, expressions, motions, keepIds)
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

    private Task RunManualAsync(NizimaTriggerRule rule) =>
        RunSafeAsync(() => NizimaTriggerExecutor.ExecuteAsync(plugin.Client, rule, CancellationToken.None));

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
        SceneId = SceneIdTextBox.Text.Trim(),
        Extra = new NizimaTriggerCommandExtra
        {
            ModelPath = ModelPathTextBox.Text.Trim(),
            ItemPath = ItemTextBox.Text.Trim()
        }
    };

    private void ChangeModelButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.ChangeModel, ModelPathTextBox.Text.Trim()));

    private void AddModelButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.AddModel, ModelPathTextBox.Text.Trim()));

    private void HotkeyButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.TriggerHotkey, HotkeyTextBox.Text.Trim()));

    private void ExpressionOnButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.ExpressionOn, SelectedPath(ExpressionComboBox)));

    private void ExpressionOffButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.ExpressionOff, SelectedPath(ExpressionComboBox)));

    private void MotionOnButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.StartMotion, SelectedPath(MotionComboBox)));

    private void MotionOffButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.StopMotion, SelectedPath(MotionComboBox)));

    private void AddItemButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.AddItem, ItemTextBox.Text.Trim()));

    private void RemoveItemButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.RemoveItem, ItemTextBox.Text.Trim()));

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var rule = BaseManual(NizimaTriggerCommands.SetModelColor, "");
        rule.Extra.R = ParseByte(ColorR.Text, 255);
        rule.Extra.G = ParseByte(ColorG.Text, 255);
        rule.Extra.B = ParseByte(ColorB.Text, 255);
        rule.Extra.A = ParseByte(ColorA.Text, 255);
        rule.Extra.UseScreen = ScreenColorCheckBox.IsChecked == true;
        _ = RunManualAsync(rule);
    }

    private void EffectOnButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.EffectOn, EffectTextBox.Text.Trim()));

    private void EffectOffButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunManualAsync(BaseManual(NizimaTriggerCommands.EffectOff, EffectTextBox.Text.Trim()));

    private void RawJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var rule = BaseManual(NizimaTriggerCommands.RawJson, "");
        rule.Extra.RawJson = RawJsonTextBox.Text;
        _ = RunManualAsync(rule);
    }

    private static int ParseByte(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 255)
            : fallback;
}

public sealed class RuleListItem(NizimaTriggerRule rule, string summary)
{
    public NizimaTriggerRule Rule { get; } = rule;
    public string Summary { get; } = summary;
}
