using JTSA.Forms;
using System.Windows;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace JTSA.Utility
{
    /// <summary>
    /// チャンネルポイント報酬まわりのビジネスロジック。
    /// TwitchHelper（API境界）と画面（ChannelPointPanel）の間に入り、
    /// 「操作可否の判定」「コピー」「プリセット適用」といったアプリ固有の処理を担う。
    /// </summary>
    static class ChannelPointService
    {
        /// <summary>
        /// チャンネルポイント報酬の一覧を取得する。
        ///
        /// Twitchの仕様上、Web画面や他アプリから作成された報酬はこのアプリからは更新／削除できない。
        /// これを判別する公式なフラグは存在しないため、
        /// 「全件（only_manageable_rewards=false）」と「自アプリ作成分のみ（=true）」の
        /// 2回のGETを取り、後者に含まれるものだけ IsManageable = true とする。
        /// </summary>
        /// <returns>取得できた報酬一覧（コスト昇順）。失敗した場合はnull。</returns>
        public static async Task<List<ChannelPointRewardForm>?> FetchRewardsAsync()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(ChannelPointService), "チャンネルポイント一覧取得");

            var allRewards = await TwitchHelper.GetCustomRewardsAsync(onlyManageableRewards: false);
            if (allRewards == null)
            {
                mainWindow.AppLogPanel.Error(nameof(ChannelPointService), "チャンネルポイント一覧取得失敗");
                mainWindow.AppLogPanel.ProcessEnd(nameof(ChannelPointService), appLogProcessName);
                return null;
            }

            // 自アプリ作成分のみの取得。ここが失敗した場合は「操作可否が不明」なので
            // 一覧自体は返しつつ、全件を操作不可扱いにして誤操作を防ぐ。
            var manageableRewards = await TwitchHelper.GetCustomRewardsAsync(onlyManageableRewards: true);
            if (manageableRewards == null)
            {
                mainWindow.AppLogPanel.Error(nameof(ChannelPointService), "操作可能な報酬の判定に失敗したため全件を操作不可として表示します");
            }

            var manageableIds = manageableRewards?.Select(x => x.Id).ToHashSet() ?? [];

            var results = allRewards
                .Select(reward => ToForm(reward, manageableIds.Contains(reward.Id)))
                .OrderBy(x => x.Cost)
                .ToList();

            mainWindow.AppLogPanel.Success(nameof(ChannelPointService),
                $"チャンネルポイント一覧取得（全{results.Count}件／操作可能{results.Count(x => x.IsManageable)}件）");
            mainWindow.AppLogPanel.ProcessEnd(nameof(ChannelPointService), appLogProcessName);

            return results;
        }


        /// <summary>
        /// 報酬の有効／無効を切り替える。成功した場合はFormの値も更新する。
        /// </summary>
        /// <param name="reward">対象の報酬</param>
        /// <param name="isEnabled">設定する値</param>
        /// <returns>true：成功</returns>
        public static async Task<bool> SetEnabledAsync(ChannelPointRewardForm reward, bool isEnabled)
        {
            return await UpdateAsync(reward, new UpdateCustomRewardRequest { IsEnabled = isEnabled });
        }


        /// <summary>
        /// 報酬の一時停止を切り替える。成功した場合はFormの値も更新する。
        /// </summary>
        /// <param name="reward">対象の報酬</param>
        /// <param name="isPaused">設定する値</param>
        /// <returns>true：成功</returns>
        public static async Task<bool> SetPausedAsync(ChannelPointRewardForm reward, bool isPaused)
        {
            return await UpdateAsync(reward, new UpdateCustomRewardRequest { IsPaused = isPaused });
        }


        /// <summary>
        /// 報酬を更新し、成功したらAPIが返した最新値をFormへ反映する。
        /// 一覧の全件再取得を行わずに画面と実状態を一致させるための共通処理。
        /// </summary>
        /// <param name="reward">対象の報酬</param>
        /// <param name="request">更新内容</param>
        /// <returns>true：成功</returns>
        private static async Task<bool> UpdateAsync(ChannelPointRewardForm reward, UpdateCustomRewardRequest request)
        {
            if (!reward.IsManageable) return false;

            var updated = await TwitchHelper.UpdateCustomRewardAsync(reward.RewardId, request);
            if (updated == null || updated.Count == 0) return false;

            ApplyToForm(reward, updated[0]);

            return true;
        }


        /// <summary>
        /// TwitchLibのCustomRewardを画面用DTOへ詰め替える
        /// </summary>
        /// <param name="reward">APIレスポンス</param>
        /// <param name="isManageable">このアプリから操作できるか</param>
        /// <returns>画面用DTO</returns>
        private static ChannelPointRewardForm ToForm(CustomReward reward, bool isManageable)
        {
            var form = new ChannelPointRewardForm
            {
                RewardId = reward.Id,
                IsManageable = isManageable
            };

            ApplyToForm(form, reward);

            return form;
        }


        /// <summary>
        /// APIレスポンスの内容を既存のFormへ上書きする（IsManageableとIsSelectedは維持する）
        /// </summary>
        /// <param name="form">上書き先</param>
        /// <param name="reward">APIレスポンス</param>
        private static void ApplyToForm(ChannelPointRewardForm form, CustomReward reward)
        {
            form.Title = reward.Title ?? "";
            form.Prompt = reward.Prompt ?? "";
            form.Cost = reward.Cost;
            form.ImageUrl = reward.Image?.Url1x ?? reward.DefaultImage?.Url1x ?? "";
            form.BackgroundColor = reward.BackgroundColor ?? "";
            form.IsUserInputRequired = reward.IsUserInputRequired;
            form.ShouldRedemptionsSkipQueue = reward.ShouldRedemptionsSkipQueue;

            // 上限・クールダウンは「有効フラグ＋値」の組で返るため、無効なら0として保持する
            form.MaxPerStream = reward.MaxPerStreamSetting?.IsEnabled == true
                ? reward.MaxPerStreamSetting.MaxPerStream : 0;
            form.MaxPerUserPerStream = reward.MaxPerUserPerStreamSetting?.IsEnabled == true
                ? reward.MaxPerUserPerStreamSetting.MaxPerUserPerStream : 0;
            form.GlobalCooldownSeconds = reward.GlobalCooldownSetting?.IsEnabled == true
                ? reward.GlobalCooldownSetting.GlobalCooldownSeconds : 0;

            // INPC通知が必要なプロパティは最後に設定する
            form.IsEnabled = reward.IsEnabled;
            form.IsPaused = reward.IsPaused;
        }
    }
}
