using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    public partial class CategorySearchSidePanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> SearchCategoryFormList { get; } = new();


        private System.Windows.Threading.DispatcherTimer categorySearchDebounceTimer;
        private string lastCategorySearchText = "";

        public CategorySearchSidePanel()
        {

            categorySearchDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            categorySearchDebounceTimer.Interval = TimeSpan.FromSeconds(1);
            categorySearchDebounceTimer.Tick += CategorySearchDebounceTimer_Tick;

            InitializeComponent();
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
                mainWindow.editTitleTextForm.CategoryId = selectedItem.CategoryId;
                mainWindow.editTitleTextForm.CategoryName = selectedItem.DisplayName;
                mainWindow.editTitleTextForm.BoxArtUrl = selectedItem.BoxArtUrl;

                mainWindow.SetEditTitleTextForm();
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
                    LastUsedDate = ""
                });
            }

            mainWindow.StatusTextBlock.Text = "検索カテゴリリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}
