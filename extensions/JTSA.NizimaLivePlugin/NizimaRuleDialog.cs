using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public sealed class NizimaRuleDialog : Window
{
    private readonly NizimaTriggerRule rule;
    private readonly IJtsaPluginContext context;
    private readonly NizimaRuleCatalogs catalogs;
    private readonly IEnumerable<string> keepChannelPointIds;
    private readonly ComboBox triggerType = new();
    private readonly TextBox triggerValueChat = new() { Height = 24 };
    private readonly ComboBox triggerReward = new()
    {
        DisplayMemberPath = nameof(ChannelPointRewardInfo.Title),
        SelectedValuePath = nameof(ChannelPointRewardInfo.Id),
        Height = 24
    };
    private readonly TextBox scheduledHour = new() { Width = 36, Height = 24, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox scheduledMinute = new() { Width = 36, Height = 24, VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox triggerAdUpcoming = new()
    {
        Height = 24,
        DisplayMemberPath = nameof(NizimaChoice.Label),
        SelectedValuePath = nameof(NizimaChoice.Id),
        ItemsSource = NizimaTriggerUi.AdUpcomingChoices
    };
    private readonly ComboBox triggerObs = new()
    {
        Height = 24,
        DisplayMemberPath = nameof(NizimaChoice.Label),
        SelectedValuePath = nameof(NizimaChoice.Id),
        ItemsSource = NizimaTriggerUi.ObsStreamChoices
    };
    private readonly TextBlock triggerValueNone = new()
    {
        Text = "—（この種別では指定不要）",
        Foreground = System.Windows.Media.Brushes.Gray,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ObservableCollection<ChannelPointRewardInfo> channelPoints = [];
    private readonly TextBlock triggerValueLabel = new();
    private readonly ComboBox commandType = new();
    private readonly ObservableCollection<NizimaNamedOption> commandOptions = [];
    private readonly ComboBox commandCombo = new()
    {
        Height = 24,
        IsEditable = true,
        DisplayMemberPath = "Label",
        SelectedValuePath = "Path"
    };
    private readonly TextBlock commandValueLabel = new()
    {
        Foreground = System.Windows.Media.Brushes.LightGray,
        Margin = new Thickness(0, 6, 0, 2)
    };
    private readonly TextBlock modelTargetLabel = new()
    {
        Foreground = System.Windows.Media.Brushes.LightGray,
        Margin = new Thickness(0, 6, 0, 2)
    };
    private readonly ObservableCollection<NizimaNamedOption> modelTargetOptions = [];
    private readonly ComboBox modelTargetCombo = new()
    {
        Height = 24,
        DisplayMemberPath = "Label",
        SelectedValuePath = "Path"
    };
    private readonly CheckBox enabled = new() { Content = "有効", Foreground = System.Windows.Media.Brushes.White, IsChecked = true };
    private readonly StackPanel scheduledTimePanel;

    public NizimaRuleDialog(
        NizimaTriggerRule rule,
        IJtsaPluginContext context,
        NizimaRuleCatalogs catalogs,
        IEnumerable<string> keepChannelPointIds)
    {
        this.rule = rule;
        this.context = context;
        this.catalogs = catalogs;
        this.keepChannelPointIds = keepChannelPointIds;
        Title = "トリガールール";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x30));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        NizimaTriggerUi.AttachDigitsOnly(scheduledHour, 2);
        NizimaTriggerUi.AttachDigitsOnly(scheduledMinute, 2);

        scheduledTimePanel = new StackPanel { Orientation = Orientation.Horizontal };
        scheduledTimePanel.Children.Add(new TextBlock
        {
            Text = "時",
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });
        scheduledTimePanel.Children.Add(scheduledHour);
        scheduledTimePanel.Children.Add(new TextBlock
        {
            Text = " : ",
            Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        scheduledTimePanel.Children.Add(scheduledMinute);
        scheduledTimePanel.Children.Add(new TextBlock
        {
            Text = " 分",
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        });

        triggerReward.ItemsSource = channelPoints;
        triggerReward.DropDownOpened += (_, _) => ReloadChannelPoints(triggerReward.SelectedValue as string ?? rule.TriggerValue);
        commandCombo.ItemsSource = commandOptions;
        modelTargetCombo.ItemsSource = modelTargetOptions;

        BindChoiceCombo(triggerType, NizimaTriggerTypes.Choices, rule.TriggerType);
        BindChoiceCombo(commandType, NizimaTriggerCommands.Choices, rule.CommandType);
        triggerValueChat.Text = rule.TriggerType == NizimaTriggerTypes.Chat ? rule.TriggerValue : "";
        enabled.IsChecked = rule.IsEnabled;

        triggerType.SelectionChanged += (_, _) =>
        {
            SyncTriggerUi();
            SyncCommandValueUi();
        };
        commandType.SelectionChanged += (_, _) => SyncCommandValueUi();
        SyncCommandValueUi();
        ReloadChannelPoints(rule.TriggerValue);
        SyncTriggerUi();

        var save = new Button { Content = "保存", Width = 80, Height = 28, Margin = new Thickness(0, 12, 8, 0) };
        save.Click += (_, _) =>
        {
            if (!TrySave())
                return;
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
        var triggerValueHost = new Grid { MinHeight = 24 };
        triggerValueHost.Children.Add(triggerReward);
        triggerValueHost.Children.Add(triggerValueChat);
        triggerValueHost.Children.Add(scheduledTimePanel);
        triggerValueHost.Children.Add(triggerAdUpcoming);
        triggerValueHost.Children.Add(triggerObs);
        triggerValueHost.Children.Add(triggerValueNone);
        panel.Children.Add(triggerValueHost);
        Add("コマンド", commandType);
        panel.Children.Add(commandValueLabel);
        panel.Children.Add(commandCombo);
        panel.Children.Add(modelTargetLabel);
        panel.Children.Add(modelTargetCombo);
        panel.Children.Add(save);
        Content = panel;
    }

    private bool TrySave()
    {
        var command = SelectedId(commandType) ?? NizimaTriggerCommands.ExpressionOn;
        var trigger = SelectedId(triggerType) ?? NizimaTriggerTypes.ChannelPoint;
        rule.IsEnabled = enabled.IsChecked == true;
        rule.TriggerType = trigger;

        if (!TryReadTriggerValue(trigger, out var triggerValue, out var triggerError))
        {
            MessageBox.Show(this, triggerError, Title);
            return false;
        }

        rule.TriggerValue = triggerValue;
        rule.CommandType = command;
        rule.CommandValue = (commandCombo.SelectedValue as string ?? commandCombo.Text ?? "").Trim();
        rule.ModelId = NizimaCommandUi.ShowsModelTarget(command)
            ? (modelTargetCombo.SelectedValue as string ?? modelTargetCombo.Text ?? "").Trim()
            : "";
        rule.SceneId = "";
        rule.Extra.ModelPath = command == NizimaTriggerCommands.ChangeModel ? rule.CommandValue : "";

        if (command == NizimaTriggerCommands.ChangeModel &&
            (string.IsNullOrWhiteSpace(rule.ModelId) || string.IsNullOrWhiteSpace(rule.CommandValue)))
        {
            MessageBox.Show(this, "モデル切り替えには対象モデルと登録モデル Path が必要です。", Title);
            return false;
        }

        if (command is NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff
            or NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion &&
            string.IsNullOrWhiteSpace(rule.CommandValue))
        {
            MessageBox.Show(this, "表情またはモーションを選択してください。", Title);
            return false;
        }

        return true;
    }

    private bool TryReadTriggerValue(string triggerType, out string value, out string error)
    {
        value = "";
        error = "";
        switch (NizimaTriggerUi.InputMode(triggerType))
        {
            case NizimaTriggerValueInputMode.ChannelPoint:
                value = (triggerReward.SelectedValue as string ?? "").Trim();
                if (string.IsNullOrWhiteSpace(value))
                    error = "チャンネルポイント報酬を選択してください。";
                return !string.IsNullOrWhiteSpace(value);
            case NizimaTriggerValueInputMode.Chat:
                value = triggerValueChat.Text.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    error = "チャットの部分一致文字列を入力してください。";
                return !string.IsNullOrWhiteSpace(value);
            case NizimaTriggerValueInputMode.ScheduledTime:
                if (!NizimaTriggerUi.TryParseScheduledParts(scheduledHour.Text, scheduledMinute.Text, out var formatted, out var parseError))
                {
                    error = parseError ?? "時刻が不正です。";
                    return false;
                }

                value = formatted;
                return true;
            case NizimaTriggerValueInputMode.AdUpcoming:
                value = SelectedId(triggerAdUpcoming) ?? "";
                if (string.IsNullOrWhiteSpace(value))
                    error = "CM 開始何分前か選択してください。";
                return !string.IsNullOrWhiteSpace(value);
            case NizimaTriggerValueInputMode.ObsStreamStart:
                value = SelectedId(triggerObs) ?? "";
                if (string.IsNullOrWhiteSpace(value))
                    error = "メイン OBS またはサブ OBS を選択してください。";
                return !string.IsNullOrWhiteSpace(value);
            default:
                value = "";
                return true;
        }
    }

    private void SyncCommandValueUi()
    {
        var command = SelectedId(commandType);
        commandValueLabel.Visibility = Visibility.Visible;
        commandCombo.Visibility = Visibility.Visible;
        commandValueLabel.Text = CommandValueLabel(command);
        ReplaceCommandOptions(command);
        NizimaNamedOptionCatalog.SelectComboValue(commandCombo, commandOptions, InitialCommandValue(rule));

        if (NizimaCommandUi.ShowsModelTarget(command))
        {
            modelTargetLabel.Visibility = Visibility.Visible;
            modelTargetCombo.Visibility = Visibility.Visible;
            modelTargetLabel.Text = NizimaCommandUi.ModelTargetRequired(command)
                ? "対象モデル（画面上のモデル）"
                : "対象モデル（空なら現在のモデル）";
            var models = NizimaCommandUi.ModelTargetRequired(command)
                ? catalogs.ModelsOnScreen
                : NizimaRuleCatalogs.WithCurrentModelOption(catalogs.ModelsOnScreen);
            NizimaNamedOptionCatalog.ReplaceAll(modelTargetOptions, models);
            NizimaNamedOptionCatalog.SelectComboValue(modelTargetCombo, modelTargetOptions, rule.ModelId);
        }
        else
        {
            modelTargetLabel.Visibility = Visibility.Collapsed;
            modelTargetCombo.Visibility = Visibility.Collapsed;
        }
    }

    private static string CommandValueLabel(string? command) =>
        command switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff => "表情",
            NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion => "モーション",
            NizimaTriggerCommands.ChangeModel => "登録モデル（ModelPath）",
            _ => "コマンド値"
        };

    private void ReplaceCommandOptions(string? command)
    {
        IReadOnlyList<NizimaNamedOption> items = command switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff => catalogs.Expressions,
            NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion => catalogs.Motions,
            NizimaTriggerCommands.ChangeModel => catalogs.RegisteredModels,
            _ => []
        };
        NizimaNamedOptionCatalog.ReplaceAll(commandOptions, items);
        commandCombo.IsEditable = items.Count == 0;
    }

    private static string InitialCommandValue(NizimaTriggerRule rule) =>
        rule.CommandType == NizimaTriggerCommands.ChangeModel
            ? FirstNonEmpty(rule.CommandValue, rule.Extra.ModelPath)
            : rule.CommandValue;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

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
        var type = SelectedId(triggerType);
        var mode = NizimaTriggerUi.InputMode(type);
        triggerValueLabel.Text = NizimaTriggerUi.ValueFieldLabel(type);
        triggerValueLabel.Visibility = mode == NizimaTriggerValueInputMode.None
            ? Visibility.Collapsed
            : Visibility.Visible;

        triggerReward.Visibility = mode == NizimaTriggerValueInputMode.ChannelPoint ? Visibility.Visible : Visibility.Collapsed;
        triggerValueChat.Visibility = mode == NizimaTriggerValueInputMode.Chat ? Visibility.Visible : Visibility.Collapsed;
        scheduledTimePanel.Visibility = mode == NizimaTriggerValueInputMode.ScheduledTime ? Visibility.Visible : Visibility.Collapsed;
        triggerAdUpcoming.Visibility = mode == NizimaTriggerValueInputMode.AdUpcoming ? Visibility.Visible : Visibility.Collapsed;
        triggerObs.Visibility = mode == NizimaTriggerValueInputMode.ObsStreamStart ? Visibility.Visible : Visibility.Collapsed;
        triggerValueNone.Visibility = mode == NizimaTriggerValueInputMode.None ? Visibility.Visible : Visibility.Collapsed;

        if (mode == NizimaTriggerValueInputMode.ChannelPoint)
            ReloadChannelPoints(triggerReward.SelectedValue as string ?? rule.TriggerValue);
        if (mode == NizimaTriggerValueInputMode.Chat && string.IsNullOrWhiteSpace(triggerValueChat.Text))
            triggerValueChat.Text = type == rule.TriggerType ? rule.TriggerValue : "";
        if (mode == NizimaTriggerValueInputMode.ScheduledTime)
            LoadScheduledFields(type == rule.TriggerType ? rule.TriggerValue : "");
        if (mode == NizimaTriggerValueInputMode.AdUpcoming)
            triggerAdUpcoming.SelectedValue = type == rule.TriggerType ? rule.TriggerValue : null;
        if (mode == NizimaTriggerValueInputMode.ObsStreamStart)
            triggerObs.SelectedValue = type == rule.TriggerType ? rule.TriggerValue : null;
    }

    private void LoadScheduledFields(string stored)
    {
        if (NizimaTriggerUi.TryParseScheduledTime(stored, out var hour, out var minute))
        {
            scheduledHour.Text = hour.ToString(CultureInfo.InvariantCulture);
            scheduledMinute.Text = minute.ToString(CultureInfo.InvariantCulture);
            return;
        }

        scheduledHour.Text = "";
        scheduledMinute.Text = "";
    }

    private static void BindChoiceCombo(ComboBox combo, IReadOnlyList<NizimaChoice> choices, string selectedId)
    {
        combo.DisplayMemberPath = nameof(NizimaChoice.Label);
        combo.SelectedValuePath = nameof(NizimaChoice.Id);
        combo.ItemsSource = choices;
        combo.SelectedValue = selectedId;
    }

    private static string? SelectedId(ComboBox combo) => combo.SelectedValue as string;
}
