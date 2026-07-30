using JTSA.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>
    /// UserPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class CategoryPanel : UserControl
    {

        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> CategoryFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> SearchCategoryFormList { get; } = new();

        private System.Windows.Threading.DispatcherTimer categorySearchDebounceTimer;
        private string lastCategorySearchText = "";


        public CategoryPanel()
        {
            InitializeComponent();

            DataContext = this;

            categorySearchDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            categorySearchDebounceTimer.Interval = TimeSpan.FromSeconds(1);
            categorySearchDebounceTimer.Tick += CategorySearchDebounceTimer_Tick;

            ReloadCategory();
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
        private void CategoryListBox_SelectionChanged(object sender, EventArgs e)
        {
            if (CategoryListBox.SelectedItem is not CategoryForm selectedItem)
                return;

            mainWindow.editTitleTextForm.SetCategory(selectedItem.CategoryId, selectedItem.DisplayName, selectedItem.BoxArtUrl);
            mainWindow.SetDisplayFromEditFrom();

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
            var records = M_Category.SelectAllOrderbyLastUser();

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                CategoryFormList.Add(new()
                {
                    CategoryId = item.CategoryId,
                    DisplayName = item.DisplayName,
                    BoxArtUrl = item.BoxArtUrl,
                    SteamUrl = item.SteamUrl,
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
                SteamHeaderUrl = "",
                SteamUrl = "",
                CountSelected = 0,
                SortNumber = 0,
                IsDeleted = false,
                LastUseDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdateDateTime = DateTime.Now
            };

            // 挿入処理
            mainWindow.AppLogPanel.AddSwitchLog(M_Category.Insert(isnertData), GetType().Name,
                "データを追加しました。",
                "既にデータが存在します。"
            );

            // 再読み込み処理
            ReloadCategory();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SteamURLUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                M_Category updateCategory = M_Category.SelectOneByCategoryId(item.CategoryId);
                updateCategory.SteamUrl = item.SteamUrl;

                string appId = PlayingGamePanel.GetSteamAppId(item.SteamUrl);
                updateCategory.SteamHeaderUrl = await PlayingGamePanel.GetSteamHeaderImageUrlAsync(appId);
                M_Category.Update(updateCategory);
            }

            ReloadCategory();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySeteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                mainWindow.editTitleTextForm.SetCategory(item.CategoryId, item.DisplayName, item.BoxArtUrl);
                mainWindow.SetDisplayFromEditFrom();
            }

            ReloadCategory();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayCategoryAddButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                M_Category category = M_Category.SelectOneByCategoryId(item.CategoryId);
                mainWindow.PlayingGamePanel.AddSteamImageAsync(item.CategoryId, category.SteamHeaderUrl);
            }

            ReloadCategory();
        }


        /// <summary>
        /// 検索遅延タイマー処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            categorySearchDebounceTimer.Stop();
            ReloadSearchCategory(lastCategorySearchText);
        }


        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchTitleSerchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            lastCategorySearchText = CategorySearchTitleSerchTextBox.Text;
            categorySearchDebounceTimer.Stop();
            categorySearchDebounceTimer.Start();
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategorySearchListBox.SelectedItem is CategoryForm selectedItem)
            {
                mainWindow.editTitleTextForm.SetCategory(selectedItem.CategoryId, selectedItem.DisplayName, selectedItem.BoxArtUrl);
                mainWindow.SetDisplayFromEditFrom();
            }
        }


        /// <summary>
        /// 読込処理：検索カテゴリ
        /// </summary>
        private async void ReloadSearchCategory(String searchText)
        {
            // 初期化処理
            SearchCategoryFormList.Clear();

            // データの取得
            var results = await TwitchHelper.SearchCategoriesByGameNameAsync(searchText);

            // 画面データ入れ換え処理
            foreach (var item in results)
            {
                SearchCategoryFormList.Add(new()
                {
                    CategoryId = item.Id,
                    DisplayName = item.Name,
                    BoxArtUrl = item.BoxArtUrl,
                    SteamUrl = "",
                    LastUsedDate = ""
                });
            }

            mainWindow.StatusTextBlock.Text = "検索カテゴリリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}
