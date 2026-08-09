using JTSA.Forms;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace JTSA.Panels
{
    /// <summary>
    /// ChannelPointPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class ChannelPointPanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary> 画面に表示している報酬一覧 </summary>
        public ObservableCollection<ChannelPointRewardForm> ChannelPointRewardFormList { get; } = [];

        /// <summary> 一覧の下に常時出す注意書き </summary>
        private const string INFO_TEXT = "※画像追加はTwitch公式UIのみ対応です。画像サイズ調整ツール: https://xipher.booth.pm/items/6573903";

        /// <summary> 最後にソートした列と方向 </summary>
        private GridViewColumnHeader? _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;


        public ChannelPointPanel()
        {
            InitializeComponent();

            // 画面紐づけ
            DataContext = this;
        }


        /// <summary>
        /// 遅延初期化。認証が終わっていないとAPIを叩けないため、
        /// コンストラクタではなくMainWindowの起動シーケンスから呼ばれる。
        /// </summary>
        public async Task Initialize()
        {
            await ReloadChannnelPoint();
        }


        /// <summary>
        /// ヘッダークリック時（列ソート）
        /// </summary>
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                // ヘッダーに対応するプロパティ名を取得
                string sortBy = "";
                if (headerClicked.Column?.DisplayMemberBinding is Binding binding)
                {
                    sortBy = binding.Path.Path;
                }
                // 画像など、DisplayMemberBinding以外を使っている列の場合の対応
                else if (headerClicked.Column?.Header?.ToString() == "有効")
                {
                    sortBy = nameof(ChannelPointRewardForm.IsEnabled);
                }
                else if (headerClicked.Column?.Header?.ToString() == "一時停止")
                {
                    sortBy = nameof(ChannelPointRewardForm.IsPaused);
                }
                else if (headerClicked.Column?.Header?.ToString() == "操作可能")
                {
                    sortBy = nameof(ChannelPointRewardForm.IsManageable);
                }

                if (string.IsNullOrEmpty(sortBy)) return;

                // ソート方向を決定
                ListSortDirection direction;
                if (headerClicked != _lastHeaderClicked)
                {
                    // 真偽値の列は初回降順、それ以外は昇順
                    if (sortBy == nameof(ChannelPointRewardForm.IsEnabled)
                     || sortBy == nameof(ChannelPointRewardForm.IsPaused)
                     || sortBy == nameof(ChannelPointRewardForm.IsManageable))
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


        /// <summary>
        /// 更新ボタン押下
        /// </summary>
        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            await ReloadChannnelPoint();
        }


        /// <summary>
        /// チャンネルポイント一覧をAPIから取り直して画面へ反映する
        /// </summary>
        public async Task ReloadChannnelPoint()
        {
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(GetType().Name, "チャンネルポイントリスト再読み込み");

            ReloadButton.IsEnabled = false;
            ChannelPointGetStatus.Text = "チャンネルポイント取得中...";

            var rewards = await ChannelPointService.FetchRewardsAsync();

            ChannelPointRewardFormList.Clear();

            if (rewards != null)
            {
                foreach (var reward in rewards)
                {
                    ChannelPointRewardFormList.Add(reward);
                }

                var lockedCount = rewards.Count(x => !x.IsManageable);
                var lockedText = lockedCount > 0
                    ? $" / 🔒 操作不可 {lockedCount}件（Twitchの Web 画面から作成された報酬です）"
                    : "";

                ChannelPointGetStatus.Text = $"取得成功！ ({rewards.Count}件){lockedText}\n{INFO_TEXT}";
                mainWindow.AppLogPanel.Success(GetType().Name, appLogProcessName);
            }
            else
            {
                ChannelPointGetStatus.Text = $"チャンネルポイントの取得に失敗しました。\n{INFO_TEXT}";
                mainWindow.AppLogPanel.Error(GetType().Name, "チャンネルポイントリスト取得失敗");
            }

            ReloadButton.IsEnabled = true;

            mainWindow.AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
        }


        /// <summary>
        /// 有効/無効トグル
        /// </summary>
        private async void ToggleIsEnabled_Click(object sender, RoutedEventArgs e)
        {
            // クリックされたチェックボックス自身の行を対象にする（選択行ではない）
            if (sender is not CheckBox checkBox || checkBox.DataContext is not ChannelPointRewardForm reward) return;

            // TwoWayバインドによりクリック時点でFormへ反映済み。その値をそのままAPIへ送る
            var requestValue = reward.IsEnabled;

            var isSuccess = await ChannelPointService.SetEnabledAsync(reward, requestValue);

            mainWindow.AppLogPanel.AddSwitchLog(isSuccess, GetType().Name,
                $"有効/無効の切り替え成功 「 {reward.Title} 」→ {(requestValue ? "有効" : "無効")}",
                $"有効/無効の切り替え失敗 「 {reward.Title} 」"
            );

            if (!isSuccess)
            {
                // 送信に失敗したので画面の見た目を元に戻す
                reward.IsEnabled = !requestValue;
                MessageBox.Show("有効/無効の切り替えに失敗しました");
            }
        }


        /// <summary>
        /// 一時停止トグル
        /// </summary>
        private async void ToggleIsPaused_Click(object sender, RoutedEventArgs e)
        {
            // クリックされたチェックボックス自身の行を対象にする（選択行ではない）
            if (sender is not CheckBox checkBox || checkBox.DataContext is not ChannelPointRewardForm reward) return;

            // TwoWayバインドによりクリック時点でFormへ反映済み。その値をそのままAPIへ送る
            var requestValue = reward.IsPaused;

            var isSuccess = await ChannelPointService.SetPausedAsync(reward, requestValue);

            mainWindow.AppLogPanel.AddSwitchLog(isSuccess, GetType().Name,
                $"一時停止の切り替え成功 「 {reward.Title} 」→ {(requestValue ? "一時停止" : "再開")}",
                $"一時停止の切り替え失敗 「 {reward.Title} 」"
            );

            if (!isSuccess)
            {
                // 送信に失敗したので画面の見た目を元に戻す
                reward.IsPaused = !requestValue;
                MessageBox.Show("一時停止の切り替えに失敗しました");
            }
        }


        /// <summary>
        /// 新規作成ボタン押下
        /// </summary>
        private void CreateRewardButton_Click(object sender, RoutedEventArgs e)
        {
            RewardFormPanel.Visibility = Visibility.Visible;
            RewardNameTextBox.Text = "";
            RewardCostTextBox.Text = "";
        }


        /// <summary>
        /// キャンセルボタン押下
        /// </summary>
        private void CreateRewardCancelButton_Click(object sender, RoutedEventArgs e)
        {
            RewardFormPanel.Visibility = Visibility.Collapsed;
        }


        /// <summary>
        /// 作成ボタン押下
        /// </summary>
        private async void CreateRewardSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string name = RewardNameTextBox.Text.Trim();
            string costText = RewardCostTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || !int.TryParse(costText, out int cost) || cost < 1)
            {
                MessageBox.Show("名前と正しいコストを入力してください。");
                return;
            }

            var req = new CreateCustomRewardsRequest
            {
                Title = name,
                Cost = cost,
                // 画像URLはTwitch APIの仕様上、作成時には直接指定できない（TwitchのWeb画面でのみ設定可能）
                Prompt = "",
                IsEnabled = true
            };

            var result = await TwitchHelper.CreateCustomRewardAsync(req);
            if (result != null && result.Count > 0)
            {
                MessageBox.Show("作成しました。");
                RewardFormPanel.Visibility = Visibility.Collapsed;
                await ReloadChannnelPoint();
            }
            else
            {
                MessageBox.Show("作成に失敗しました。");
            }
        }
    }
}
