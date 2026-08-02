using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using TwitchLib.Api.Helix.Models.Schedule;
using static JTSA.Forms.PlaylistItemForm;

namespace JTSA.Panels
{
    /// <summary>
    /// SchedulePanel.xaml の相互作用ロジック
    /// </summary>
    public partial class PlayingGamePanel : UserControl
    {

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

        private string? exportFilePath;

        private ObsHttpServer? server;

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

        public async Task ReloadGamePlaylist()
        {
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

        private async void GamePlaylistDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is PlaylistHeaderForm item)
            {
                DAO_GamePlaylist.DeleteGamePlayList(item.GamePlayListId);
            }

            await ReloadGamePlaylist();
        }

        public static string? GetSteamAppId(string url)
        {
            var match = Regex.Match(url, @"store\.steampowered\.com/app/(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async void ImageItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                item.Status =
                    item.Status == PlaylistItemForm.GameStatus.Playing
                        ? PlaylistItemForm.GameStatus.None
                        : PlaylistItemForm.GameStatus.Playing;
            }

            UpdatePlaylistData();

            await ReloadGamePlaylist();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                playlistItemFormList.Remove(item);
            }

            UpdatePlaylistData();

            await ReloadGamePlaylist();
        }

        /// <summary>
        /// OBS書き出し用ボタン押下時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                FileName = "obs_steam_list.html",
                Filter = "HTMLファイル (*.html)|*.html"
            };

            if (dialog.ShowDialog() != true) return;

            exportFilePath = dialog.FileName;

            MessageBox.Show("OBS用HTMLを書き出しました。\n以降は追加・削除・完了切替時に自動更新します。");
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

            await ReloadGamePlaylist();
        }


        private T_GamePlaylistHeader FormConvertToPlaylistHeadder()
        {
            T_GamePlaylistHeader result = null;

            var playlistTitleText = CurrentGamePlaylistName;

            if (string.IsNullOrEmpty(playlistTitleText)) {
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

            await ReloadGamePlaylist();
        }

        private string CreateObsJson()
        {
            return JsonSerializer.Serialize(
                playlistItemFormList.Select(x => new
                {
                    imageUrl = x.ImageUrl,
                    status = x.Status.ToString()
                }));
        }

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

#imageList {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
    padding: 0;
}

.imageItem {
    position: relative;
    width: 230px;
    height: 107px;
    overflow: hidden;
    flex-shrink: 0;
}

.imageItem img {
    width: 100%;
    height: 100%;
    object-fit: contain;
    display: block;
}

/* 完了：画像全体を暗くして中央表示 */
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

/* プレイ中：左下に表示 */
.playingText {
    position: absolute;
    left: 8px;
    bottom: 8px;

    display: none;

    padding: 4px 10px;
    border-radius: 5px;

    color: white;
    background: #2196f3;

    font-size: 14px;
    font-weight: bold;
}

.imageItem.playing .playingText {
    display: block;
}
</style>
</head>

<body>

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

        const items = await response.json();
        const list = document.getElementById("imageList");

        list.innerHTML = "";

        for (const item of items) {
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

            const completeText = document.createElement("div");
            completeText.className = "completeText";
            completeText.textContent = "完了";

            const playingText = document.createElement("div");
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

            await ReloadGamePlaylist();
        }

        public void UpdatePlaylistData()
        {
            // データ作成
            var insertPlaylistHeader = FormConvertToPlaylistHeadder();
            var insertPlaylistItemList = FormConvertToPlalistItemList(insertPlaylistHeader.GamePlayListId);

            // 挿入処理
            DAO_GamePlaylist.InsertUpdate(insertPlaylistHeader, insertPlaylistItemList);
        }

        private async void GamePlaylistListBox_DoubleClick(object sender, EventArgs e)
        {
            if (GamePlaylistListBox.SelectedItem is PlaylistHeaderForm selectedItem)
            {
                // データ登録
                CurrentGamePlaylistId = selectedItem.GamePlayListId;
            }

            // 再読み込み処理
            await ReloadGamePlaylist();
        } 
    }
}
