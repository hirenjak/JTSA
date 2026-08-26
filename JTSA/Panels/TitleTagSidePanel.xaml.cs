using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>
    /// タイトルテキストタグ管理パネル
    /// </summary>
    public partial class TitleTagSidePanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<TitleTagForm> TitleTagFormList { get; } = new();


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TitleTagSidePanel()
        {
            DataContext = this;

            InitializeComponent();
        }


        /// <summary>
        /// リストボックスアイテム選択時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TitleTagListBox.SelectedItem is TitleTagForm selectedItem)
            {
                mainWindow.InsertTextAtCaret(selectedItem.Placeholder);
                if (!selectedItem.IsSystem)
                {
                    DAO_TitleTag.UpdateLastUse(selectedItem.Id);
                    ReloadTitleTag();
                }
            }

            // 選択状態を解除
            TitleTagListBox.SelectedItem = null;
        }

        /// <summary>
        /// リストボックスアイテムクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // クリックされたアイテムを取得
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var item = ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item == null) return;

            // すでに選択されている場合は一度選択解除
            if (item != null && item.IsSelected)
            {
                listBox.SelectedIndex = -1;
            }
        }


        /// <summary>
        /// 削除ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if (sender is Button { DataContext: TitleTagForm { IsSystem: false } item })
            {
                DAO_TitleTag.Delete(item.Id);
            }

            ReloadTitleTag();
        }


        /// <summary>
        /// 追加ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TitleTagAddButton_Click(object sender, RoutedEventArgs e)
        {
            String displayName = TitleTextTagAddTextBox.Text.Trim();
            AddTitleTag(displayName);
        }



        /// <summary>
        /// 読み込み処理：タイトルタグ
        /// </summary>
        public void ReloadTitleTag()
        {
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(GetType().Name, "タイトルタグリスト再読み込み");

            // DB接続と初期化処理
            using var db = new AppDbContext();
            TitleTagFormList.Clear();

            TitleTagFormList.Add(new()
            {
                Placeholder = "${title}",
                IsSystem = true,
                DisplayName = "配信タイトル",
                LastUsedDate = string.Empty
            });
            TitleTagFormList.Add(new()
            {
                Placeholder = "${date}",
                IsSystem = true,
                DisplayName = "今日の日付（yyyy/MM/dd）",
                LastUsedDate = string.Empty
            });
            TitleTagFormList.Add(new()
            {
                Placeholder = "${category_ja}",
                IsSystem = true,
                DisplayName = "カテゴリ名（日本語）",
                LastUsedDate = string.Empty
            });
            TitleTagFormList.Add(new()
            {
                Placeholder = "${friend}",
                IsSystem = true,
                DisplayName = "選択したフレンド",
                LastUsedDate = string.Empty
            });

            // データの取得
            var records = DAO_TitleTag.SelectAllOrderbyLastUser();

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                TitleTagFormList.Add(new()
                {
                    Id = item.Id,
                    Placeholder = $"${{{item.Id}}}",
                    IsSystem = false,
                    DisplayName = item.DisplayName,
                    LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm")
                });
            }

            mainWindow.AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
        }


        /// <summary>
        /// タイトルタグテーブル：挿入処理
        /// </summary>
        /// <param name="title"></param>
        private void AddTitleTag(string displayName)
        {
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(GetType().Name, "タイトルタグDB追加");

            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (string.IsNullOrWhiteSpace(displayName)) return;

            // データ作成
            var isnertData = new M_TitleTag
            {
                DisplayName = displayName,
                SelectedCount = 0,
                SortNumber = 0,
                LastUsedDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            };

            // 挿入処理
            mainWindow.AppLogPanel.AddSwitchLog(DAO_TitleTag.Insert(isnertData), GetType().Name,
                $"【 DB追加 】 成功：{isnertData.DisplayName}",
                "【 DB追加 】 既存データと競合"
            );

            // 再読み込み処理
            ReloadTitleTag();

            mainWindow.AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
        }
    }    
}
