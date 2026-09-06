using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTSA;

public partial class ChannelPointPresetRewardSelectionWindow : Window
{
    private readonly HashSet<string> existingRewardIds;
    private readonly IReadOnlyList<ChannelPointRewardForm> rewards;

    public ObservableCollection<RewardChoice> Choices { get; } = [];

    public IReadOnlyList<ChannelPointRewardForm> SelectedRewards =>
        RewardListBox.SelectedItem is RewardChoice { CanRegister: true } choice
            ? [choice.Reward]
            : [];

    public ChannelPointPresetRewardSelectionWindow(
        IEnumerable<string> existingRewardIds,
        IEnumerable<ChannelPointRewardForm> rewards)
    {
        this.existingRewardIds = existingRewardIds.ToHashSet();
        this.rewards = rewards.ToList();
        InitializeComponent();
        DataContext = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // CPタブで取得済みの一覧を使う。ここでAPIを再取得すると、操作可否の判定だけが
        // 一時的に失敗した際に全件が操作不可扱いとなり、空の一覧が表示されてしまう。
        foreach (var reward in rewards)
        {
            Choices.Add(new RewardChoice(
                reward,
                existingRewardIds.Contains(reward.RewardId)));
        }

        if (Choices.Count == 0)
        {
            StatusTextBlock.Text = rewards.Count == 0
                ? "CP一覧がありません。CP設定で一覧を更新してください。"
                : "このプリセットに追加できるCPはありません。";
            return;
        }

        var registerableCount = Choices.Count(x => x.CanRegister);
        StatusTextBlock.Text = registerableCount > 0
            ? $"登録するCPを選択してください。（追加可能 {registerableCount}件）"
            : "追加可能なCPはありません。登録済み／操作不可の状態を確認してください。";
        RewardListBox.IsEnabled = true;
        RegisterButton.IsEnabled = false;
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        RegisterSelectedReward();
    }

    private void RewardListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RegisterButton.IsEnabled =
            RewardListBox.SelectedItem is RewardChoice { CanRegister: true };
    }

    private void RewardListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(
                RewardListBox,
                e.OriginalSource as DependencyObject) is not ListBoxItem)
        {
            return;
        }

        RegisterSelectedReward();
    }

    private void RegisterSelectedReward()
    {
        if (RewardListBox.SelectedItem is not RewardChoice { CanRegister: true })
        {
            MessageBox.Show(this, "追加可能なCPを選択してください。", "CPを登録");
            return;
        }

        DialogResult = true;
    }

    public sealed class RewardChoice(ChannelPointRewardForm reward, bool isAlreadyRegistered)
    {
        public ChannelPointRewardForm Reward { get; } = reward;
        public bool CanRegister { get; } = reward.IsManageable && !isAlreadyRegistered;
        public string UnavailableReason { get; } = isAlreadyRegistered
            ? "登録済み"
            : reward.IsManageable ? string.Empty : "操作不可";
    }
}
