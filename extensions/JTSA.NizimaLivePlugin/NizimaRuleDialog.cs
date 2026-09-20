using System.Collections.ObjectModel;
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
    private readonly TextBox triggerValue = new();
    private readonly ComboBox triggerReward = new()
    {
        DisplayMemberPath = nameof(ChannelPointRewardInfo.Title),
        SelectedValuePath = nameof(ChannelPointRewardInfo.Id)
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
    private readonly TextBox commandHotkey = new() { Height = 24 };
    private readonly TextBlock commandValueLabel = new()
    {
        Foreground = System.Windows.Media.Brushes.LightGray,
        Margin = new Thickness(0, 6, 0, 2)
    };
    private readonly Grid commandValueHost = new() { Height = 24 };
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
    private readonly TextBlock sceneLabel = new()
    {
        Foreground = System.Windows.Media.Brushes.LightGray,
        Margin = new Thickness(0, 6, 0, 2)
    };
    private readonly ObservableCollection<NizimaNamedOption> sceneOptions = [];
    private readonly ComboBox sceneCombo = new()
    {
        Height = 24,
        IsEditable = true,
        DisplayMemberPath = "Label",
        SelectedValuePath = "Path"
    };
    private readonly CheckBox enabled = new() { Content = "有効", Foreground = System.Windows.Media.Brushes.White, IsChecked = true };

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

        triggerReward.ItemsSource = channelPoints;
        triggerReward.DropDownOpened += (_, _) => ReloadChannelPoints(triggerReward.SelectedValue as string ?? rule.TriggerValue);
        commandCombo.ItemsSource = commandOptions;
        modelTargetCombo.ItemsSource = modelTargetOptions;
        sceneCombo.ItemsSource = sceneOptions;

        BindChoiceCombo(triggerType, NizimaTriggerTypes.Choices, rule.TriggerType);
        BindChoiceCombo(commandType, NizimaTriggerCommands.Choices, rule.CommandType);
        triggerValue.Text = rule.TriggerValue;
        commandHotkey.Text = InitialCommandValue(rule);
        NizimaHotkeyCapture.Attach(commandHotkey);
        commandValueHost.Children.Add(commandCombo);
        commandValueHost.Children.Add(commandHotkey);
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
        var triggerValueHost = new Grid();
        triggerValueHost.Children.Add(triggerReward);
        triggerValueHost.Children.Add(triggerValue);
        panel.Children.Add(triggerValueHost);
        Add("コマンド", commandType);
        panel.Children.Add(commandValueLabel);
        panel.Children.Add(commandValueHost);
        panel.Children.Add(modelTargetLabel);
        panel.Children.Add(modelTargetCombo);
        panel.Children.Add(sceneLabel);
        panel.Children.Add(sceneCombo);
        panel.Children.Add(save);
        Content = panel;
    }

    private bool TrySave()
    {
        var command = SelectedId(commandType) ?? NizimaTriggerCommands.ExpressionOn;
        rule.IsEnabled = enabled.IsChecked == true;
        rule.TriggerType = SelectedId(triggerType) ?? NizimaTriggerTypes.ChannelPoint;
        rule.TriggerValue = rule.TriggerType == NizimaTriggerTypes.ChannelPoint
            ? (triggerReward.SelectedValue as string ?? "").Trim()
            : triggerValue.Text.Trim();
        rule.CommandType = command;
        rule.CommandValue = NizimaCommandUi.UsesHotkey(command)
            ? commandHotkey.Text.Trim()
            : (commandCombo.SelectedValue as string ?? commandCombo.Text ?? "").Trim();
        rule.ModelId = NizimaCommandUi.ShowsModelTarget(command)
            ? (modelTargetCombo.SelectedValue as string ?? modelTargetCombo.Text ?? "").Trim()
            : "";
        rule.SceneId = NizimaCommandUi.ShowsScene(command)
            ? (sceneCombo.SelectedValue as string ?? sceneCombo.Text ?? "").Trim()
            : "";

        rule.Extra.ModelPath = command is NizimaTriggerCommands.ChangeModel or NizimaTriggerCommands.AddModel
            ? rule.CommandValue
            : "";
        rule.Extra.ItemPath = command == NizimaTriggerCommands.AddItem ? rule.CommandValue : "";

        if (command == NizimaTriggerCommands.ChangeModel &&
            (string.IsNullOrWhiteSpace(rule.ModelId) || string.IsNullOrWhiteSpace(rule.CommandValue)))
        {
            MessageBox.Show(this, "モデル切り替えには対象モデルと登録モデル Path が必要です。", Title);
            return false;
        }

        if (command == NizimaTriggerCommands.AddItem &&
            (string.IsNullOrWhiteSpace(rule.SceneId) || string.IsNullOrWhiteSpace(rule.CommandValue)))
        {
            MessageBox.Show(this, "アイテム追加には SceneId と ItemPath が必要です。", Title);
            return false;
        }

        return true;
    }

    private void SyncCommandValueUi()
    {
        var command = SelectedId(commandType);
        var showsCommandValue = NizimaCommandUi.ShowsCommandValue(command);
        commandValueLabel.Visibility = showsCommandValue ? Visibility.Visible : Visibility.Collapsed;
        commandValueHost.Visibility = showsCommandValue ? Visibility.Visible : Visibility.Collapsed;

        if (showsCommandValue)
        {
            if (NizimaCommandUi.UsesHotkey(command))
            {
                commandHotkey.Visibility = Visibility.Visible;
                commandCombo.Visibility = Visibility.Collapsed;
                commandValueLabel.Text = "コマンド値（欄を選択してキーを押す）";
                if (string.IsNullOrWhiteSpace(commandHotkey.Text))
                    commandHotkey.Text = InitialCommandValue(rule);
            }
            else
            {
                commandHotkey.Visibility = Visibility.Collapsed;
                commandCombo.Visibility = Visibility.Visible;
                commandValueLabel.Text = CommandValueLabel(command);
                ReplaceCommandOptions(command);
                NizimaNamedOptionCatalog.SelectComboValue(commandCombo, commandOptions, InitialCommandValue(rule));
            }
        }

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

        if (NizimaCommandUi.ShowsScene(command))
        {
            sceneLabel.Visibility = Visibility.Visible;
            sceneCombo.Visibility = Visibility.Visible;
            sceneLabel.Text = command == NizimaTriggerCommands.AddItem
                ? "SceneId（必須）"
                : "SceneId（空なら新規ウィンドウ）";
            var scenes = command == NizimaTriggerCommands.AddModel
                ? NizimaRuleCatalogs.WithNewWindowSceneOption(catalogs.Scenes)
                : catalogs.Scenes;
            NizimaNamedOptionCatalog.ReplaceAll(sceneOptions, scenes);
            NizimaNamedOptionCatalog.SelectComboValue(sceneCombo, sceneOptions, rule.SceneId);
        }
        else
        {
            sceneLabel.Visibility = Visibility.Collapsed;
            sceneCombo.Visibility = Visibility.Collapsed;
        }
    }

    private static string CommandValueLabel(string? command) =>
        command switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff => "表情",
            NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion => "モーション",
            NizimaTriggerCommands.ChangeModel or NizimaTriggerCommands.AddModel => "登録モデル（ModelPath）",
            NizimaTriggerCommands.AddItem => "登録アイテム（ItemPath）",
            NizimaTriggerCommands.RemoveItem => "画面上のアイテム（ItemId）",
            NizimaTriggerCommands.EffectOn or NizimaTriggerCommands.EffectOff => "エフェクト GroupId",
            _ => "コマンド値"
        };

    private void ReplaceCommandOptions(string? command)
    {
        IReadOnlyList<NizimaNamedOption> items = command switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff => catalogs.Expressions,
            NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion => catalogs.Motions,
            NizimaTriggerCommands.ChangeModel or NizimaTriggerCommands.AddModel => catalogs.RegisteredModels,
            NizimaTriggerCommands.AddItem => catalogs.RegisteredItems,
            NizimaTriggerCommands.RemoveItem => catalogs.ItemsOnScreen,
            NizimaTriggerCommands.EffectOn or NizimaTriggerCommands.EffectOff => catalogs.EffectGroups,
            _ => []
        };
        NizimaNamedOptionCatalog.ReplaceAll(commandOptions, items);
        commandCombo.IsEditable = items.Count == 0;
    }

    private static string InitialCommandValue(NizimaTriggerRule rule) =>
        rule.CommandType switch
        {
            NizimaTriggerCommands.ChangeModel or NizimaTriggerCommands.AddModel =>
                FirstNonEmpty(rule.CommandValue, rule.Extra.ModelPath),
            NizimaTriggerCommands.AddItem => FirstNonEmpty(rule.CommandValue, rule.Extra.ItemPath),
            _ => rule.CommandValue
        };

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
}
