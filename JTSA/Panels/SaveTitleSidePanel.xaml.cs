using JTSA.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    public partial class SaveTitleSidePanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<TitleTextForm> SaveTitleTextFormList { get; } = new();

        public SaveTitleSidePanel()
        {
            InitializeComponent();
        }


        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO：お気に入りタイトル検索処理追加
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SaveTitleListBox.SelectedItem is TitleTextForm selectedItem)
            {
                mainWindow.editTitleTextForm.Content = selectedItem.Content;
                mainWindow.editTitleTextForm.CategoryId = selectedItem.CategoryId;
                mainWindow.editTitleTextForm.CategoryName = selectedItem.CategoryName;

                mainWindow.SetEditTitleTextForm();
            }

            // 選択状態を解除
            SaveTitleListBox.SelectedItem = null;
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveTitleDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is TitleTextForm item)
            {
                var targetData = M_TitleText.SelectOneById(item.Id);

                targetData.SortNumber = 0;

                M_TitleText.Update(targetData);
            }

            ReloadSaveTitleText();
        }


        /// <summary>
        /// 読み込み処理：カテゴリ
        /// </summary>
        public void ReloadSaveTitleText()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            SaveTitleTextFormList.Clear();

            // データの取得
            var records = M_TitleText.SelectSaveDataOrderbyLastUser();

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                SaveTitleTextFormList.Add(new()
                {
                    CategoryId = item.CategoryId,
                    Id = item.Id,
                    Content = item.Content,
                    CategoryName = item.CategoryName,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }

            mainWindow.StatusTextBlock.Text = "保存タイトルリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}
