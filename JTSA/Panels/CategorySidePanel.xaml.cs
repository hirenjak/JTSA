using JTSA.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>
    /// 登録カテゴリ管理パネル
    /// </summary>
    public partial class CategorySidePanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> CategoryFormList { get; } = new();


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CategorySidePanel()
        {
            DataContext = this;

            InitializeComponent();
        }

        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO：カテゴリ検索処理追加
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is CategoryForm selectedItem)
            {
                mainWindow.SelectCategpryIdTextBlock.Text = selectedItem.CategoryId;
                mainWindow.SelectCategpryNameTextBlock.Text = selectedItem.DisplayName;
            }

            // 選択状態を解除
            CategoryListBox.SelectedIndex = -1;
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                M_Category.Delete(item.CategoryId);
            }

            ReloadCategory();
        }


        /// <summary>
        /// 読み込み処理：カテゴリ
        /// </summary>
        public void ReloadCategory()
        {
            // DB接続と初期化処理
            using var db = new AppDbContext();
            CategoryFormList.Clear();

            // データの取得
            var records = M_Category.SelectAllOrderbyLastUser(db);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                CategoryFormList.Add(new()
                {
                    CategoryId = item.CategoryId,
                    DisplayName = item.DisplayName,
                    BoxArtUrl = item.BoxArtUrl,
                    LastUsedDate = item.LastUseDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }

            mainWindow.StatusTextBlock.Text = "カテゴリリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }


        /// <summary>
        /// カテゴリテーブル：挿入更新処理
        /// </summary>
        /// <param name="title"></param>
        public void AddCategory(String gameId, String displayName, String boxArtUrl)
        {
            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (string.IsNullOrWhiteSpace(displayName)) return;

            // データ作成
            var isnertData = new M_Category
            {
                CategoryId = gameId,
                DisplayName = displayName,
                BoxArtUrl = boxArtUrl,
                CountSelected = 0,
                SortNumber = 0,
                IsDeleted = false,
                LastUseDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdateDateTime = DateTime.Now
            };

            // 挿入処理
            mainWindow.DisplayLog(M_Category.Insert(db, isnertData),
                "データを追加しました。",
                "既にデータが存在します。"
            );

            // 再読み込み処理
            ReloadCategory();
        }
    }
}
