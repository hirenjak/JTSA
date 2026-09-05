using JTSA.Forms;
using JTSA.Utility;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace JTSA;

public partial class ChannelPointRewardCreateWindow : Window
{
    private readonly ChannelPointRewardForm? editingReward;

    public ChannelPointRewardCreateWindow(ChannelPointRewardForm? editingReward = null)
    {
        this.editingReward = editingReward;
        InitializeComponent();
        if (editingReward != null)
        {
            Title = "チャンネルポイント報酬を編集";
            WindowHeadingTextBlock.Text = "チャンネルポイント報酬を編集";
            CreateButton.Content = "保存";
            RewardNameTextBox.Text = editingReward.Title;
            RewardCostTextBox.Text = editingReward.Cost.ToString();
            RewardPromptTextBox.Text = editingReward.Prompt;
        }
        Loaded += (_, _) => RewardNameTextBox.Focus();
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = RewardNameTextBox.Text.Trim();
        var costText = RewardCostTextBox.Text.Trim();

        if (string.IsNullOrEmpty(name) || !int.TryParse(costText, out var cost) || cost < 1)
        {
            MessageBox.Show(this, "名前と1以上のコストを入力してください。", "入力内容を確認");
            return;
        }

        CreateButton.IsEnabled = false;
        if (editingReward != null)
        {
            var updateResult = await ChannelPointService.UpdateDetailsAsync(
                editingReward, name, RewardPromptTextBox.Text.Trim(), cost);
            CreateButton.IsEnabled = true;
            if (!updateResult.IsSuccess)
            {
                MessageBox.Show(this, $"更新に失敗しました。\n\n{updateResult.ErrorMessage}", "更新失敗");
                return;
            }

            DialogResult = true;
            return;
        }

        var result = await TwitchHelper.CreateCustomRewardAsync(new CreateCustomRewardsRequest
        {
            Title = name,
            Cost = cost,
            Prompt = RewardPromptTextBox.Text.Trim(),
            IsEnabled = true
        });
        CreateButton.IsEnabled = true;

        if (!result.IsSuccess)
        {
            MessageBox.Show(this, $"作成に失敗しました。\n\n{result.ErrorMessage}", "作成失敗");
            return;
        }

        MessageBox.Show(this, "作成しました。\n\n画像はTwitchの報酬設定画面から設定してください。", "作成完了");
        DialogResult = true;
    }

    private void RewardCostTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = Regex.IsMatch(e.Text, "[^0-9]");
    }
}
