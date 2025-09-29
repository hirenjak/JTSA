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
        public void ReloadChannnelPoint()
        {
            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "チャンネルポイントリスト再読み込み", "処理開始");

            // DB接続と初期化処理
            using var db = new AppDbContext();
            ChannelPointGetStatus.Text = "hi!!!";

            // データの取得
            var records = M_TitleTag.SelectAllOrderbyLastUser();

            // 画面データ入れ換え処理

            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "チャンネルポイントリスト再読み込み", "処理終了");
        }
    }
}
