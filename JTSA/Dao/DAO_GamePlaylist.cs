using JTSA.Models;
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
        public static List<T_GamePlayListHeader> SelectAllHeader()
        {
            List<T_GamePlayListHeader> results = [];

            using (var db = new AppDbContext())
            {
                // SQL実行
                var exeResult = db.M_GamePlayList.OrderByDescending(x => x.LastUsedDateTime);

                results = exeResult.ToList();
            }

            return results;
        }


        /// <summary>
        /// ゲームプレイリストヘッダに紐づくカテゴリを取得
        /// SELECT * FROM T_GamePlayListLink ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<T_GamePlayListItem> SelectGamePlaylistById(int gamePlaylistId)
        {
            List<T_GamePlayListItem> results = [];

            using (var db = new AppDbContext())
            {
                // SQL実行
                var exeResult = db.T_GamePlayListItem.Where(x => x.GamePlayListId == gamePlaylistId);

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
        public static bool InsertUpdate(T_GamePlayListHeader targetHeaderData, List<T_GamePlayListItem> targetItemData)
        {
            using (var db = new AppDbContext())
            {
                // ヘッダの既存データの読込処理
                var selectHeaderExeResult = db.M_GamePlayList.SingleOrDefault(x =>
                                            x.GamePlayListId == targetHeaderData.GamePlayListId);

                // ヘッダが無い場合はアイテムもない想定なので新規追加処理として行う
                List<T_GamePlayListItem> selectItemExeResult = [];
                if (selectHeaderExeResult != null)
                {
                    selectItemExeResult = db.T_GamePlayListItem.Where(x => x.GamePlayListId == selectHeaderExeResult.GamePlayListId).ToList();
                }

                // プレイリストヘッダの追加更新処理
                if (selectHeaderExeResult == null)
                {
                    // SQL実行
                    db.M_GamePlayList.Add(targetHeaderData);
                }
                else
                {
                    // SQL実行
                    db.M_GamePlayList.Update(targetHeaderData);
                }

                // プレイリストアイテムの追加更新処理
                if(selectItemExeResult.Count == 0)
                {
                    // SQL実行
                    db.T_GamePlayListItem.AddRange(targetItemData);
                }
                else
                {
                    // SQL実行
                    foreach (var targetItem in targetItemData)
                    {
                        bool isUpdated = false;
                        foreach (var selectItem in selectItemExeResult)
                        {
                            if (selectItem.CategoryId == targetItem.CategoryId)
                            {
                                db.T_GamePlayListItem.Update(targetItem);
                                isUpdated = true;
                                break;
                            }
                        }
                        if (!isUpdated)
                        {
                            db.T_GamePlayListItem.Add(targetItem);
                        }
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
        public static List<T_GamePlayListItem> InsertUpdateList(List<T_GamePlayListItem> targetDataList)
        {
            List<T_GamePlayListItem> results = [];

            using (var db = new AppDbContext())
            {
                foreach (var targetData in targetDataList)
                {
                    var selectExeResult = db.T_GamePlayListItem.SingleOrDefault(x =>
                                                x.GamePlayListId == targetData.GamePlayListId
                                             && x.CategoryId == targetData.CategoryId);

                    if (selectExeResult is null)
                    {
                        // SQL実行
                        var exeResult = db.T_GamePlayListItem.Add(targetData);
                        results.Add(exeResult.Entity);
                    }
                    else
                    {
                        var exeResult = db.T_GamePlayListItem.Update(targetData);
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
        public static bool DeleteGamePlayList(int gamePlaylistId)
        {
            using (var db = new AppDbContext())
            {
                // 削除対象の検索処理
                var entityPlaylist = db.M_GamePlayList.FirstOrDefault(x => x.GamePlayListId == gamePlaylistId);
                var entityPlayListItem = db.T_GamePlayListItem.Where(x => x.GamePlayListId == gamePlaylistId).ToList();

                if (entityPlaylist != null)
                {
                    // プレイリストの削除処理
                    db.M_GamePlayList.Remove(entityPlaylist);
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
                        db.T_GamePlayListItem.Remove(item);
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
                var entity = db.T_GamePlayListItem.FirstOrDefault(x =>
                                       x.GamePlayListId == gamePlaylistId
                                    && x.CategoryId == CategoryId);

                if (entity != null)
                {
                    // プレイリストアイテムの削除処理
                    db.T_GamePlayListItem.Remove(entity);
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
