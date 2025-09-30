using JTSA.Models;
using System;
using System.Collections.Generic;
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
