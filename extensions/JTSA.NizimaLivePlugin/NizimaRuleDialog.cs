using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public sealed class NizimaRuleDialog : Window
{
    private readonly NizimaTriggerRule rule;
    private readonly IJtsaPluginContext context;
    private readonly IEnumerable<string> keepChannelPointIds;
    private readonly ComboBox triggerType = new();
    private readonly TextBox triggerValue = new();
    private readonly ComboBox triggerReward = new()
    {
        DisplayMemberPath = nameof(ChannelPointRewardInfo.Title),
        SelectedValuePath = nameof(ChannelPointRewardInfo.Id)
    };
    private readonly ObservableCollection<ChannelPointRewardInfo> channelPoints = [];
    private readonly TextBlock triggerValueLabel = new();
    private readonly ComboBox commandType = new();
    private readonly ComboBox commandCombo = new()
    {
        Height = 24,
        IsEditable = true,
        DisplayMemberPath = "Label",
        SelectedValuePath = "Path"
    };
    private readonly TextBox commandHotkey = new() { Height = 24 };
    private readonly TextBlock commandValueLabel = new()
    {
        Foreground = System.Windows.Media.Brushes.LightGray,
        Margin = new Thickness(0, 6, 0, 2)
    };
    private readonly Grid commandValueHost = new();
    private readonly TextBox modelId = new();
    private readonly TextBox sceneId = new();
    private readonly TextBox modelPath = new();
    private readonly CheckBox enabled = new() { Content = "有効", Foreground = System.Windows.Media.Brushes.White, IsChecked = true };

    public NizimaRuleDialog(
        NizimaTriggerRule rule,
        IJtsaPluginContext context,
        IReadOnlyList<NizimaNamedOption> expressions,
        IReadOnlyList<NizimaNamedOption> motions,
        IEnumerable<string> keepChannelPointIds)
    {
        this.rule = rule;
        this.context = context;
        this.keepChannelPointIds = keepChannelPointIds;
        Title = "トリガールール";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x30));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        triggerReward.ItemsSource = channelPoints;
        triggerReward.DropDownOpened += (_, _) => ReloadChannelPoints(triggerReward.SelectedValue as string ?? rule.TriggerValue);

        BindChoiceCombo(triggerType, NizimaTriggerTypes.Choices, rule.TriggerType);
        BindChoiceCombo(commandType, NizimaTriggerCommands.Choices, rule.CommandType);
        triggerValue.Text = rule.TriggerValue;
        commandCombo.Text = rule.CommandValue;
        commandHotkey.Text = rule.CommandValue;
        NizimaHotkeyCapture.Attach(commandHotkey);
        commandValueHost.Height = 24;
        commandValueHost.Children.Add(commandCombo);
        commandValueHost.Children.Add(commandHotkey);
        triggerType.SelectionChanged += (_, _) =>
        {
            SyncTriggerUi();
            SyncCommandValueUi();
        };
        commandType.SelectionChanged += (_, _) => SyncCommandValueUi();
        void SyncCommandValueUi()
        {
            var selected = SelectedId(commandType);
            var isHotkey = selected == NizimaTriggerCommands.TriggerHotkey;
            commandHotkey.Visibility = isHotkey ? Visibility.Visible : Visibility.Collapsed;
            commandCombo.Visibility = isHotkey ? Visibility.Collapsed : Visibility.Visible;
            commandValueLabel.Text = isHotkey
                ? "コマンド値（欄を選択してキーを押す）"
                : "コマンド値（表情・モーションは一覧から選択可）";

            if (isHotkey)
            {
                commandCombo.ItemsSource = null;
                if (string.IsNullOrWhiteSpace(commandHotkey.Text))
                    commandHotkey.Text = (commandCombo.Text ?? "").Trim();
                return;
            }

            if (!string.IsNullOrWhiteSpace(commandHotkey.Text))
                commandCombo.Text = commandHotkey.Text.Trim();

            commandCombo.ItemsSource = selected is NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff
                ? expressions
                : selected is NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion
                    ? motions
                    : null;
            if (commandCombo.ItemsSource is null)
                commandCombo.Text = rule.CommandValue;
            else
                commandCombo.SelectedValue = rule.CommandValue;
        }
        SyncCommandValueUi();
        ReloadChannelPoints(rule.TriggerValue);
        SyncTriggerUi();
        modelId.Text = rule.ModelId;
        sceneId.Text = rule.SceneId;
        modelPath.Text = rule.Extra.ModelPath;
        enabled.IsChecked = rule.IsEnabled;

        var save = new Button { Content = "保存", Width = 80, Height = 28, Margin = new Thickness(0, 12, 8, 0) };
        save.Click += (_, _) =>
        {
            rule.IsEnabled = enabled.IsChecked == true;
            rule.TriggerType = SelectedId(triggerType) ?? NizimaTriggerTypes.ChannelPoint;
            rule.TriggerValue = rule.TriggerType == NizimaTriggerTypes.ChannelPoint
                ? (triggerReward.SelectedValue as string ?? "").Trim()
                : triggerValue.Text.Trim();
            rule.CommandType = SelectedId(commandType) ?? NizimaTriggerCommands.ExpressionOn;
            rule.CommandValue = rule.CommandType == NizimaTriggerCommands.TriggerHotkey
                ? commandHotkey.Text.Trim()
                : (commandCombo.SelectedValue as string ?? commandCombo.Text ?? "").Trim();
            rule.ModelId = modelId.Text.Trim();
            rule.SceneId = sceneId.Text.Trim();
            rule.Extra.ModelPath = modelPath.Text.Trim();
            if (rule.CommandType == NizimaTriggerCommands.ChangeModel &&
                (string.IsNullOrWhiteSpace(rule.ModelId) || string.IsNullOrWhiteSpace(FirstPath())))
            {
                MessageBox.Show(this, "モデル切り替えには ModelId と Path が必要です。", Title);
                return;
            }

            if (rule.CommandType == NizimaTriggerCommands.ChangeModel)
                rule.Extra.ModelPath = FirstPath();
            DialogResult = true;
            Close();
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        void Add(string label, UIElement element)
        {
            panel.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.LightGray, Margin = new Thickness(0, 6, 0, 2) });
            panel.Children.Add(element);
        }

        triggerValueLabel.Foreground = System.Windows.Media.Brushes.LightGray;
        triggerValueLabel.Margin = new Thickness(0, 6, 0, 2);
        panel.Children.Add(enabled);
        Add("トリガー種別", triggerType);
        panel.Children.Add(triggerValueLabel);
        var triggerValueHost = new Grid();
        triggerValueHost.Children.Add(triggerReward);
        triggerValueHost.Children.Add(triggerValue);
        panel.Children.Add(triggerValueHost);
        Add("コマンド", commandType);
        panel.Children.Add(commandValueLabel);
        panel.Children.Add(commandValueHost);
        Add("ModelId（空なら現在モデル）", modelId);
        Add("SceneId", sceneId);
        Add("ModelPath / ItemPath", modelPath);
        panel.Children.Add(save);
        Content = panel;
    }

    private void ReloadChannelPoints(string selectedId)
    {
        NizimaChannelPointCatalog.ReplaceKeeping(
            channelPoints,
            context.GetChannelPointRewards(),
            keepChannelPointIds);
        if (string.IsNullOrWhiteSpace(selectedId) && channelPoints.Count > 0 && string.IsNullOrWhiteSpace(rule.TriggerValue))
            selectedId = channelPoints[0].Id;
        if (!string.IsNullOrWhiteSpace(selectedId))
            NizimaChannelPointCatalog.EnsureOption(channelPoints, selectedId);
        triggerReward.SelectedValue = string.IsNullOrWhiteSpace(selectedId) ? null : selectedId;
        if (triggerReward.SelectedIndex < 0 && channelPoints.Count > 0)
            triggerReward.SelectedIndex = 0;
    }

    private void SyncTriggerUi()
    {
        var isChannelPoint = SelectedId(triggerType) == NizimaTriggerTypes.ChannelPoint;
        triggerReward.Visibility = isChannelPoint ? Visibility.Visible : Visibility.Collapsed;
        triggerValue.Visibility = isChannelPoint ? Visibility.Collapsed : Visibility.Visible;
        triggerValueLabel.Text = isChannelPoint
            ? "チャンネルポイント報酬"
            : "トリガー値（チャットは部分一致）";
        if (isChannelPoint)
            ReloadChannelPoints(triggerReward.SelectedValue as string ?? rule.TriggerValue);
    }

    private static void BindChoiceCombo(ComboBox combo, IReadOnlyList<NizimaChoice> choices, string selectedId)
    {
        combo.DisplayMemberPath = nameof(NizimaChoice.Label);
        combo.SelectedValuePath = nameof(NizimaChoice.Id);
        combo.ItemsSource = choices;
        combo.SelectedValue = selectedId;
    }

    private static string? SelectedId(ComboBox combo) => combo.SelectedValue as string;

    private string FirstPath() =>
        string.IsNullOrWhiteSpace(modelPath.Text)
            ? (commandCombo.SelectedValue as string ?? commandCombo.Text ?? "").Trim()
            : modelPath.Text.Trim();
}
