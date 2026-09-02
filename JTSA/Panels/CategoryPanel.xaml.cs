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

        /// <summary> カテゴリに紐づけられるチャンネルポイントプリセットの選択肢 </summary>
        public ObservableCollection<ChannelPointPresetForm> ChannelPointPresetFormList { get; } = new();

        /// <summary> 「紐づけなし」を表すプリセットID（ComboBoxはnullを扱いにくいため0を使う） </summary>
        private const long PRESET_ID_NONE = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CategoryPanel()
        {
            InitializeComponent();

            DataContext = this;
        }

        public void Initialize()
        {
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

        private void CategoryAddButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new CategorySearchWindow
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();
            ReloadCategory();
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

        private void OpenObsCaptureDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CategoryForm item) return;

            var window = new ObsCaptureDestinationWindow(
                item.CategoryId,
                item.DisplayName,
                item.BoxArtUrl)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
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

            // プリセットの選択肢を先に用意する（カテゴリ行のComboBoxが参照するため）
            ReloadChannelPointPreset();

            // データの取得
            var records = DAO_Category.SelectAllOrderbyLastUser();
            var captureRules = DAO_ObsCaptureSetting.SelectRules()
                .ToDictionary(rule => rule.CategoryId, StringComparer.OrdinalIgnoreCase);

            // 画面データ入れ換え処理
            foreach (var item in records)
            {
                captureRules.TryGetValue(item.CategoryId, out var captureRule);
                CategoryFormList.Add(new()
                {
                    CategoryId = item.CategoryId,
                    DisplayName = item.DisplayName,
                    JapaneseDisplayName = string.IsNullOrWhiteSpace(item.JapaneseDisplayName)
                        ? item.DisplayName
                        : item.JapaneseDisplayName,
                    BoxArtUrl = item.BoxArtUrl,
                    SteamUrl = item.SteamUrl ?? "",
                    ObsCaptureSummary = captureRule is null
                        ? "未設定"
                        : $"{(captureRule.IsSubObs ? "サブOBS" : "メインOBS")} / {captureRule.InputName} / " +
                          (string.IsNullOrWhiteSpace(captureRule.DestinationValue)
                              ? "キャプチャ先未設定"
                              : FormatCaptureDestinationValue(captureRule.DestinationValue)),
                    ChannelPointPresetId = item.ChannelPointPresetId ?? PRESET_ID_NONE,
                    LastUsedDate = item.LastUsedDateTime.ToString("yyyy/MM/dd HH:mm")
                });
            }

            mainWindow.StatusTextBlock.Text = "カテゴリリストを読込";
            mainWindow.StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        }

        private static string FormatCaptureDestinationValue(string value)
        {
            var parts = value.Split(':');
            if (parts.Length < 3) return value.Replace("#3A", ":").Replace("#23", "#");
            var title = parts[0].Replace("#3A", ":").Replace("#23", "#");
            var executable = parts[^1].Replace("#3A", ":").Replace("#23", "#");
            return string.IsNullOrWhiteSpace(executable) ? title : $"[{executable}]: {title}";
        }


        /// <summary>
        /// カテゴリに紐づけるプリセットの選択肢を読み込み直す。
        /// CPタブでプリセットを増減した後にも呼ばれる。
        /// </summary>
        public void ReloadChannelPointPreset()
        {
            ChannelPointPresetFormList.Clear();

            // 「紐づけなし」を先頭に置く。これを選ぶとカテゴリ変更時に何も適用しない
            ChannelPointPresetFormList.Add(new ChannelPointPresetForm
            {
                PresetId = PRESET_ID_NONE,
                PresetName = "（自動適用しない）"
            });

            foreach (var header in DAO_ChannelPointPreset.SelectAllHeader())
            {
                ChannelPointPresetFormList.Add(new ChannelPointPresetForm
                {
                    PresetId = header.PresetId,
                    PresetName = header.PresetName
                });
            }
        }


        /// <summary>
        /// カテゴリ行のプリセット選択が変わったとき：紐づけを保存する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CategoryPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox)?.DataContext is not CategoryForm item) return;

            var updateCategory = DAO_Category.SelectOneById(item.CategoryId);
            if (updateCategory == null) return;

            var selectedPresetId = item.ChannelPointPresetId == PRESET_ID_NONE
                ? (long?)null
                : item.ChannelPointPresetId;

            // 一覧の再読込による初期バインドでもこのイベントは発火するため、
            // 実際に値が変わったときだけ書き込む
            if (updateCategory.ChannelPointPresetId == selectedPresetId) return;

            updateCategory.ChannelPointPresetId = selectedPresetId;

            var isSuccess = DAO_Category.Update(updateCategory);

            var presetName = ChannelPointPresetFormList
                .FirstOrDefault(x => x.PresetId == item.ChannelPointPresetId)?.PresetName ?? "";

            mainWindow.AppLogPanel.AddSwitchLog(isSuccess, GetType().Name,
                $"CPプリセット紐づけ 「 {item.DisplayName} 」→「 {presetName} 」",
                $"CPプリセット紐づけ失敗 「 {item.DisplayName} 」"
            );
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SteamURLUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CategoryForm item) return;

            button.IsEnabled = false;
            try
            {
                var steamUrls = await IgdbService.GetSteamUrlsAsync(item.CategoryId);
                var steamUrl = steamUrls.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(steamUrl))
                {
                    MessageBox.Show($"「{item.DisplayName}」のSteam URLを取得できませんでした。", "Steam URL再取得");
                    return;
                }

                var updateCategory = DAO_Category.SelectOneById(item.CategoryId);
                if (updateCategory == null) return;
                updateCategory.SteamUrl = steamUrl;
                updateCategory.UpdatedDateTime = DateTime.Now;

                var appId = SteamHelper.GetSteamAppId(steamUrl);
                updateCategory.SteamHeaderArtUrl = appId is null
                    ? null
                    : await SteamHelper.GetSteamHeaderImageUrlAsync(appId);

                if (!DAO_Category.Update(updateCategory))
                {
                    MessageBox.Show("Steam URLを保存できませんでした。", "Steam URL再取得");
                    return;
                }

                if (mainWindow.CurrentCategoryId == item.CategoryId)
                    mainWindow.CurrentCategorySteamUrl = steamUrl;
                ReloadCategory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Steam URLの再取得に失敗しました。\n{ex.GetBaseException().Message}", "Steam URL再取得");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void JapaneseNameEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CategoryForm item) return;

            var window = new JapaneseCategoryNameEditWindow(
                item.DisplayName,
                item.JapaneseDisplayName ?? string.Empty)
            {
                Owner = Window.GetWindow(this)
            };
            if (window.ShowDialog() != true) return;

            var category = DAO_Category.SelectOneById(item.CategoryId);
            if (category is null) return;

            var japaneseName = window.JapaneseName;
            if (category.JapaneseDisplayName == japaneseName) return;

            category.JapaneseDisplayName = japaneseName;
            category.UpdatedDateTime = DateTime.Now;
            var saved = DAO_Category.Update(category);
            mainWindow.AppLogPanel.AddSwitchLog(
                saved,
                GetType().Name,
                $"日本語カテゴリ名更新 「 {item.DisplayName} 」→「 {japaneseName} 」",
                $"日本語カテゴリ名更新失敗 「 {item.DisplayName} 」");

            if (saved && mainWindow.CurrentCategoryId == item.CategoryId)
                mainWindow.CurrentTitleTextUpdate();
            if (saved) ReloadCategory();
        }

        /// <summary>IGDBから日本向けカテゴリ名を再取得して保存する。</summary>
        private async void JapaneseNameFetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CategoryForm item)
                return;

            button.IsEnabled = false;
            try
            {
                var japaneseName = await IgdbService.GetJapaneseGameNameAsync(item.CategoryId);
                if (string.IsNullOrWhiteSpace(japaneseName))
                {
                    MessageBox.Show(
                        $"「{item.DisplayName}」の日本語カテゴリ名を取得できませんでした。",
                        "カテゴリ名取得");
                    return;
                }

                var category = DAO_Category.SelectOneById(item.CategoryId);
                if (category is null) return;
                category.JapaneseDisplayName = japaneseName.Trim();
                category.UpdatedDateTime = DateTime.Now;
                if (!DAO_Category.Update(category))
                {
                    MessageBox.Show("日本語カテゴリ名を保存できませんでした。", "カテゴリ名取得");
                    return;
                }

                if (mainWindow.CurrentCategoryId == item.CategoryId)
                    mainWindow.CurrentTitleTextUpdate();
                ReloadCategory();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"日本語カテゴリ名の取得に失敗しました。\n{ex.GetBaseException().Message}",
                    "カテゴリ名取得");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

    }
}
