using JTSA.Models;

namespace JTSA.Dao
{
    /// <summary>
    /// チャンネルポイントプリセット（ヘッダ＋アイテム）のCRUD
    /// </summary>
    class DAO_ChannelPointPreset
    {
        #region ==================== SELECT ====================

        /// <summary>
        /// プリセットヘッダの一覧取得
        /// SELECT * FROM T_ChannelPointPresetHeader ORDER BY LastUsedDateTime DESC
        /// </summary>
        /// <returns>検索結果</returns>
        public static List<T_ChannelPointPresetHeader> SelectAllHeader()
        {
            using var db = new AppDbContext();

            return db.T_ChannelPointPresetHeader
                     .OrderByDescending(x => x.LastUsedDateTime)
                     .ToList();
        }


        /// <summary>
        /// プリセットヘッダの単一取得
        /// </summary>
        /// <param name="presetId">プリセットID</param>
        /// <returns>検索結果</returns>
        public static T_ChannelPointPresetHeader? SelectHeaderById(long presetId)
        {
            using var db = new AppDbContext();

            return db.T_ChannelPointPresetHeader.SingleOrDefault(x => x.PresetId == presetId);
        }


        /// <summary>
        /// プリセットに紐づくアイテムの取得
        /// </summary>
        /// <param name="presetId">プリセットID</param>
        /// <returns>検索結果</returns>
        public static List<T_ChannelPointPresetItem> SelectItemsByPresetId(long presetId)
        {
            using var db = new AppDbContext();

            return db.T_ChannelPointPresetItem
                     .Where(x => x.PresetId == presetId)
                     .ToList();
        }


        /// <summary>
        /// プリセットごとのアイテム件数を取得する（一覧表示用）
        /// </summary>
        /// <returns>プリセットID → 件数</returns>
        public static Dictionary<long, int> SelectItemCounts()
        {
            using var db = new AppDbContext();

            return db.T_ChannelPointPresetItem
                     .GroupBy(x => x.PresetId)
                     .ToDictionary(x => x.Key, x => x.Count());
        }

        #endregion


        #region ==================== INSERT/UPDATE ====================

        /// <summary>
        /// プリセットの保存（新規／上書き）。
        ///
        /// 全件スナップショット方式のため、アイテムは差分更新せず
        /// 「既存を全削除 → 渡された内容を全追加」で置き換える。
        /// （DAO_GamePlaylist.InsertUpdate は既存アイテムの削除が漏れており重複するため真似しない）
        /// </summary>
        /// <param name="targetHeaderData">保存するヘッダ</param>
        /// <param name="targetItemDataList">保存するアイテム</param>
        /// <returns>true：成功</returns>
        public static bool InsertUpdate(
            T_ChannelPointPresetHeader targetHeaderData,
            List<T_ChannelPointPresetItem> targetItemDataList)
        {
            using var db = new AppDbContext();

            // ヘッダの追加更新処理
            var selectHeaderExeResult = db.T_ChannelPointPresetHeader
                                          .SingleOrDefault(x => x.PresetId == targetHeaderData.PresetId);

            if (selectHeaderExeResult == null)
            {
                db.T_ChannelPointPresetHeader.Add(targetHeaderData);
            }
            else
            {
                selectHeaderExeResult.PresetName = targetHeaderData.PresetName;
                selectHeaderExeResult.UpdatedDateTime = targetHeaderData.UpdatedDateTime;
                selectHeaderExeResult.LastUsedDateTime = targetHeaderData.LastUsedDateTime;
            }

            // アイテムは全置換する
            var currentItems = db.T_ChannelPointPresetItem
                                 .Where(x => x.PresetId == targetHeaderData.PresetId)
                                 .ToList();

            db.T_ChannelPointPresetItem.RemoveRange(currentItems);
            db.T_ChannelPointPresetItem.AddRange(targetItemDataList);

            // コミット処理
            db.SaveChanges();

            return true;
        }


        /// <summary>
        /// プリセット名の変更
        /// </summary>
        /// <param name="presetId">プリセットID</param>
        /// <param name="presetName">新しいプリセット名</param>
        /// <returns>true：成功</returns>
        public static bool UpdateName(long presetId, string presetName)
        {
            using var db = new AppDbContext();

            var selectExeResult = db.T_ChannelPointPresetHeader.SingleOrDefault(x => x.PresetId == presetId);
            if (selectExeResult == null) return false;

            selectExeResult.PresetName = presetName;
            selectExeResult.UpdatedDateTime = DateTime.Now;

            // コミット処理
            db.SaveChanges();

            return true;
        }


        /// <summary>
        /// Update：最終使用（適用したタイミングで呼ぶ）
        /// </summary>
        /// <param name="presetId">プリセットID</param>
        /// <returns>true：成功</returns>
        public static bool UpdateLastUsed(long presetId)
        {
            using var db = new AppDbContext();

            var selectExeResult = db.T_ChannelPointPresetHeader.SingleOrDefault(x => x.PresetId == presetId);
            if (selectExeResult == null) return false;

            selectExeResult.LastUsedDateTime = DateTime.Now;
            selectExeResult.SelectedCount++;

            // コミット処理
            db.SaveChanges();

            return true;
        }

        #endregion


        #region ==================== DELETE ====================

        /// <summary>
        /// プリセットの削除（アイテムと、カテゴリからの紐づけも同時に削除）
        /// </summary>
        /// <param name="presetId">プリセットID</param>
        /// <returns>true：成功</returns>
        public static bool Delete(long presetId)
        {
            using var db = new AppDbContext();

            var entityHeader = db.T_ChannelPointPresetHeader.FirstOrDefault(x => x.PresetId == presetId);
            if (entityHeader == null) return false;

            var entityItems = db.T_ChannelPointPresetItem.Where(x => x.PresetId == presetId).ToList();

            db.T_ChannelPointPresetItem.RemoveRange(entityItems);
            db.T_ChannelPointPresetHeader.Remove(entityHeader);

            // 存在しないプリセットを指したままにすると、カテゴリ変更のたびに適用エラーになるため外す
            foreach (var linkedCategory in db.M_Category.Where(x => x.ChannelPointPresetId == presetId))
            {
                linkedCategory.ChannelPointPresetId = null;
                linkedCategory.UpdatedDateTime = DateTime.Now;
            }

            // コミット処理
            db.SaveChanges();

            return true;
        }

        #endregion
    }
}
