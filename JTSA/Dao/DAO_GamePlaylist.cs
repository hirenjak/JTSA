using JTSA.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Dao
{
    public class DAO_GamePlaylist
    {
        #region ==================== SELECT ====================

        /// <summary>
        /// プレイリストヘッダの一覧取得
        /// SELECT * FROM M_Category ORDER BY LastUseDateTime DESC
        /// </summary>
        /// <returns>検索結果</returns>
        public static List<T_GamePlaylistHeader> SelectAllHeader()
        {
            List<T_GamePlaylistHeader> results = [];

            using (var db = new AppDbContext())
            {
                // SQL実行
                var exeResult = db.T_GamePlaylistHeader.OrderByDescending(x => x.LastUsedDateTime);

                results = exeResult.ToList();
            }

            return results;
        }


        /// <summary>
        /// プレイリストヘッダの一覧取得
        /// SELECT * FROM M_Category ORDER BY LastUseDateTime DESC
        /// </summary>
        /// <returns>検索結果</returns>
        public static T_GamePlaylistHeader SelectHeaderById(long gamePlaylistId)
        {
            T_GamePlaylistHeader result;

            using (var db = new AppDbContext())
            {
                // SQL実行
                result = db.T_GamePlaylistHeader.FirstOrDefault(x => x.GamePlayListId == gamePlaylistId);
            }

            return result;
        }


        /// <summary>
        /// ゲームプレイリストヘッダに紐づくカテゴリを取得
        /// SELECT * FROM T_GamePlayListLink ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<T_GamePlaylistItem> SelectGamePlaylistById(long gamePlaylistId)
        {
            List<T_GamePlaylistItem> results = [];

            using (var db = new AppDbContext())
            {
                // SQL実行
                var exeResult = db.T_GamePlaylistItem.Where(x => x.GamePlayListId == gamePlaylistId);

                results = exeResult.ToList();
            }
            return results;
        }

        #endregion


        #region ==================== INSERT/UPDATE ====================

        /// <summary>
        /// プレイリストの挿入更新処理
        /// 引数で渡すデータは全てデータがある前提で行う
        /// </summary>
        /// <param name="db"></param>
        /// <param name="targetHeaderData"></param>
        /// <returns>登録したT_GamePlayListHeader </returns>
        public static bool InsertUpdate(T_GamePlaylistHeader targetHeaderData)
        {
            using (var db = new AppDbContext())
            {
                // ヘッダの既存データの読込処理
                var selectHeaderExeResult = db.T_GamePlaylistHeader.SingleOrDefault(x => x.GamePlayListId == targetHeaderData.GamePlayListId);

                // ヘッダが無い場合はアイテムもない想定なので新規追加処理として行う
                List<T_GamePlaylistItem> selectItemExeResult = [];
                if (selectHeaderExeResult != null)
                {
                    selectItemExeResult = db.T_GamePlaylistItem.Where(x => x.GamePlayListId == selectHeaderExeResult.GamePlayListId).ToList();
                }

                // プレイリストヘッダの追加更新処理
                if (selectHeaderExeResult == null)
                {
                    // SQL実行
                    db.T_GamePlaylistHeader.Add(targetHeaderData);
                }
                else
                {
                    // SQL実行
                    selectHeaderExeResult.GamePlayListName = targetHeaderData.GamePlayListName;
                    selectHeaderExeResult.UpdatedDateTime = targetHeaderData.UpdatedDateTime;
                }

                // コミット処理
                db.SaveChanges();
            }

            return true;
        }

        /// <summary>
        /// プレイリストの挿入更新処理
        /// 引数で渡すデータは全てデータがある前提で行う
        /// </summary>
        /// <param name="db"></param>
        /// <param name="targetHeaderData"></param>
        /// <returns>登録したT_GamePlayListHeader </returns>
        public static bool InsertUpdate(T_GamePlaylistHeader targetHeaderData, List<T_GamePlaylistItem> targetItemData)
        {
            using (var db = new AppDbContext())
            {
                // ヘッダの既存データの読込処理
                var selectHeaderExeResult = db.T_GamePlaylistHeader.SingleOrDefault(x => x.GamePlayListId == targetHeaderData.GamePlayListId);

                // ヘッダが無い場合はアイテムもない想定なので新規追加処理として行う
                List<T_GamePlaylistItem> selectItemExeResult = [];
                if (selectHeaderExeResult != null)
                {
                    selectItemExeResult = db.T_GamePlaylistItem.Where(x => x.GamePlayListId == selectHeaderExeResult.GamePlayListId).ToList();
                }

                // プレイリストヘッダの追加更新処理
                if (selectHeaderExeResult == null)
                {
                    // SQL実行
                    db.T_GamePlaylistHeader.Add(targetHeaderData);
                }
                else
                {
                    // SQL実行
                    selectHeaderExeResult.GamePlayListName = targetHeaderData.GamePlayListName;
                    selectHeaderExeResult.UpdatedDateTime = targetHeaderData.UpdatedDateTime;
                }

                // プレイリストアイテムの追加更新処理
                if (selectItemExeResult.Count == 0)
                {
                    // SQL実行
                    db.T_GamePlaylistItem.AddRange(targetItemData);
                }
                else
                {
                    var entityPlayListItem = db.T_GamePlaylistItem.Where(x => x.GamePlayListId == selectHeaderExeResult.GamePlayListId).ToList();

                    if (entityPlayListItem != null)
                    {
                        // プレイリストアイテムの削除処理
                        foreach (var item in entityPlayListItem)
                        {
                            db.T_GamePlaylistItem.Remove(item);
                        }
                    }

                    // SQL実行
                    foreach (var targetItem in targetItemData)
                    {
                        db.T_GamePlaylistItem.Add(targetItem);
                    }
                }

                // コミット処理
                db.SaveChanges();
            }

            return true;
        }


        /// <summary>
        /// プレイリストアイテムの挿入更新処理
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns>true：登録成功 false：既にデータがある</returns>
        public static List<T_GamePlaylistItem> InsertUpdateList(List<T_GamePlaylistItem> targetDataList)
        {
            List<T_GamePlaylistItem> results = [];

            using (var db = new AppDbContext())
            {
                foreach (var targetData in targetDataList)
                {
                    var selectExeResult = db.T_GamePlaylistItem.SingleOrDefault(x =>
                                                x.GamePlayListId == targetData.GamePlayListId
                                             && x.CategoryId == targetData.CategoryId);

                    if (selectExeResult is null)
                    {
                        // SQL実行
                        var exeResult = db.T_GamePlaylistItem.Add(targetData);
                        results.Add(exeResult.Entity);
                    }
                    else
                    {
                        var exeResult = db.T_GamePlaylistItem.Update(targetData);
                        results.Add(exeResult.Entity);
                    }
                }

                // コミット処理
                db.SaveChanges();
            }

            return results;
        }


        #endregion


        #region ==================== DELETE ====================

        /// <summary>
        /// プレイリストの削除処理（アイテムも同時に削除）
        /// </summary>
        /// <param name="id"></param>
        public static bool DeleteGamePlayList(long gamePlaylistId)
        {
            using (var db = new AppDbContext())
            {
                // 削除対象の検索処理
                var entityPlaylist = db.T_GamePlaylistHeader.FirstOrDefault(x => x.GamePlayListId == gamePlaylistId);
                var entityPlayListItem = db.T_GamePlaylistItem.Where(x => x.GamePlayListId == gamePlaylistId).ToList();

                if (entityPlaylist != null)
                {
                    // プレイリストの削除処理
                    db.T_GamePlaylistHeader.Remove(entityPlaylist);
                }
                else
                {
                    // 対象が見つからない場合は失敗扱い
                    return false;
                }

                if (entityPlayListItem != null)
                {
                    // プレイリストアイテムの削除処理
                    foreach (var item in entityPlayListItem)
                    {
                        db.T_GamePlaylistItem.Remove(item);
                    }
                }
                else
                {
                    // 対象が見つからない場合は失敗扱い
                    return false;
                }

                // コミット処理
                db.SaveChanges();
            }

            return true;
        }


        /// <summary>
        /// プレイリストアイテムの削除処理
        /// </summary>
        /// <param name="id"></param>
        public static bool DeleteItem(int gamePlaylistId, string CategoryId)
        {
            using (var db = new AppDbContext())
            {
                // 削除対象の検索処理
                var entity = db.T_GamePlaylistItem.FirstOrDefault(x =>
                                       x.GamePlayListId == gamePlaylistId
                                    && x.CategoryId == CategoryId);

                if (entity != null)
                {
                    // プレイリストアイテムの削除処理
                    db.T_GamePlaylistItem.Remove(entity);
                }
                else
                {
                    // 対象が見つからない場合は失敗扱い
                    return false;
                }

                // コミット処理
                db.SaveChanges();
            }

            return true;
        }


        #endregion
    }
}
