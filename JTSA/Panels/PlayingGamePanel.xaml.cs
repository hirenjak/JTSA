using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                CreateObsJson);

            _ = server.StartAsync();
        }


        #region ==================== イベントハンドラー ====================


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GamePlaylistListBox_DoubleClick(object sender, EventArgs e)
        {
            if (GamePlaylistListBox.SelectedItem is PlaylistHeaderForm selectedItem)
            {
                // データ登録
                CurrentGamePlaylistId = selectedItem.GamePlayListId;
            }

            // 再読み込み処理
            await ReloadGameAllPlaylist();
        }


        /// <summary>
        /// ゲームプレイリスト新規保存ボタン押下時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GamePlayListSaveButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentGamePlaylistId = JTSAHelper.GetCurrentUnixTimestampMillis();
            CurrentGamePlaylistName = "";

            // データ作成
            var insertPlaylistHeader = FormConvertToPlaylistHeadder();
            DAO_GamePlaylist.InsertUpdate(insertPlaylistHeader);

            await ReloadGameAllPlaylist();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ImageItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                item.Status =
                    item.Status == PlaylistItemForm.GameStatus.Completed
                        ? PlaylistItemForm.GameStatus.None
                        : PlaylistItemForm.GameStatus.Completed;
            }

            UpdatePlaylistData();

            DAO_GamePlaylist.UpdateLastUsed(CurrentGamePlaylistId);

            await ReloadGameAllPlaylist();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                playlistItemFormList.Remove(item);
            }

            UpdatePlaylistData();

            await ReloadGameAllPlaylist();
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
                item.Status =
                    item.Status == PlaylistItemForm.GameStatus.Playing
                        ? PlaylistItemForm.GameStatus.None
                        : PlaylistItemForm.GameStatus.Playing;

                if(item.Status == GameStatus.Playing)
                {
                    var categoryData = await TwitchHelper.GetCategoryByGameId(item.CategoryId);

                    mainWindow.editTitleTextForm.SetCategory(categoryData.Id, categoryData.Name, categoryData.BoxArtUrl);
                    mainWindow.SetDisplayFromEditFrom();
                }
            }

            UpdatePlaylistData();

            await ReloadGameAllPlaylist();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GamePlaylistDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is PlaylistHeaderForm item)
            {
                DAO_GamePlaylist.DeleteGamePlayList(item.GamePlayListId);
            }

            await ReloadGameAllPlaylist();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GamePlayListTitleEdit_Click(object sender, EventArgs e)
        {
            var currentHeader = FormConvertToPlaylistHeadder();
            DAO_GamePlaylist.InsertUpdate(currentHeader);

            await ReloadGamePlaylistHeader();
        }

        #endregion


        #region ==================== DB関連メソッド ====================


        /// <summary>
        /// 画面情報をDBに保存
        /// </summary>
        public void UpdatePlaylistData()
        {
            // データ作成
            var insertPlaylistHeader = FormConvertToPlaylistHeadder();
            var insertPlaylistItemList = FormConvertToPlalistItemList(insertPlaylistHeader.GamePlayListId);

            // 挿入処理
            DAO_GamePlaylist.InsertUpdate(insertPlaylistHeader, insertPlaylistItemList);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private T_GamePlaylistHeader FormConvertToPlaylistHeadder()
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
                ThumbnailCategoryUrl = playlistItemFormList.Count == 0 ? "" : playlistItemFormList[0].CategoryId,
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


        /// <summary>
        /// プレイリスト一覧画面の再読み込み
        /// </summary>
        /// <returns></returns>
        public async Task ReloadGameAllPlaylist()
        {
            //　リストの初期化
            playlistItemFormList.Clear();
            playlistHeaderFormList.Clear();

            // プレイリストヘッダ一覧の読込
            var gamePlayListHeaders = DAO_GamePlaylist.SelectAllHeader();
            if (gamePlayListHeaders.Count == 0) return;

            foreach (var gamePlayListHeader in gamePlayListHeaders)
            {
                playlistHeaderFormList.Add(new PlaylistHeaderForm()
                {
                    GamePlayListId = gamePlayListHeader.GamePlayListId,
                    GamePlayListName = gamePlayListHeader.GamePlayListName,
                    ImageUrl = gamePlayListHeader.ThumbnailCategoryUrl,
                    LastUsedDate = gamePlayListHeader.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm"),
                    IsLoaded = false
                });
            }

            // 画面に何も設定されていないかの確認
            if (CurrentGamePlaylistId == 0)
            {
                // 未設定なら一番目のプレイリストを読込
                CurrentGamePlaylistId = gamePlayListHeaders[0].GamePlayListId;
            }

            var gamePlaylist = DAO_GamePlaylist.SelectHeaderById(CurrentGamePlaylistId);

            // 画面に何も設定されていないかの確認
            if (gamePlaylist == null)
            {
                // 未設定なら一番目のプレイリストを読込
                CurrentGamePlaylistId = gamePlayListHeaders[0].GamePlayListId;
                gamePlaylist = DAO_GamePlaylist.SelectHeaderById(CurrentGamePlaylistId);
            }

            CurrentGamePlaylistName = gamePlaylist.GamePlayListName;


            // 画面に設定されている
            var gamePlayListItems = DAO_GamePlaylist.SelectGamePlaylistById(CurrentGamePlaylistId);

            // 
            foreach (var game in gamePlayListItems)
            {
                string imageUrl = "";
                var categoryData = DAO_Category.SelectOneById(game.CategoryId);
                if (categoryData == null)
                {
                    var twitchCategoryData = await TwitchHelper.GetCategoryByGameId(game.CategoryId);
                    imageUrl = twitchCategoryData.BoxArtUrl;
                }
                else
                {
                    imageUrl = categoryData.SteamHeaderArtUrl != "" ? categoryData.SteamHeaderArtUrl : categoryData.BoxArtUrl;
                }

                playlistItemFormList.Add(new PlaylistItemForm()
                {
                    CategoryId = game.CategoryId,
                    ImageUrl = imageUrl,
                    Status = (GameStatus)game.Status
                });
            }
        }


        /// <summary>
        /// プレイリスト一覧画面の再読み込み
        /// </summary>
        /// <returns></returns>
        public async Task ReloadGamePlaylistHeader()
        {
            //　リストの初期化
            playlistHeaderFormList.Clear();

            // プレイリストヘッダ一覧の読込
            var gamePlayListHeaders = DAO_GamePlaylist.SelectAllHeader();
            if (gamePlayListHeaders.Count == 0) return;

            foreach (var gamePlayListHeader in gamePlayListHeaders)
            {
                playlistHeaderFormList.Add(new PlaylistHeaderForm()
                {
                    GamePlayListId = gamePlayListHeader.GamePlayListId,
                    GamePlayListName = gamePlayListHeader.GamePlayListName,
                    ImageUrl = gamePlayListHeader.ThumbnailCategoryUrl,
                    LastUsedDate = gamePlayListHeader.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm"),
                    IsLoaded = false
                });
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="urlText"></param>
        /// <returns></returns>
        public async Task AddSteamImageAsync(string categoryId, string urlText)
        {
            string url = urlText.Trim();

            var categoryData = await TwitchHelper.GetCategoryByGameId(categoryId);

            playlistItemFormList.Add(new PlaylistItemForm
            {
                CategoryId = categoryId,
                ImageUrl = string.IsNullOrEmpty(url) ? url : categoryData.BoxArtUrl,
            });

            UpdatePlaylistData();

            await ReloadGameAllPlaylist();
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

                            div.appendChild(img);
                            div.appendChild(completeText);
                            div.appendChild(playingText);

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
