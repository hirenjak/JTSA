using JTSA.Models;

namespace JTSA.Dao
{
    /// <summary>
    /// チャンネルポイント報酬キャッシュ（M_ChannelPoint）のCRUD
    /// </summary>
    class DAO_ChannelPoint
    {
        #region ==================== SELECT ====================

        /// <summary>
        /// SELECT * FROM M_ChannelPoint ORDER BY Cost ASC
        /// </summary>
        /// <returns>検索結果</returns>
        public static List<M_ChannelPoint> SelectAll()
        {
            using var db = new AppDbContext();

            return db.M_ChannelPoint.OrderBy(x => x.Cost).ToList();
        }


        /// <summary>
        /// プライマリキーによる単一検索
        /// </summary>
        /// <param name="rewardId">報酬ID</param>
        /// <returns>検索結果</returns>
        public static M_ChannelPoint? SelectOneById(string rewardId)
        {
            using var db = new AppDbContext();

            return db.M_ChannelPoint.SingleOrDefault(x => x.RewardId == rewardId);
        }

        #endregion


        #region ==================== INSERT/UPDATE ====================

        /// <summary>
        /// 報酬キャッシュを一覧の内容へ同期する。
        /// APIから消えた報酬（Web画面で削除された等）はキャッシュからも消す。
        /// </summary>
        /// <param name="targetDataList">同期後の全報酬</param>
        public static void ReplaceAll(List<M_ChannelPoint> targetDataList)
        {
            using var db = new AppDbContext();

            var currentRecords = db.M_ChannelPoint.ToList();
            var targetIds = targetDataList.Select(x => x.RewardId).ToHashSet();

            // APIに存在しなくなった報酬を削除
            foreach (var currentRecord in currentRecords.Where(x => !targetIds.Contains(x.RewardId)))
            {
                db.M_ChannelPoint.Remove(currentRecord);
            }

            foreach (var targetData in targetDataList)
            {
                var currentRecord = currentRecords.FirstOrDefault(x => x.RewardId == targetData.RewardId);

                if (currentRecord == null)
                {
                    db.M_ChannelPoint.Add(targetData);
                }
                else
                {
                    currentRecord.Title = targetData.Title;
                    currentRecord.Cost = targetData.Cost;
                    currentRecord.ImageUrl = targetData.ImageUrl;
                    currentRecord.IsManageable = targetData.IsManageable;
                    currentRecord.UpdatedDateTime = targetData.UpdatedDateTime;
                    currentRecord.LastUsedDateTime = targetData.LastUsedDateTime;
                }
            }

            // コミット処理
            db.SaveChanges();
        }

        #endregion
    }
}
