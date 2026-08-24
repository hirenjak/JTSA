using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Security.Policy;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TwitchLib.Api.Helix.Models.Entitlements;
using TwitchLib.Api.Helix.Models.Schedule;
using static JTSA.Dao.DAO_GamePlaylist;
using static JTSA.Forms.PlaylistItemForm;

namespace JTSA.Panels
{
    /// <summary>
    /// SchedulePanel.xaml の相互作用ロジック
    /// </summary>
    public partial class PlayingGamePanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        private long CurrentGamePlaylistId { 
            get 
            {
                if (string.IsNullOrEmpty(CurrentGamePlaylistIdTextBlock.Text))
                {
                    return 0;
                }
                else
                {
                    return long.Parse(CurrentGamePlaylistIdTextBlock.Text);
                }
            } 
            set { CurrentGamePlaylistIdTextBlock.Text = value.ToString(); }}
        public string CurrentGamePlaylistName { get { return GamePlayListTitleEdit.Text; } set { GamePlayListTitleEdit.Text = value; } }

        private ObservableCollection<PlaylistItemForm> playlistItemFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<PlaylistHeaderForm> playlistHeaderFormList { get; } = new();

        private ObsHttpServer? server;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PlayingGamePanel()
        {
            InitializeComponent();
            ImageItemsControl.ItemsSource = playlistItemFormList;
            GamePlaylistListBox.ItemsSource = playlistHeaderFormList;

            server = new ObsHttpServer(
                CreateObsHtml,
                CreateObsJson,
                () => mainWindow.ChatPanel.CreateObsChatHtml(),
                () => mainWindow.ChatPanel.CreateObsChatJson());

            _ = server.StartAsync();
        }


        #region ==================== イベントハンドラー ====================


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePlaylistListBox_DoubleClick(object sender, EventArgs e)
        {
            if (GamePlaylistListBox.SelectedItem is PlaylistHeaderForm selectedItem)
            {
                // データ登録
                CurrentGamePlaylistId = selectedItem.GamePlayListId;
                CurrentGamePlaylistName = selectedItem.GamePlayListName;
            }

            // 再読み込み処理
            ReloadGamePlaylistItem();
        }

        /// <summary>
        /// 登録済みカテゴリを現在のプレイリストへ追加する。
        /// </summary>
        private void ExistingCategoryListBox_DoubleClick(object sender, EventArgs e)
        {
            if (ExistingCategoryListBox.SelectedItem is not CategoryForm selectedItem) return;

            AddPlaylistItem(selectedItem.CategoryId);
            ExistingCategoryListBox.SelectedIndex = -1;
        }

        /// <summary>
        /// カテゴリ画面と同じ登録済みカテゴリ一覧を表示する。
        /// </summary>
        public void BindExistingCategoryList(ObservableCollection<CategoryForm> categoryFormList)
        {
            ExistingCategoryListBox.ItemsSource = categoryFormList;
        }


        /// <summary>
        /// ゲームプレイリスト新規保存ボタン押下時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePlayListSaveButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentGamePlaylistId = JTSAHelper.GetCurrentUnixTimestampMillis();
            CurrentGamePlaylistName = "";

            // データ作成
            var insertPlaylistHeader = FormConvertToPlaylistHeader();
            DAO_GamePlaylist.InsertUpdate(insertPlaylistHeader);

            ReloadPlaylistHeader();
            ReloadGamePlaylistItem();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                item.Status =
                    item.Status == GameStatus.Completed
                        ? GameStatus.None
                        : GameStatus.Completed;

                DAO_GamePlaylist.UpdatePlaylistItemStatus(CurrentGamePlaylistId, item.CategoryId, item.Status);

                DAO_GamePlaylist.UpdateLastUsed(CurrentGamePlaylistId);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                playlistItemFormList.Remove(item);

                DAO_GamePlaylist.DeleteItem(CurrentGamePlaylistId, item.CategoryId);
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ImageItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                item.Status = item.Status switch
                {
                    GameStatus.Playing => GameStatus.Interrupted,
                    GameStatus.Interrupted => GameStatus.None,
                    _ => GameStatus.Playing
                };

                if (item.Status == GameStatus.Playing)
                {
                    foreach (var otherPlayingItem in playlistItemFormList.Where(x =>
                        x != item && x.Status == GameStatus.Playing))
                    {
                        otherPlayingItem.Status = GameStatus.Interrupted;
                    }

                    DAO_GamePlaylist.SetPlaylistItemPlaying(CurrentGamePlaylistId, item.CategoryId);
                }
                else
                {
                    DAO_GamePlaylist.UpdatePlaylistItemStatus(CurrentGamePlaylistId, item.CategoryId, item.Status);
                }

                DAO_GamePlaylist.UpdateLastUsed(CurrentGamePlaylistId);

                if (item.Status == GameStatus.Playing)
                {
                    var categoryData = await TwitchHelper.GetCategoryByGameId(item.CategoryId);

                    mainWindow.CurrentCategoryId = categoryData.Id;
                    mainWindow.CurrentCategoryName = categoryData.Name;
                    mainWindow.CurrentCategoryBoxArtUrl = categoryData.BoxArtUrl;

                    // カテゴリに紐づくチャンネルポイントプリセットを適用する（紐づけが無ければ何もしない）
                    await mainWindow.ApplyChannelPointPresetForCategoryAsync(categoryData.Id);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePlaylistDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is PlaylistHeaderForm item)
            {
                DAO_GamePlaylist.DeleteGamePlayList(item.GamePlayListId);
            }

            // プレイリスト削除時は全て再読み込み
            ReloadPlaylistHeader();
            ReloadGamePlaylistItem();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePlayListTitleEdit_Click(object sender, EventArgs e)
        {
            var currentHeader = FormConvertToPlaylistHeader();
            DAO_GamePlaylist.InsertUpdate(currentHeader);

            // ヘッダー一覧の名前を修正する必要があるので再読み込み
            ReloadPlaylistHeader();
        }

        #endregion


        #region ==================== サムネイル ====================


        /// <summary>
        /// プレイリストに表示するサムネイルURLを決める。
        /// 見栄えの良いSteamのヘッダー画像を優先し、Steamに無いカテゴリ（Art等）は
        /// Twitchに登録されているボックスアートにフォールバックする。
        /// </summary>
        /// <param name="steamHeaderArtUrl">Steamのヘッダー画像URL（未取得ならnull）</param>
        /// <param name="boxArtUrl">Twitchのボックスアート URL</param>
        /// <returns>サムネイルURL。どちらも無ければ空文字</returns>
        private static string ResolveThumbnailUrl(string? steamHeaderArtUrl, string? boxArtUrl)
        {
            if (!string.IsNullOrWhiteSpace(steamHeaderArtUrl)) return steamHeaderArtUrl;

            // ボックスアートは縦長なのでタイル内では小さく表示される。解像度を確保するため大きめに要求する
            return TwitchHelper.ResizeBoxArtUrl(boxArtUrl, 285, 380);
        }


        /// <summary>
        /// カテゴリIDからサムネイルURLを解決する。DBに無ければTwitchから取り直す。
        /// </summary>
        /// <param name="categoryId">カテゴリID</param>
        /// <returns>サムネイルURL。解決できなければ空文字</returns>
        private static async Task<string> ResolveThumbnailUrlByCategoryIdAsync(string categoryId)
        {
            var categoryData = DAO_Category.SelectOneById(categoryId);

            if (categoryData != null)
            {
                return ResolveThumbnailUrl(categoryData.SteamHeaderArtUrl, categoryData.BoxArtUrl);
            }

            var twitchCategoryData = await TwitchHelper.GetCategoryByGameId(categoryId);

            return ResolveThumbnailUrl(null, twitchCategoryData?.BoxArtUrl);
        }

        /// <summary>
        /// プレイリストヘッダー用にTwitchカテゴリのボックスアートURLを解決する。
        /// Steamヘッダー画像は使用しない。
        /// </summary>
        private static async Task<string> ResolveBoxArtUrlByCategoryIdAsync(string categoryId)
        {
            var categoryData = DAO_Category.SelectOneById(categoryId);

            if (!string.IsNullOrWhiteSpace(categoryData?.BoxArtUrl))
            {
                return TwitchHelper.ResizeBoxArtUrl(categoryData.BoxArtUrl, 285, 380);
            }

            var twitchCategoryData = await TwitchHelper.GetCategoryByGameId(categoryId);
            return TwitchHelper.ResizeBoxArtUrl(twitchCategoryData?.BoxArtUrl, 285, 380);
        }

        #endregion


        #region ==================== DB関連メソッド ====================


        /// <summary>
        /// 画面情報をDBに保存
        /// </summary>
        public void UpdatePlaylistData()
        {
            // データ作成
            var insertPlaylistHeader = FormConvertToPlaylistHeader();
            var insertPlaylistItemList = FormConvertToPlalistItemList(insertPlaylistHeader.GamePlayListId);

            // 挿入処理
            DAO_GamePlaylist.InsertUpdate(insertPlaylistHeader, insertPlaylistItemList);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private T_GamePlaylistHeader FormConvertToPlaylistHeader()
        {
            T_GamePlaylistHeader result = null;

            var playlistTitleText = CurrentGamePlaylistName;

            if (string.IsNullOrEmpty(playlistTitleText))
            {
                playlistTitleText = "名称未設定";
            }

            var playlistId = CurrentGamePlaylistId;

            if (playlistId == 0)
            {
                playlistId = JTSAHelper.GetCurrentUnixTimestampMillis();
            }

            result = new T_GamePlaylistHeader
            {
                GamePlayListId = playlistId,
                GamePlayListName = playlistTitleText,
                ThumbnailCategoryUrl = playlistItemFormList.Count == 0 ? "" : playlistItemFormList[0].ImageUrl,
                SelectedCount = 0,
                SortNumber = 9999,
                LastUsedDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            };

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        private List<T_GamePlaylistItem> FormConvertToPlalistItemList(long playlistId)
        {
            List<T_GamePlaylistItem> resultList = [];
            foreach (var item in playlistItemFormList)
            {
                resultList.Add(
                    new T_GamePlaylistItem
                    {
                        GamePlayListId = playlistId,
                        CategoryId = item.CategoryId,
                        Status = (int)item.Status,
                        LastUsedDateTime = DateTime.Now,
                        CreatedDateTime = DateTime.Now,
                        UpdatedDateTime = DateTime.Now
                    });
            }

            return resultList;
        }

        #endregion


        public async void ReloadPlaylistHeader()
        {
            //　リストの初期化
            playlistHeaderFormList.Clear();

            // プレイリストヘッダ一覧の読込
            var gamePlayListHeaders = DAO_GamePlaylist.SelectAllHeader();
            if (gamePlayListHeaders.Count == 0) return;

            foreach (var gamePlayListHeader in gamePlayListHeaders)
            {
                // プレイリストヘッダーには、先頭カテゴリのTwitchボックスアートを使用する。
                // 保存済みURLやSteamヘッダー画像には依存せず、カテゴリIDから毎回解決し直す。
                var firstItem = DAO_GamePlaylist
                    .SelectGamePlaylistById(gamePlayListHeader.GamePlayListId)
                    .FirstOrDefault();

                var thumbnailUrl = firstItem == null
                    ? ""
                    : await ResolveBoxArtUrlByCategoryIdAsync(firstItem.CategoryId);

                playlistHeaderFormList.Add(new PlaylistHeaderForm()
                {
                    GamePlayListId = gamePlayListHeader.GamePlayListId,
                    GamePlayListName = gamePlayListHeader.GamePlayListName,
                    ImageUrl = thumbnailUrl,
                    LastUsedDate = gamePlayListHeader.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm"),
                    IsLoaded = false
                });
            }

            // 画面に何も設定されていないかの確認
            if (CurrentGamePlaylistId == 0)
            {
                CurrentGamePlaylistId = gamePlayListHeaders[0].GamePlayListId;
            }

            // ヘッダ情報の取得
            var gamePlaylist = DAO_GamePlaylist.SelectHeaderById(CurrentGamePlaylistId);

            // 取得したデータが無ければ再取得
            if (gamePlaylist == null)
            {
                CurrentGamePlaylistId = gamePlayListHeaders[0].GamePlayListId;
                gamePlaylist = DAO_GamePlaylist.SelectHeaderById(CurrentGamePlaylistId);
            }

            CurrentGamePlaylistName = gamePlaylist?.GamePlayListName ?? "";

        }

        /// <summary>
        /// プレイリスト一覧画面の再読み込み
        /// </summary>
        /// <returns></returns>
        public async void ReloadGamePlaylistItem()
        {
            //　リストの初期化
            playlistItemFormList.Clear();

            // 画面に設定されているプレイリストIDに紐づくプレイリストアイテムを取得
            var gamePlayListItems = DAO_GamePlaylist.SelectGamePlaylistById(CurrentGamePlaylistId);

            //
            foreach (var game in gamePlayListItems)
            {
                playlistItemFormList.Add(new PlaylistItemForm()
                {
                    CategoryId = game.CategoryId,
                    ImageUrl = await ResolveThumbnailUrlByCategoryIdAsync(game.CategoryId),
                    Status = (GameStatus)game.Status
                });
            }
        }


        /// <summary>
        /// カテゴリを現在のプレイリストに追加する。
        /// サムネイルは再読み込み時にカテゴリから解決するため、ここでは受け取らない。
        /// </summary>
        /// <param name="categoryId">追加するカテゴリID</param>
        public void AddPlaylistItem(string categoryId)
        {
            AddGamePlaylistItem(categoryId);

            ReloadGamePlaylistItem();
        }

        public void AddGamePlaylistItem(string categoryId)
        {
            var insertList = new List<T_GamePlaylistItem>();
            insertList.Add(new T_GamePlaylistItem()
            {
                GamePlayListId = CurrentGamePlaylistId,
                CategoryId = categoryId,
                Status = (int)GameStatus.None,
                LastUsedDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            });

            DAO_GamePlaylist.InsertItemList(insertList);

        }


        /// <summary>
        /// OBS用JSON作成
        /// </summary>
        /// <returns></returns>
        private string CreateObsJson()
        {
            string title = "";
            bool showTitle = false;

            Dispatcher.Invoke(() =>
            {
                title = CurrentGamePlaylistName;
                showTitle =  true;
            });

            var obj = new
            {
                showTitle,
                title,
                items = playlistItemFormList.Select(x => new
                {
                    imageUrl = x.ImageUrl,
                    status = x.Status.ToString(),
                }).ToList()
            };

            return JsonSerializer.Serialize(obj);
        }


        /// <summary>
        /// OBS用HTML作成
        /// </summary>
        /// <returns></returns>
        private string CreateObsHtml()
        {
            return """
        <!DOCTYPE html>
        <html lang="ja">
        <head>
            <meta charset="UTF-8">
            <title>OBS表示用</title>

            <style>
                html,
                body {
                    margin: 0;
                    padding: 0;
                    background: transparent;
                    overflow: hidden;
                    font-family: sans-serif;
                }

                .playlistTitle {
                    display: none;

                    margin: 0 0 10px 0;
                    padding: 8px 12px;

                    color: white;
                    background: rgba(0, 0, 0, 0.65);

                    font-size: 28px;
                    font-weight: bold;

                    box-sizing: border-box;
                }

                #imageList {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 16px;
                    align-items: flex-start;
                    padding: 0;
                }

                .imageItem {
                    position: relative;
                    display: inline-block;
                    overflow: hidden;
                    flex-shrink: 0;
                }

                .imageItem img {
                    display: block;
                    width: auto;
                    height: auto;
                    max-width: 230px;
                    max-height: 107px;
                }

                .completeText {
                    position: absolute;
                    inset: 0;

                    display: none;
                    align-items: center;
                    justify-content: center;

                    color: #00ff80;
                    background: rgba(0, 0, 0, 0.55);

                    font-size: 30px;
                    font-weight: bold;
                }

                .imageItem.completed .completeText {
                    display: flex;
                }

                .playingText {
                    position: absolute;
                    left: 4px;
                    bottom: 8px;

                    display: none;

                    padding: 2px 10px;
                    border-radius: 5px;

                    color: white;
                    background: #2196f3;

                    font-size: 11px;
                    font-weight: bold;

                    white-space: nowrap;
                }

                .imageItem.playing .playingText {
                    display: block;
                }

                .interruptedText {
                    position: absolute;
                    left: 4px;
                    bottom: 8px;

                    display: none;

                    padding: 2px 10px;
                    border-radius: 5px;

                    color: white;
                    background: #f59e0b;

                    font-size: 11px;
                    font-weight: bold;

                    white-space: nowrap;
                }

                .imageItem.interrupted .interruptedText {
                    display: block;
                }
            </style>
        </head>

        <body>
            <div id="playlistTitle" class="playlistTitle"></div>
            <div id="imageList"></div>

            <script>
                async function load() {
                    try {
                        const response = await fetch("/data?t=" + Date.now(), {
                            cache: "no-store"
                        });

                        if (!response.ok) {
                            throw new Error("データ取得失敗: " + response.status);
                        }

                        const data = await response.json();

                        const titleElement =
                            document.getElementById("playlistTitle");

                        if (data.showTitle && data.title) {
                            titleElement.style.display = "block";
                            titleElement.textContent = data.title;
                        }
                        else {
                            titleElement.style.display = "none";
                            titleElement.textContent = "";
                        }

                        const list = document.getElementById("imageList");
                        list.innerHTML = "";

                        for (const item of data.items ?? []) {
                            const div = document.createElement("div");
                            div.className = "imageItem";

                            if (item.status === "Completed") {
                                div.classList.add("completed");
                            }
                            else if (item.status === "Playing") {
                                div.classList.add("playing");
                            }
                            else if (item.status === "Interrupted") {
                                div.classList.add("interrupted");
                            }

                            const img = document.createElement("img");
                            img.src = item.imageUrl;
                            img.alt = "";

                            const completeText =
                                document.createElement("div");

                            completeText.className = "completeText";
                            completeText.textContent = "完了";

                            const playingText =
                                document.createElement("div");

                            playingText.className = "playingText";
                            playingText.textContent = "プレイ中";

                            const interruptedText =
                                document.createElement("div");

                            interruptedText.className = "interruptedText";
                            interruptedText.textContent = "プレイ中断中";

                            div.appendChild(img);
                            div.appendChild(completeText);
                            div.appendChild(playingText);
                            div.appendChild(interruptedText);

                            list.appendChild(div);
                        }
                    }
                    catch (error) {
                        console.error(error);
                    }
                }

                load();
                setInterval(load, 500);
            </script>
        </body>
        </html>
        """;
        }
    }
}
