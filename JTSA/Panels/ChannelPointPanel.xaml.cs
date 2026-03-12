using JTSA.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TwitchLib.Api.Helix.Models.ChannelPoints; // CustomReward用
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace JTSA.Panels
{
    /// <summary>
    /// ChannelPointPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class ChannelPointPanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
        public ChannelPointPanel()
        {
            InitializeComponent();
        }

        // ★追加：最後にソートした列と方向を記憶するための変数
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        // キャッシュされた報酬のリスト
        private List<CustomReward> _cachedRewards = null;

        // ★追加：ヘッダークリック時のイベントハンドラ
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                // ヘッダーに対応するプロパティ名を取得
                string sortBy = "";
                if (headerClicked.Column.DisplayMemberBinding is Binding binding)
                {
                    sortBy = binding.Path.Path;
                }
                // 画像など、DisplayMemberBinding以外を使っている列の場合の対応
                else if (headerClicked.Column.Header.ToString() == "有効")
                {
                    sortBy = "IsEnabled";
                }
                else if (headerClicked.Column.Header.ToString() == "一時停止")
                {
                    sortBy = "IsPaused";
                }

                if (string.IsNullOrEmpty(sortBy)) return;

                // ソート方向を決定
                ListSortDirection direction;
                if (headerClicked != _lastHeaderClicked)
                {
                    // IsEnabled, IsPaused の場合は初回降順、それ以外は昇順
                    if (sortBy == "IsEnabled" || sortBy == "IsPaused")
                        direction = ListSortDirection.Descending;
                    else
                        direction = ListSortDirection.Ascending;
                }
                else
                {
                    direction = _lastDirection == ListSortDirection.Ascending ?
                                ListSortDirection.Descending : ListSortDirection.Ascending;
                }

                // ListViewのItemsSourceからCollectionViewを取得してソートを適用
                var dataView = CollectionViewSource.GetDefaultView(ChannelPointListView.ItemsSource);
                dataView.SortDescriptions.Clear();
                dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
                dataView.Refresh();

                // 今回のソート情報を記憶
                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }
        public async void ReloadChannnelPoint(bool forceReload = false)
        {
            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "チャンネルポイントリスト再読み込み", "処理開始");
            ChannelPointGetStatus.Text = "チャンネルポイント取得中...";
            ChannelPointListView.ItemsSource = null;

            // キャッシュがなければ取得、forceReload指定時は再取得
            if (_cachedRewards == null || forceReload)
            {
                _cachedRewards = await TwitchHelper.GetCustomRewardsAsync();
            }
            var rewards = _cachedRewards;

            string info = "※画像追加はTwitch公式UIのみ対応です。画像サイズ調整ツール: https://xipher.booth.pm/items/6573903";

            if (rewards != null)
            {
                rewards.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                ChannelPointListView.ItemsSource = rewards;
                ChannelPointGetStatus.Text = $"取得成功！ ({rewards.Count}件)\n{info}";
                mainWindow.AppLogPanel.AddSuccessLog(GetType().Name, "チャンネルポイントリスト取得成功");
            }
            else
            {
                ChannelPointGetStatus.Text = $"チャンネルポイントの取得に失敗しました。\n{info}";
                mainWindow.AppLogPanel.AddErrorLog(GetType().Name, "チャンネルポイントリスト取得失敗");
            }

            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "チャンネルポイントリスト再読み込み", "処理終了");
        }

        // 有効/無効トグル
        private async void ToggleIsEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is CustomReward reward)
            {
                // キャッシュから該当する CustomReward を取得
                var cached = _cachedRewards?.FirstOrDefault(r => r.Id == reward.Id);
                if (cached == null)
                {
                    MessageBox.Show("キャッシュから該当のリワードが見つかりませんでした");
                    return;
                }

                var request = new UpdateCustomRewardRequest
                {
                    // キャッシュの値を元に反転
                    IsEnabled = !cached.IsEnabled
                };
                var updated = await TwitchHelper.UpdateCustomRewardAsync(cached.Id, request);
                if (updated != null)
                {
                    // キャッシュをクリアして再取得
                    _cachedRewards = null;
                    ReloadChannnelPoint();
                }
                else
                {
                    MessageBox.Show("有効/無効の切り替えに失敗しました");
                }
            }
        }

        // 一時停止トグル
        private async void ToggleIsPaused_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelPointListView.SelectedItem is CustomReward reward)
            {
                var request = new UpdateCustomRewardRequest
                {
                    IsPaused = !reward.IsPaused
                };
                var updated = await TwitchHelper.UpdateCustomRewardAsync(reward.Id, request);
                if (updated != null)
                {
                    ReloadChannnelPoint();
                }
                else
                {
                    MessageBox.Show("一時停止の切り替えに失敗しました");
                }
            }
        }

        // 新規作成ボタン押下
        private void CreateRewardButton_Click(object sender, RoutedEventArgs e)
        {
            RewardFormPanel.Visibility = Visibility.Visible;
            RewardNameTextBox.Text = "";
            RewardCostTextBox.Text = "";
            RewardImageUrlTextBox.Text = "";
        }

        // キャンセルボタン押下
        private void CreateRewardCancelButton_Click(object sender, RoutedEventArgs e)
        {
            RewardFormPanel.Visibility = Visibility.Collapsed;
        }

        // 作成ボタン押下
        private async void CreateRewardSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string name = RewardNameTextBox.Text.Trim();
            string costText = RewardCostTextBox.Text.Trim();
            string imageUrl = RewardImageUrlTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || !int.TryParse(costText, out int cost) || cost < 1)
            {
                MessageBox.Show("名前と正しいコストを入力してください。");
                return;
            }

            var req = new CreateCustomRewardsRequest
            {
                Title = name,
                Cost = cost,
                // 画像URLはTwitch APIの仕様上、作成時には直接指定できません（TwitchのUIでのみ設定可能）。
                // ここでは説明用にプロンプトや他の項目を追加できます。
                Prompt = "",
                IsEnabled = true
            };

            var result = await JTSA.TwitchHelper.CreateCustomRewardAsync(req);
            if (result != null && result.Count > 0)
            {
                MessageBox.Show("作成しました。");
                RewardFormPanel.Visibility = Visibility.Collapsed;
                _cachedRewards = null;
                ReloadChannnelPoint(true);
            }
            else
            {
                MessageBox.Show("作成に失敗しました。");
            }
        }
    }
}
