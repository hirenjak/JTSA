using JTSA.Dao;
using JTSA.Forms;
using JTSA.Forms.TwitchIF;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
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

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CategoryPanel()
        {
            InitializeComponent();

            DataContext = this;

            ReloadCategory();
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryListBox_MouseDoubleClick(object sender, EventArgs e)
        {
            if (CategoryListBox.SelectedItem is not CategoryForm selectedItem) return;

            mainWindow.CurrentCategoryId = selectedItem.CategoryId;
            mainWindow.CurrentCategoryName = selectedItem.DisplayName;
            mainWindow.CurrentCategoryBoxArtUrl = selectedItem.BoxArtUrl;
            mainWindow.CurrentCategorySteamUrl = selectedItem.SteamUrl;

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
                DAO_Category.Delete(item.CategoryId);
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
            var records = DAO_Category.SelectAllOrderbyLastUser();

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                CategoryFormList.Add(new()
                {
                    CategoryId = item.CategoryId,
                    DisplayName = item.DisplayName,
                    BoxArtUrl = item.BoxArtUrl,
                    SteamUrl = item.SteamUrl ?? "",
                    LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }

            mainWindow.StatusTextBlock.Text = "カテゴリリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
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
                M_Category? updateCategory = DAO_Category.SelectOneById(item.CategoryId);
                if (updateCategory == null) return;

                updateCategory.SteamUrl = item.SteamUrl;

                string? appId = SteamHelper.GetSteamAppId(item.SteamUrl);
                if (appId == null) return;

                updateCategory.SteamHeaderArtUrl = await SteamHelper.GetSteamHeaderImageUrlAsync(appId);
                DAO_Category.Update(updateCategory);
            }

            ReloadCategory();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySetButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                mainWindow.CurrentCategoryId = item.CategoryId;
                mainWindow.CurrentCategoryName = item.DisplayName;
                mainWindow.CurrentCategoryBoxArtUrl = item.BoxArtUrl;
            }

            ReloadCategory();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PlayCategoryAddButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is CategoryForm item)
            {
                M_Category category = DAO_Category.SelectOneById(item.CategoryId);
                await mainWindow.PlayingGamePanel.AddSteamImageAsync(item.CategoryId, category.SteamHeaderArtUrl);
            }

            ReloadCategory();
        }
    }
}
