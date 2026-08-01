using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using TwitchLib.Api.Helix.Models.Schedule;

namespace JTSA.Panels
{
    /// <summary>
    /// SchedulePanel.xaml の相互作用ロジック
    /// </summary>
    public partial class PlayingGamePanel : UserControl
    {
        private ObservableCollection<PlaylistItemForm> playlistItemFormList { get; } = new();

        /// <summary>  </summary>
        public ObservableCollection<PlaylistHeaderForm> playlistHeaderFormList { get; } = new();

        private static readonly HttpClient httpClient = new();

        private string? exportFilePath;

        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;


        public PlayingGamePanel()
        {
            InitializeComponent();
            ImageItemsControl.ItemsSource = playlistItemFormList;
            GamePlayListBox.ItemsSource = playlistHeaderFormList;

            ReloadGamePlaylist();
        }

        private void ReloadGamePlaylist()
        {
            playlistItemFormList.Clear();
            playlistHeaderFormList.Clear();

            // プレイリストヘッダ一覧の読込
            var gamePlayListHeaders = DAO_GamePlaylist.SelectAllHeader();
            if (gamePlayListHeaders.Count == 0) return;

            // 一番目のプレイリストを読込
            var gamePlayList = DAO_GamePlaylist.SelectGamePlaylistById(gamePlayListHeaders[0].GamePlayListId);

            foreach (var gamePlayListHeader in gamePlayListHeaders)
            {
                var categoryData = DAO_Category.SelectOneByCategoryId(gamePlayListHeader.ThumbnailCategoryUrl);
                playlistHeaderFormList.Add(new PlaylistHeaderForm()
                {
                    GamePlayListId = gamePlayListHeader.GamePlayListId,
                    GamePlayListName = gamePlayListHeader.GamePlayListName,
                    ImageUrl = categoryData.BoxArtUrl,
                    LastUsedDate = gamePlayListHeader.LastUsedDateTime.ToString("yyyy/MM/dd hh:mm"),
                    IsLoaded = false
                });
            }

            foreach (var game in gamePlayList)
            {
                var categoryData = DAO_Category.SelectOneByCategoryId(game.CategoryId);
                playlistItemFormList.Add(new PlaylistItemForm()
                {
                    CategoryId = game.CategoryId,
                    ImageUrl = categoryData.SteamHeaderArtUrl != "" ? categoryData.SteamHeaderArtUrl : categoryData.BoxArtUrl,
                    Status = PlaylistItemForm.GameStatus.None
                });
            }
        }

        private void GamePlaylistDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ボタンのDataContextから削除対象を取得
            if ((sender as Button)?.DataContext is PlaylistHeaderForm item)
            {
                DAO_GamePlaylist.DeleteGamePlayList(item.GamePlayListId);
            }

            ReloadGamePlaylist();
        }

        public static string? GetSteamAppId(string url)
        {
            var match = Regex.Match(url, @"store\.steampowered\.com/app/(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private void ImageItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                item.Status =
                    item.Status == PlaylistItemForm.GameStatus.Playing
                        ? PlaylistItemForm.GameStatus.None
                        : PlaylistItemForm.GameStatus.Playing;

                SaveObsHtml();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                playlistItemFormList.Remove(item);

                SaveObsHtml();
            }
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
            SaveObsHtml();

            MessageBox.Show("OBS用HTMLを書き出しました。\n以降は追加・削除・完了切替時に自動更新します。");
        }

        
        /// <summary>
        /// ゲームプレイリスト新規保存ボタン押下時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePlayListUpdateButton_Click(object sender, RoutedEventArgs e)
        {

        }


        /// <summary>
        /// ゲームプレイリスト新規保存ボタン押下時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePlayListSaveButton_Click(object sender, RoutedEventArgs e)
        {
            //ログ出力
            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "ゲームプレイリスト", "追加処理開始");

            // DB接続処理
            using var db = new AppDbContext();

            // データチェック
            if (playlistItemFormList.Count == 0) return;

            // データ作成

            // 挿入処理
            var insertPlaylistHeader = FormConvertToPlaylistHeadder();
            var insertPlaylistItemList = FormConvertToPlalistItemList();

            DAO_GamePlaylist.InsertUpdate(insertPlaylistHeader, insertPlaylistItemList);

            mainWindow.AppLogPanel.AddProcessLog(GetType().Name, "ゲームプレイリスト", "追加処理終了");

            ReloadGamePlaylist();
        }

        private T_GamePlayListHeader FormConvertToPlaylistHeadder()
        {
            T_GamePlayListHeader result = null;

            result = new T_GamePlayListHeader
            {
                GamePlayListName = GamePlayListTitleEdit.Text,
                ThumbnailCategoryUrl = playlistItemFormList[0].CategoryId,
                SelectedCount = 0,
                SortNumber = 9999,
                IsDeleted = false,
                LastUsedDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            };

            return result;
        }

        private List<T_GamePlayListItem> FormConvertToPlalistItemList()
        {
            List<T_GamePlayListItem> resultList = [];
            foreach (var item in playlistItemFormList)
            {
                resultList.Add(
                    new T_GamePlayListItem
                    {
                        GamePlayListId = item.PlaylistId,
                        CategoryId = item.CategoryId,
                        LastUsedDateTime = DateTime.Now,
                        CreatedDateTime = DateTime.Now,
                        UpdatedDateTime = DateTime.Now
                    });
            }

            return resultList;
        }

        private void SaveObsHtml()
        {
            if (string.IsNullOrEmpty(exportFilePath)) return;

            File.WriteAllText(exportFilePath, CreateObsHtml(), Encoding.UTF8);

        }

        private void ImageItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PlaylistItemForm item)
            {
                item.Status =
                    item.Status == PlaylistItemForm.GameStatus.Completed
                        ? PlaylistItemForm.GameStatus.None
                        : PlaylistItemForm.GameStatus.Completed;

                SaveObsHtml();
            }
        }

        private string CreateObsHtml()
        {
            var itemsHtml = string.Join(Environment.NewLine, playlistItemFormList.Select(item =>
                $"""
                <div class="imageItem{item.StatusClass}">
                    <img src="{item.ImageUrl}">
                    <div class="completeText">完了</div>
                    <div class="playingText">プレイ中</div>
                </div>
                """));

            return $$"""
        <!DOCTYPE html>
        <html lang="ja">
        <head>
        <meta charset="UTF-8">
        <meta http-equiv="refresh" content="1">
        <title>OBS表示用</title>
        <style>
        body{
            margin:20px;
            background:transparent;
            overflow:hidden;
        }
        #imageList{
            display:flex;
            flex-wrap:wrap;
            gap:16px;
        }
        .imageItem{
            position:relative;
            width:230px;
            height:107px;
            overflow:hidden;
        }
        .imageItem img{
            width:100%;
            height:100%;
            object-fit:cover;
            display:block;
        }
        .completeText{
            position:absolute;
            inset:0;
            display:none;
            justify-content:center;
            align-items:center;
            font-size:30px;
            font-weight:bold;
            color:#00ff80;
            background:rgba(0,0,0,.55);
        }
        .imageItem.completed .completeText{
            display:flex;
        }
        .playingText{
            position:absolute;
            left:8px;
            bottom:8px;
            display:none;
            padding:4px 10px;
            border-radius:5px;
            background:#2196F3;
            color:white;
            font-size:14px;
            font-weight:bold;
        }

        .imageItem.playing .playingText{
            display:block;
        }
        </style>
        </head>
        <body>
        <div id="imageList">
        {{itemsHtml}}
        </div>
        </body>
        </html>
        """;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="urlText"></param>
        /// <returns></returns>
        public void AddSteamImageAsync(string categoryId, string urlText)
        {
            var url = urlText.Trim();

            if (string.IsNullOrEmpty(urlText))
            {
                MessageBox.Show("SteamストアURLを入力してください。");
                return;
            }

            playlistItemFormList.Add(new PlaylistItemForm
            {
                CategoryId = categoryId,
                ImageUrl = urlText
            });

            SaveObsHtml();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="appId"></param>
        /// <returns></returns>
        public static async Task<string?> GetSteamHeaderImageUrlAsync(string appId)
        {
            var apiUrl =
                $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=JP&l=japanese";

            var json = await httpClient.GetStringAsync(apiUrl);

            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement.GetProperty(appId);

            if (!root.GetProperty("success").GetBoolean())
                return null;

            var data = root.GetProperty("data");

            if (data.TryGetProperty("header_image", out var headerImage))
                return headerImage.GetString();

            return null;
        }
    }
}
