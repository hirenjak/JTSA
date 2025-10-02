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
        public async void ReloadChannnelPoint()
        {
            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "チャンネルポイントリスト再読み込み", "処理開始");
            ChannelPointGetStatus.Text = "チャンネルポイント取得中..."; // 処理中のメッセージ
            ChannelPointListView.ItemsSource = null; // 事前にリストをクリア

            // データの取得し、結果をrewards変数に格納
            var rewards = await TwitchHelper.GetCustomRewardsAsync();

            // 取得結果のチェック
            if (rewards != null)
            {
                // 取得成功時、ListViewのItemsSourceにデータリストを設定
                rewards.Sort((a, b) => a.Cost.CompareTo(b.Cost)); // コストで昇順ソート
                ChannelPointListView.ItemsSource = rewards;
                ChannelPointGetStatus.Text = $"取得成功！ ({rewards.Count}件)";
                mainWindow.AppLogPanel.AddSuccessLog(GetType().Name, "チャンネルポイントリスト取得成功");
            }
            else
            {
                // 取得失敗時
                ChannelPointGetStatus.Text = "チャンネルポイントの取得に失敗しました。";
                mainWindow.AppLogPanel.AddErrorLog(GetType().Name, "チャンネルポイントリスト取得失敗");
            }

            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "チャンネルポイントリスト再読み込み", "処理終了");
        }
    }
}
