using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using System.Windows;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using static JTSA.Dao.DAO_Setting;

namespace JTSA.Utility
{
    /// <summary>
    /// 報酬コピーの結果（1件分）
    /// </summary>
    public class ChannelPointCopyResult
    {
        /// <summary> 成功したか </summary>
        public bool IsSuccess { get; set; }

        /// <summary> コピー元の報酬名 </summary>
        public string SourceTitle { get; set; } = "";

        /// <summary> 実際に作成された報酬名（接尾辞付き） </summary>
        public string CreatedTitle { get; set; } = "";

        /// <summary> 失敗理由 </summary>
        public string ErrorMessage { get; set; } = "";
    }


    /// <summary>
    /// 報酬一覧の取得結果
    /// </summary>
    public class ChannelPointFetchResult
    {
        /// <summary> 取得した報酬一覧（コスト昇順） </summary>
        public List<ChannelPointRewardForm> Rewards { get; set; } = [];

        /// <summary>
        /// 操作可否の判定に成功したか。
        /// falseの場合、IsManageableは全件falseになっているだけで実際の可否は不明。
        /// 「本当に全件が操作不可」なのか「判定できなかった」のかを画面で区別するために持つ。
        /// </summary>
        public bool IsManageableCheckSucceeded { get; set; }
    }


    /// <summary>
    /// プリセット適用の結果
    /// </summary>
    public class ChannelPointApplyResult
    {
        /// <summary> 適用処理を実行できたか（プリセット未存在や一覧取得失敗はfalse） </summary>
        public bool IsSuccess { get; set; }

        /// <summary> 適用したプリセット名 </summary>
        public string PresetName { get; set; } = "";

        /// <summary> 実際に変更した報酬の件数 </summary>
        public int ChangedCount { get; set; }

        /// <summary> 既に狙いの状態だったため変更不要だった件数 </summary>
        public int UnchangedCount { get; set; }

        /// <summary> 報酬が見つからない等でスキップした件数 </summary>
        public int SkippedCount { get; set; }

        /// <summary> 変更に失敗した件数 </summary>
        public int FailedCount { get; set; }

        /// <summary> 失敗理由（1件でも失敗した場合） </summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary> ログ・画面に出す要約 </summary>
        public string SummaryText =>
            $"「{PresetName}」を適用（変更{ChangedCount}件／変更不要{UnchangedCount}件"
            + (SkippedCount > 0 ? $"／スキップ{SkippedCount}件" : "")
            + (FailedCount > 0 ? $"／失敗{FailedCount}件" : "")
            + "）";
    }


    /// <summary>
    /// チャンネルポイント報酬まわりのビジネスロジック。
    /// TwitchHelper（API境界）と画面（ChannelPointPanel）の間に入り、
    /// 「操作可否の判定」「コピー」「プリセット適用」といったアプリ固有の処理を担う。
    /// </summary>
    static class ChannelPointService
    {
        /// <summary> Twitchの報酬名の最大文字数 </summary>
        private const int TITLE_MAX_LENGTH = 45;

        /// <summary> コピー時に付ける接尾辞の既定値 </summary>
        public const string DEFAULT_COPY_SUFFIX = "'";

        /// <summary> タイトル重複時に接尾辞を重ねて再試行する回数 </summary>
        private const int COPY_RETRY_COUNT = 3;


        #region ==================== 取得 ====================

        /// <summary>
        /// チャンネルポイント報酬の一覧を取得する。
        ///
        /// Twitchの仕様上、Web画面や他アプリから作成された報酬はこのアプリからは更新／削除できない。
        /// これを判別する公式なフラグは存在しないため、
        /// 「全件（only_manageable_rewards=false）」と「自アプリ作成分のみ（=true）」の
        /// 2回のGETを取り、後者に含まれるものだけ IsManageable = true とする。
        /// </summary>
        /// <returns>取得結果。一覧そのものの取得に失敗した場合はnull。</returns>
        public static async Task<ChannelPointFetchResult?> FetchRewardsAsync()
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

            var rewards = allRewards
                .Select(reward => ToForm(reward, manageableIds.Contains(reward.Id)))
                .OrderBy(x => x.Cost)
                .ToList();

            // プリセット編集画面がAPIを叩かずに報酬名を出せるようキャッシュしておく
            SaveRewardCache(rewards);

            mainWindow.AppLogPanel.Success(nameof(ChannelPointService),
                $"チャンネルポイント一覧取得（全{rewards.Count}件／操作可能{rewards.Count(x => x.IsManageable)}件）");
            mainWindow.AppLogPanel.ProcessEnd(nameof(ChannelPointService), appLogProcessName);

            return new ChannelPointFetchResult
            {
                Rewards = rewards,
                IsManageableCheckSucceeded = manageableRewards != null
            };
        }


        /// <summary>
        /// 取得した報酬一覧を M_ChannelPoint へ同期する
        /// </summary>
        /// <param name="rewards">報酬一覧</param>
        private static void SaveRewardCache(List<ChannelPointRewardForm> rewards)
        {
            var now = DateTime.Now;

            var cacheData = rewards.Select(reward => new M_ChannelPoint
            {
                RewardId = reward.RewardId,
                Title = reward.Title,
                Cost = reward.Cost,
                ImageUrl = reward.ImageUrl,
                IsManageable = reward.IsManageable,
                LastUsedDateTime = now,
                CreatedDateTime = now,
                UpdatedDateTime = now
            }).ToList();

            DAO_ChannelPoint.ReplaceAll(cacheData);
        }

        #endregion


        #region ==================== 更新 ====================

        /// <summary>
        /// 報酬の有効／無効を切り替える。成功した場合はFormの値も更新する。
        /// </summary>
        /// <param name="reward">対象の報酬</param>
        /// <param name="isEnabled">設定する値</param>
        /// <returns>成否と失敗理由</returns>
        public static async Task<TwitchApiResult<bool>> SetEnabledAsync(ChannelPointRewardForm reward, bool isEnabled)
        {
            return await UpdateAsync(reward, new UpdateCustomRewardRequest { IsEnabled = isEnabled });
        }


        /// <summary>
        /// 報酬の一時停止を切り替える。成功した場合はFormの値も更新する。
        /// </summary>
        /// <param name="reward">対象の報酬</param>
        /// <param name="isPaused">設定する値</param>
        /// <returns>成否と失敗理由</returns>
        public static async Task<TwitchApiResult<bool>> SetPausedAsync(ChannelPointRewardForm reward, bool isPaused)
        {
            return await UpdateAsync(reward, new UpdateCustomRewardRequest { IsPaused = isPaused });
        }


        /// <summary>
        /// 報酬を更新し、成功したらAPIが返した最新値をFormへ反映する。
        /// 一覧の全件再取得を行わずに画面と実状態を一致させるための共通処理。
        /// </summary>
        /// <param name="reward">対象の報酬</param>
        /// <param name="request">更新内容</param>
        /// <returns>成否と失敗理由</returns>
        private static async Task<TwitchApiResult<bool>> UpdateAsync(ChannelPointRewardForm reward, UpdateCustomRewardRequest request)
        {
            if (!reward.IsManageable)
            {
                return TwitchApiResult<bool>.Failure(TwitchApiErrorKind.NotManageable,
                    "Twitch の Web 画面から作成された報酬のため操作できません。");
            }

            var result = await TwitchHelper.UpdateCustomRewardAsync(reward.RewardId, request);

            if (!result.IsSuccess) return TwitchApiResult<bool>.Failure(result.ErrorKind, result.ErrorMessage);

            if (result.Data == null || result.Data.Count == 0)
            {
                return TwitchApiResult<bool>.Failure(TwitchApiErrorKind.Unknown, "レスポンスが空でした");
            }

            ApplyToForm(reward, result.Data[0]);

            return TwitchApiResult<bool>.Success(true);
        }

        #endregion


        #region ==================== コピー ====================

        /// <summary>
        /// コピー時にタイトルへ付ける接尾辞を取得する。
        /// Twitchの報酬名はチャンネル内で一意でなければならないため、
        /// 元と同名のコピーは作成できない。その回避策。
        /// </summary>
        /// <returns>設定値。未設定なら既定値</returns>
        public static string GetCopySuffix()
        {
            var setting = DAO_Setting.SelectOneById(SettingName.ChannelPointCopySuffix);

            return string.IsNullOrEmpty(setting?.Value) ? DEFAULT_COPY_SUFFIX : setting.Value;
        }


        /// <summary>
        /// 操作不可な報酬を、このアプリの管理下（＝操作可能）へコピーする。
        ///
        /// 画像だけはTwitch APIで設定できないため引き継げない。
        /// また元の報酬はこのアプリからは削除できないので、後始末はWeb画面でユーザーが行う。
        /// </summary>
        /// <param name="source">コピー元の報酬</param>
        /// <param name="suffix">タイトルへ付ける接尾辞</param>
        /// <returns>コピー結果</returns>
        public static async Task<ChannelPointCopyResult> CopyRewardAsync(ChannelPointRewardForm source, string suffix)
        {
            var lastErrorMessage = "";

            // タイトル重複だけは接尾辞を重ねて再試行する（例: 「報酬'」→「報酬''」）
            for (var attempt = 1; attempt <= COPY_RETRY_COUNT; attempt++)
            {
                var candidateSuffix = string.Concat(Enumerable.Repeat(suffix, attempt));
                var title = BuildCopyTitle(source.Title, candidateSuffix);

                var result = await TwitchHelper.CreateCustomRewardAsync(BuildCreateRequest(source, title));

                if (result.IsSuccess)
                {
                    return new ChannelPointCopyResult
                    {
                        IsSuccess = true,
                        SourceTitle = source.Title,
                        CreatedTitle = title
                    };
                }

                lastErrorMessage = result.ErrorMessage;

                // 重複以外の失敗は再試行しても同じなので即座に打ち切る
                if (result.ErrorKind != TwitchApiErrorKind.DuplicateTitle) break;
            }

            return new ChannelPointCopyResult
            {
                IsSuccess = false,
                SourceTitle = source.Title,
                ErrorMessage = lastErrorMessage
            };
        }


        /// <summary>
        /// コピー先のタイトルを組み立てる。
        /// Twitchの上限（45文字）を超える場合は元のタイトル側を切り詰める。
        /// </summary>
        /// <param name="sourceTitle">コピー元の報酬名</param>
        /// <param name="suffix">付与する接尾辞</param>
        /// <returns>コピー先の報酬名</returns>
        private static string BuildCopyTitle(string sourceTitle, string suffix)
        {
            var maxBaseLength = TITLE_MAX_LENGTH - suffix.Length;

            // 接尾辞だけで上限を超えるような設定値の場合は、上限まで切り詰めて返すしかない
            if (maxBaseLength <= 0) return suffix[..TITLE_MAX_LENGTH];

            var baseTitle = sourceTitle.Length > maxBaseLength
                ? sourceTitle[..maxBaseLength]
                : sourceTitle;

            return baseTitle + suffix;
        }


        /// <summary>
        /// コピー元の設定を作成リクエストへ写す（画像はAPIで設定できないため対象外）
        /// </summary>
        /// <param name="source">コピー元の報酬</param>
        /// <param name="title">コピー先の報酬名</param>
        /// <returns>作成リクエスト</returns>
        private static CreateCustomRewardsRequest BuildCreateRequest(ChannelPointRewardForm source, string title)
        {
            var request = new CreateCustomRewardsRequest
            {
                Title = title,
                Cost = source.Cost,
                Prompt = source.Prompt,
                IsEnabled = source.IsEnabled,
                IsUserInputRequired = source.IsUserInputRequired,
                ShouldRedemptionsSkipRequestQueue = source.ShouldRedemptionsSkipQueue,

                // 上限・クールダウンは「有効フラグ＋値」の組で送る。0は未設定とみなす
                IsMaxPerStreamEnabled = source.MaxPerStream > 0,
                MaxPerStream = source.MaxPerStream > 0 ? source.MaxPerStream : null,

                IsMaxPerUserPerStreamEnabled = source.MaxPerUserPerStream > 0,
                MaxPerUserPerStream = source.MaxPerUserPerStream > 0 ? source.MaxPerUserPerStream : null,

                IsGlobalCooldownEnabled = source.GlobalCooldownSeconds > 0,
                GlobalCooldownSeconds = source.GlobalCooldownSeconds > 0 ? source.GlobalCooldownSeconds : null,
            };

            // 背景色は未設定のまま送るとエラーになり得るため、値があるときだけ設定する
            if (!string.IsNullOrWhiteSpace(source.BackgroundColor))
            {
                request.BackgroundColor = source.BackgroundColor;
            }

            return request;
        }

        #endregion


        #region ==================== プリセット ====================

        /// <summary>
        /// 現在の「操作可能な全報酬」の有効／無効をプリセットとして保存する（全件スナップショット方式）。
        ///
        /// 操作不可な報酬を含めないのは、適用しても変更できず失敗するだけのため。
        /// </summary>
        /// <param name="presetName">プリセット名</param>
        /// <param name="rewards">保存対象の報酬一覧（画面に表示中のもの）</param>
        /// <param name="presetId">上書き対象のプリセットID。nullなら新規作成</param>
        /// <returns>保存したプリセットID。対象が0件だった場合はnull</returns>
        public static long? SavePreset(string presetName, List<ChannelPointRewardForm> rewards, long? presetId = null)
        {
            var targets = rewards.Where(x => x.IsManageable).ToList();
            if (targets.Count == 0) return null;

            var now = DateTime.Now;

            // プレイリストと同じくUnixミリ秒で採番する
            var savePresetId = presetId ?? JTSAHelper.GetCurrentUnixTimestampMillis();

            var header = new T_ChannelPointPresetHeader
            {
                PresetId = savePresetId,
                PresetName = presetName,
                LastUsedDateTime = now,
                CreatedDateTime = now,
                UpdatedDateTime = now
            };

            var items = targets.Select(reward => new T_ChannelPointPresetItem
            {
                PresetId = savePresetId,
                RewardId = reward.RewardId,
                RewardTitle = reward.Title,
                IsEnabled = reward.IsEnabled,
                LastUsedDateTime = now,
                CreatedDateTime = now,
                UpdatedDateTime = now
            }).ToList();

            DAO_ChannelPointPreset.InsertUpdate(header, items);

            return savePresetId;
        }


        /// <summary>
        /// プリセットを適用する。
        /// 現在の状態と突き合わせ、差分のある報酬だけ更新することで
        /// 無駄なAPI呼び出しとレート制限を避ける。
        /// </summary>
        /// <param name="presetId">適用するプリセットID</param>
        /// <param name="rewards">
        /// 画面に表示中の報酬一覧。渡した場合はこのFormも更新されるので画面に即反映される。
        /// nullの場合はAPIから取得し直す。
        /// </param>
        /// <returns>適用結果</returns>
        public static async Task<ChannelPointApplyResult> ApplyPresetAsync(
            long presetId,
            List<ChannelPointRewardForm>? rewards = null)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(ChannelPointService), "チャンネルポイントプリセット適用");

            var result = new ChannelPointApplyResult();

            var header = DAO_ChannelPointPreset.SelectHeaderById(presetId);
            if (header == null)
            {
                result.ErrorMessage = "プリセットが見つかりませんでした。";
                mainWindow.AppLogPanel.Error(nameof(ChannelPointService), result.ErrorMessage);
                mainWindow.AppLogPanel.ProcessEnd(nameof(ChannelPointService), appLogProcessName);
                return result;
            }

            result.PresetName = header.PresetName;

            var items = DAO_ChannelPointPreset.SelectItemsByPresetId(presetId);

            // 画面から渡されなかった場合はAPIから取り直す（カテゴリ紐づけの自動適用経路など）
            var currentRewards = rewards ?? (await FetchRewardsAsync())?.Rewards;
            if (currentRewards == null)
            {
                result.ErrorMessage = "チャンネルポイント一覧の取得に失敗したため適用できませんでした。";
                mainWindow.AppLogPanel.Error(nameof(ChannelPointService), result.ErrorMessage);
                mainWindow.AppLogPanel.ProcessEnd(nameof(ChannelPointService), appLogProcessName);
                return result;
            }

            foreach (var item in items)
            {
                var reward = currentRewards.FirstOrDefault(x => x.RewardId == item.RewardId);

                // 報酬が削除された、または操作不可になった場合はスキップする
                if (reward == null || !reward.IsManageable)
                {
                    result.SkippedCount++;
                    continue;
                }

                // 既に狙いの状態なら何もしない（API呼び出しを減らす）
                if (reward.IsEnabled == item.IsEnabled)
                {
                    result.UnchangedCount++;
                    continue;
                }

                var updateResult = await SetEnabledAsync(reward, item.IsEnabled);

                if (updateResult.IsSuccess)
                {
                    result.ChangedCount++;
                }
                else
                {
                    result.FailedCount++;
                    result.ErrorMessage = updateResult.ErrorMessage;

                    mainWindow.AppLogPanel.Error(nameof(ChannelPointService),
                        $"プリセット適用失敗 「 {item.RewardTitle} 」：{updateResult.ErrorMessage}");
                }
            }

            result.IsSuccess = result.FailedCount == 0;

            DAO_ChannelPointPreset.UpdateLastUsed(presetId);

            mainWindow.AppLogPanel.AddSwitchLog(result.IsSuccess, nameof(ChannelPointService),
                result.SummaryText,
                result.SummaryText + "：" + result.ErrorMessage
            );
            mainWindow.AppLogPanel.ProcessEnd(nameof(ChannelPointService), appLogProcessName);

            return result;
        }


        /// <summary>
        /// カテゴリに紐づいたプリセットを適用する。
        /// 紐づけが無い場合は何もしない（ユーザーの明示的な指定がないカテゴリでは
        /// チャンネルポイントの状態を勝手に変えない、という方針）。
        /// </summary>
        /// <param name="categoryId">カテゴリID</param>
        /// <returns>適用した場合は結果。紐づけが無い場合はnull</returns>
        public static async Task<ChannelPointApplyResult?> ApplyPresetForCategoryAsync(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return null;

            var category = DAO_Category.SelectOneById(categoryId);
            if (category?.ChannelPointPresetId == null) return null;

            return await ApplyPresetAsync(category.ChannelPointPresetId.Value);
        }

        #endregion


        #region ==================== 詰め替え ====================

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

        #endregion
    }
}
