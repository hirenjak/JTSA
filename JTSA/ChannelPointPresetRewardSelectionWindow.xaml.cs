using JTSA.Forms;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Windows;

namespace JTSA;

public partial class ChannelPointPresetRewardSelectionWindow : Window
{
    private readonly HashSet<string> existingRewardIds;

    public ObservableCollection<RewardChoice> Choices { get; } = [];

    public IReadOnlyList<ChannelPointRewardForm> SelectedRewards => Choices
        .Where(x => x.IsSelected)
        .Select(x => x.Reward)
        .ToList();

    public ChannelPointPresetRewardSelectionWindow(IEnumerable<string> existingRewardIds)
    {
        this.existingRewardIds = existingRewardIds.ToHashSet();
        InitializeComponent();
        DataContext = this;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var result = await ChannelPointService.FetchRewardsAsync();
        if (result == null)
        {
            StatusTextBlock.Text = "CP一覧を取得できませんでした。";
            return;
        }

        foreach (var reward in result.Rewards.Where(
                     x => x.IsManageable && !existingRewardIds.Contains(x.RewardId)))
        {
            Choices.Add(new RewardChoice(reward));
        }

        if (Choices.Count == 0)
        {
            StatusTextBlock.Text = "登録できるCPはありません。";
            return;
        }

        StatusTextBlock.Text = "登録するCPを選択してください。";
        RewardListBox.IsEnabled = true;
        RegisterButton.IsEnabled = true;
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRewards.Count == 0)
        {
            MessageBox.Show(this, "登録するCPを選択してください。", "CPを登録");
            return;
        }

        DialogResult = true;
    }

    public sealed class RewardChoice(ChannelPointRewardForm reward)
    {
        public ChannelPointRewardForm Reward { get; } = reward;
        public bool IsSelected { get; set; }
    }
}
