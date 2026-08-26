using JTSA.Dao;
using JTSA.Forms;
using JTSA.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
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
    /// CategorySearch.xaml の相互作用ロジック
    /// </summary>
    public partial class CategorySearchPanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>
        /// 検索結果で選択したカテゴリを現在のプレイリストにも追加するか。
        /// カテゴリ画面では追加し、配信概要画面では追加しない。
        /// </summary>
        public bool AddToPlaylistOnSelect { get; set; } = true;

        private string lastCategorySearchText = "";
        private System.Windows.Threading.DispatcherTimer categorySearchDebounceTimer;

        /// <summary>  </summary>
        public ObservableCollection<CategoryForm> SearchCategoryFormList { get; } = new();

        public CategorySearchPanel()
        {
            InitializeComponent();
            DataContext = this;

            categorySearchDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            categorySearchDebounceTimer.Interval = TimeSpan.FromSeconds(1);
            categorySearchDebounceTimer.Tick += CategorySearchDebounceTimer_Tick;
        }

        /// <summary>
        /// 検索テキスト文字入力時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategorySearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            lastCategorySearchText = CategorySearchTitleSerchTextBox.Text;
            categorySearchDebounceTimer.Stop();
            categorySearchDebounceTimer.Start();
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
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CategorySearchListBox_DoubleClick(object sender, EventArgs e)
        {
            if (CategorySearchListBox.SelectedItem is CategoryForm selectedItem)
            {
                var existingCategory = DAO_Category.SelectOneById(selectedItem.CategoryId);

                if (existingCategory == null)
                {
                    // Steamに存在しないカテゴリ（Art等）ではURLが取れないため、空文字として扱う
                    List<string> steamUrls = await IgdbService.GetSteamUrlsAsync(selectedItem.CategoryId);
                    string steamUrl = steamUrls.FirstOrDefault() ?? "";

                    // 未登録カテゴリを新規登録
                    var insertData = await DAO_Category.InsertDataCreate(selectedItem.CategoryId, steamUrl);
                    if (insertData == null) return;

                    DAO_Category.Insert(insertData);
                }
                else
                {
                    // 登録済みカテゴリは最終使用日時を更新し、一覧の先頭へ移動させる
                    DAO_Category.UpdateLastUsed(selectedItem.CategoryId);
                }

                if (AddToPlaylistOnSelect)
                {
                    mainWindow.PlayingGamePanel.AddPlaylistItem(selectedItem.CategoryId);
                }

                // カテゴリタブと配信概要パネルの一覧は同じコレクションを参照しているため、
                // 登録直後に読み直して両方へ反映する。
                mainWindow.CategoryPanel.ReloadCategory();

                CategorySearchTitleSerchTextBox.Text = "";
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
                    JapaneseDisplayName = item.Name,
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
