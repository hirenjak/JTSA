using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using JTSA.Utility;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTSA.Panels
{
    /// <summary>
    /// ChannelPointPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class ChannelPointPanel : UserControl
    {
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary> 画面に表示している報酬一覧 </summary>
        public ObservableCollection<ChannelPointRewardForm> ChannelPointRewardFormList { get; } = [];

        public ObservableCollection<ChannelPointRewardForm> EnabledChannelPointRewardFormList { get; } = [];
        public ObservableCollection<ChannelPointRewardForm> PausedChannelPointRewardFormList { get; } = [];
        public ObservableCollection<ChannelPointRewardForm> DisabledChannelPointRewardFormList { get; } = [];

        private Point _dragStartPoint;

        /// <summary> プリセット一覧 </summary>
        public ObservableCollection<ChannelPointPresetForm> ChannelPointPresetFormList { get; } = [];

        /// <summary> 選択中プリセットの内訳 </summary>
        public ObservableCollection<ChannelPointPresetItemForm> ChannelPointPresetItemFormList { get; } = [];

        /// <summary>
        /// 報酬一覧の取得に成功しているか。
        /// 取得できていないのに「報酬が存在しない」と判断してしまうと
        /// プリセットの中身を誤って削除してしまうため、掃除処理の実行条件に使う。
        /// </summary>
        private bool _isRewardListLoaded = false;

        /// <summary> 起動時に自動選択するプリセット名（大文字小文字は区別しない） </summary>
        private static readonly string[] DEFAULT_PRESET_NAMES = ["default", "デフォルト"];


        public ChannelPointPanel()
        {
            InitializeComponent();

            // 画面紐づけ
            DataContext = this;
        }


        /// <summary>
        /// 遅延初期化。認証が終わっていないとAPIを叩けないため、
        /// コンストラクタではなくMainWindowの起動シーケンスから呼ばれる。
        /// </summary>
        public async Task Initialize()
        {
            await ReloadChannnelPoint();

            ReloadPreset();
        }


        /// <summary>
        /// 更新ボタン押下
        /// </summary>
        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            await ReloadChannnelPoint();
        }


        /// <summary>
        /// チャンネルポイント一覧をAPIから取り直して画面へ反映する
        /// </summary>
        public async Task ReloadChannnelPoint()
        {
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(GetType().Name, "チャンネルポイントリスト再読み込み");
            var selectedRewardId = (CpManagementListView.SelectedItem as ChannelPointRewardForm)?.RewardId;

            ReloadButton.IsEnabled = false;
            CpManagementReloadButton.IsEnabled = false;

            var fetchResult = await ChannelPointService.FetchRewardsAsync();

            ChannelPointRewardFormList.Clear();

            if (fetchResult != null)
            {
                foreach (var reward in fetchResult.Rewards)
                {
                    ChannelPointRewardFormList.Add(reward);
                }

                CpManagementListView.SelectedItem =
                    ChannelPointRewardFormList.FirstOrDefault(x => x.RewardId == selectedRewardId)
                    ?? ChannelPointRewardFormList.FirstOrDefault();

                mainWindow.AppLogPanel.Success(GetType().Name, BuildStatusText(fetchResult));
                mainWindow.AppLogPanel.Success(GetType().Name, appLogProcessName);
            }
            else
            {
                mainWindow.AppLogPanel.Error(GetType().Name, "チャンネルポイントリスト取得失敗");
            }

            // 取得した一覧を状態別の3ペインへ振り分ける
            RefreshRewardStateLists();

            // 取得に失敗した状態で「報酬が存在しない」と判断するとプリセットを壊すため、成否を覚えておく
            _isRewardListLoaded = fetchResult != null;

            ReloadButton.IsEnabled = true;
            CpManagementReloadButton.IsEnabled = true;

            // 一覧が変わったので、選択中プリセットの内訳も作り直す
            RefreshSelectedPresetDetail();

            mainWindow.AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
        }

        private void CpNavigationTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, e.OriginalSource) ||
                !CpSettingsTab.IsSelected ||
                CpManagementListView.SelectedItem != null) return;
            CpManagementListView.SelectedItem = ChannelPointRewardFormList.FirstOrDefault();
        }


        /// <summary>
        /// 一覧上部に出すステータス文言を組み立てる。
        ///
        /// 「本当に操作可能な報酬が0件」なのか「操作可否を判定できなかった」のかは
        /// 対処法が全く違うため、必ず区別して表示する。
        /// </summary>
        /// <param name="fetchResult">取得結果</param>
        /// <returns>表示するステータス文言</returns>
        private static string BuildStatusText(ChannelPointFetchResult fetchResult)
        {
            var totalCount = fetchResult.Rewards.Count;
            var manageableCount = fetchResult.Rewards.Count(x => x.IsManageable);
            var lockedCount = totalCount - manageableCount;

            if (!fetchResult.IsManageableCheckSucceeded)
            {
                return $"取得成功！ ({totalCount}件)\n"
                     + "⚠ 操作可否の判定に失敗したため、全件を操作不可として表示しています。"
                     + "「更新」で再試行してください（解消しない場合は Setting タブから再認証）。";
            }

            var statusText = $"取得成功！ ({totalCount}件) / ✔ 操作可能 {manageableCount}件 / 🔒 操作不可 {lockedCount}件";

            if (manageableCount == 0 && totalCount > 0)
            {
                statusText += "\n⚠ このアプリから操作できる報酬がありません。"
                            + "🔒 の報酬にチェックを入れて「選択をコピー」すると、操作できる報酬が作られます。"
                            + "（プリセットの保存も操作可能な報酬が必要です）";
            }
            else if (lockedCount > 0)
            {
                statusText += "\n🔒 は Twitch の Web 画面から作成された報酬です。コピーするとこのアプリから操作できるようになります。";
            }

            return statusText;
        }


        /// <summary>
        /// 有効/無効トグル
        /// </summary>
        private async void ToggleIsEnabled_Click(object sender, RoutedEventArgs e)
        {
            // クリックされたチェックボックス自身の行を対象にする（選択行ではない）
            if (sender is not CheckBox checkBox || checkBox.DataContext is not ChannelPointRewardForm reward) return;

            // TwoWayバインドによりクリック時点でFormへ反映済み。その値をそのままAPIへ送る
            var requestValue = reward.IsEnabled;

            var result = await ChannelPointService.SetEnabledAsync(reward, requestValue);

            mainWindow.AppLogPanel.AddSwitchLog(result.IsSuccess, GetType().Name,
                $"有効/無効の切り替え成功 「 {reward.Title} 」→ {(requestValue ? "有効" : "無効")}",
                $"有効/無効の切り替え失敗 「 {reward.Title} 」：{result.ErrorMessage}"
            );

            if (!result.IsSuccess)
            {
                // 送信に失敗したので画面の見た目を元に戻す
                reward.IsEnabled = !requestValue;
                MessageBox.Show($"有効/無効の切り替えに失敗しました。\n\n{result.ErrorMessage}");
            }

            RefreshRewardStateLists();
        }


        /// <summary>
        /// 一時停止トグル
        /// </summary>
        private async void ToggleIsPaused_Click(object sender, RoutedEventArgs e)
        {
            // クリックされたチェックボックス自身の行を対象にする（選択行ではない）
            if (sender is not CheckBox checkBox || checkBox.DataContext is not ChannelPointRewardForm reward) return;

            // TwoWayバインドによりクリック時点でFormへ反映済み。その値をそのままAPIへ送る
            var requestValue = reward.IsPaused;

            var result = await ChannelPointService.SetPausedAsync(reward, requestValue);

            mainWindow.AppLogPanel.AddSwitchLog(result.IsSuccess, GetType().Name,
                $"一時停止の切り替え成功 「 {reward.Title} 」→ {(requestValue ? "一時停止" : "再開")}",
                $"一時停止の切り替え失敗 「 {reward.Title} 」：{result.ErrorMessage}"
            );

            if (!result.IsSuccess)
            {
                // 送信に失敗したので画面の見た目を元に戻す
                reward.IsPaused = !requestValue;
                MessageBox.Show($"一時停止の切り替えに失敗しました。\n\n{result.ErrorMessage}");
            }

            RefreshRewardStateLists();
        }


        private void RefreshRewardStateLists()
        {
            EnabledChannelPointRewardFormList.Clear();
            PausedChannelPointRewardFormList.Clear();
            DisabledChannelPointRewardFormList.Clear();

            foreach (var reward in ChannelPointRewardFormList)
            {
                if (!reward.IsEnabled)
                    DisabledChannelPointRewardFormList.Add(reward);
                else if (reward.IsPaused)
                    PausedChannelPointRewardFormList.Add(reward);
                else
                    EnabledChannelPointRewardFormList.Add(reward);
            }
        }


        private void RewardList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }


        private void RewardList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || sender is not ListView listView) return;

            var currentPoint = e.GetPosition(null);
            if (Math.Abs(currentPoint.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
             && Math.Abs(currentPoint.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var container = ItemsControl.ContainerFromElement(listView, e.OriginalSource as DependencyObject) as ListViewItem;
            if (container?.DataContext is ChannelPointRewardForm reward)
                DragDrop.DoDragDrop(container, reward, DragDropEffects.Move);
        }


        private async void RewardList_Drop(object sender, DragEventArgs e)
        {
            if (sender is not ListView listView
             || listView.Tag is not string targetState
             || e.Data.GetData(typeof(ChannelPointRewardForm)) is not ChannelPointRewardForm reward
             || !reward.IsManageable) return;

            var originalEnabled = reward.IsEnabled;
            var originalPaused = reward.IsPaused;
            var targetEnabled = targetState != "Disabled";
            var targetPaused = targetState == "Paused";

            if (originalEnabled == targetEnabled && originalPaused == targetPaused) return;

            listView.IsEnabled = false;
            var errorMessage = "";

            if (reward.IsEnabled != targetEnabled)
            {
                var enabledResult = await ChannelPointService.SetEnabledAsync(reward, targetEnabled);
                if (enabledResult.IsSuccess)
                    reward.IsEnabled = targetEnabled;
                else
                    errorMessage = enabledResult.ErrorMessage;
            }

            if (string.IsNullOrEmpty(errorMessage) && reward.IsPaused != targetPaused)
            {
                var pausedResult = await ChannelPointService.SetPausedAsync(reward, targetPaused);
                if (pausedResult.IsSuccess)
                    reward.IsPaused = targetPaused;
                else
                    errorMessage = pausedResult.ErrorMessage;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                if (reward.IsEnabled != originalEnabled)
                    await ChannelPointService.SetEnabledAsync(reward, originalEnabled);
                if (reward.IsPaused != originalPaused)
                    await ChannelPointService.SetPausedAsync(reward, originalPaused);

                reward.IsEnabled = originalEnabled;
                reward.IsPaused = originalPaused;
                MessageBox.Show($"状態の変更に失敗しました。\n\n{errorMessage}");
            }
            else
            {
                mainWindow.AppLogPanel.Success(GetType().Name,
                    $"CP状態変更 「 {reward.Title} 」→ {GetRewardStateLabel(targetState)}");
            }

            listView.IsEnabled = true;
            RefreshRewardStateLists();
        }


        private static string GetRewardStateLabel(string state) => state switch
        {
            "Paused" => "一時停止中",
            "Disabled" => "無効",
            _ => "有効"
        };


        /// <summary>
        /// 新規作成ボタン押下
        /// </summary>
        private async void CreateRewardButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ChannelPointRewardCreateWindow
            {
                Owner = mainWindow
            };

            if (window.ShowDialog() == true)
                await ReloadChannnelPoint();
        }

        private async void SaveManagedRewardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ChannelPointRewardForm reward }) return;
            if (!int.TryParse(CpManagementCostTextBox.Text.Trim(), out var cost) || cost < 1)
            {
                MessageBox.Show("1以上のコストを入力してください。", "入力内容を確認");
                return;
            }

            var result = await ChannelPointService.UpdateDetailsAsync(
                reward, reward.Title, CpManagementPromptTextBox.Text.Trim(), cost);
            if (!result.IsSuccess)
            {
                MessageBox.Show($"更新に失敗しました。\n\n{result.ErrorMessage}", "更新失敗");
                return;
            }

            await ReloadChannnelPoint();
        }

        private void CpManagementListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetCpNameEditing(false);
        }

        private async void CpNameEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (CpManagementListView.SelectedItem is not ChannelPointRewardForm reward || !reward.IsManageable) return;

            if (CpDetailNameTextBox.Visibility != Visibility.Visible)
            {
                CpDetailNameTextBox.Text = reward.Title;
                SetCpNameEditing(true);
                CpDetailNameTextBox.Focus();
                CpDetailNameTextBox.SelectAll();
                return;
            }

            var title = CpDetailNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(title) ||
                !int.TryParse(CpManagementCostTextBox.Text.Trim(), out var cost) || cost < 1)
            {
                MessageBox.Show("CP名と1以上のコストを入力してください。", "入力内容を確認");
                return;
            }

            var result = await ChannelPointService.UpdateDetailsAsync(
                reward, title, CpManagementPromptTextBox.Text.Trim(), cost);
            if (!result.IsSuccess)
            {
                MessageBox.Show($"更新に失敗しました。\n\n{result.ErrorMessage}", "更新失敗");
                return;
            }

            await ReloadChannnelPoint();
        }

        private void SetCpNameEditing(bool isEditing)
        {
            CpDetailTitleTextBlock.Visibility = isEditing ? Visibility.Collapsed : Visibility.Visible;
            CpDetailNameTextBox.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
            CpNameEditButton.Content = isEditing ? "✓" : "✎";
            CpNameEditButton.ToolTip = isEditing ? "CP名の変更を確定" : "CP名を編集";
        }


        #region ==================== プリセット ====================

        /// <summary>
        /// プリセット一覧を読み込み直す
        /// </summary>
        /// <param name="selectPresetId">読み込み後に選択しておくプリセットID</param>
        public void ReloadPreset(long? selectPresetId = null)
        {
            var itemCounts = DAO_ChannelPointPreset.SelectItemCounts();
            var headers = DAO_ChannelPointPreset.SelectAllHeader();
            var appliedPresetId = long.TryParse(
                DAO_Setting.SelectOneById(DAO_Setting.SettingName.AppliedChannelPointPresetId)?.Value,
                out var parsedAppliedPresetId)
                ? parsedAppliedPresetId
                : (long?)null;

            ChannelPointPresetFormList.Clear();

            foreach (var header in headers)
            {
                ChannelPointPresetFormList.Add(new ChannelPointPresetForm
                {
                    PresetId = header.PresetId,
                    PresetName = header.PresetName,
                    ItemCount = itemCounts.TryGetValue(header.PresetId, out var count) ? count : 0,
                    LastUsedDate = header.LastUsedDateTime.ToString("yyyy/MM/dd HH:mm"),
                    IsApplied = header.PresetId == appliedPresetId
                });
            }

            // 選択の指定が無い場合は適用中のプリセットを優先し、無ければ既定を選ぶ
            selectPresetId ??= appliedPresetId ?? FindDefaultPresetId(headers);

            if (selectPresetId != null)
            {
                PresetComboBox.SelectedItem =
                    ChannelPointPresetFormList.FirstOrDefault(x => x.PresetId == selectPresetId);
            }

            // カテゴリ画面にも増減を反映する。
            // 選択肢だけ差し替えるとバインド中のComboBoxが選択を失って紐づけを壊すため、
            // カテゴリ一覧ごと作り直す（ReloadCategory は一覧をクリアしてから選択肢を入れ替える）
            mainWindow.CategoryPanel.ReloadCategory();
        }


        /// <summary>
        /// 現在選択中のプリセットID。未選択ならnull
        /// </summary>
        private long? GetSelectedPresetId()
        {
            return (PresetComboBox.SelectedItem as ChannelPointPresetForm)?.PresetId;
        }


        /// <summary>
        /// 初期表示で選ぶプリセットを決める。
        /// 「default」「デフォルト」という名前のものを優先し、
        /// 無ければ一番古く作られたものを選ぶ。
        /// </summary>
        /// <param name="headers">プリセットヘッダの一覧</param>
        /// <returns>選択するプリセットID。プリセットが1件も無い場合はnull</returns>
        private static long? FindDefaultPresetId(List<T_ChannelPointPresetHeader> headers)
        {
            if (headers.Count == 0) return null;

            // 同名が複数ある場合も含め、常に古い方を優先する
            var ordered = headers.OrderBy(x => x.CreatedDateTime).ToList();

            var defaultNamed = ordered.FirstOrDefault(x => IsDefaultPresetName(x.PresetName));

            return (defaultNamed ?? ordered.First()).PresetId;
        }


        /// <summary>
        /// 既定として扱うプリセット名かどうか
        /// </summary>
        /// <param name="presetName">プリセット名</param>
        /// <returns>true：既定として扱う</returns>
        private static bool IsDefaultPresetName(string presetName)
        {
            var trimmedName = presetName.Trim();

            return DEFAULT_PRESET_NAMES.Any(x => trimmedName.Equals(x, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// プリセット選択時：内訳を表示する
        /// </summary>
        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetPresetNameEditing(false);
            RefreshSelectedPresetDetail();

            if (PresetComboBox.SelectedItem is ChannelPointPresetForm selectedPreset)
            {
                PresetDetailTitleTextBlock.Text = selectedPreset.PresetName;
                PresetNameEditButton.IsEnabled = true;
            }
            else
            {
                PresetDetailTitleTextBlock.Text = "プリセットを選択してください";
                PresetNameEditButton.IsEnabled = false;
            }
        }


        /// <summary>
        /// 選択中プリセットの内訳を組み立て直す。
        /// 報酬を削除した後など、「削除済み」判定が変わったときにも呼ぶ。
        /// </summary>
        private void RefreshSelectedPresetDetail()
        {
            ChannelPointPresetItemFormList.Clear();

            if (PresetComboBox.SelectedItem is not ChannelPointPresetForm preset)
            {
                PresetItemListView.Visibility = Visibility.Collapsed;
                PresetDetailStatus.Text = "プリセットを選択すると内容が表示されます。";
                return;
            }

            var items = DAO_ChannelPointPreset.SelectItemsByPresetId(preset.PresetId);

            foreach (var item in items.OrderByDescending(x => x.IsEnabled).ThenBy(x => x.RewardTitle))
            {
                // 報酬一覧に存在するかどうかだけを見る（操作可否は適用時に判定する）。
                // 一覧を取得できていないときは存在の判断がつかないので「ある」扱いにして誤表示を防ぐ
                var isExisting = !_isRewardListLoaded
                              || ChannelPointRewardFormList.Any(x => x.RewardId == item.RewardId);

                ChannelPointPresetItemFormList.Add(new ChannelPointPresetItemForm
                {
                    RewardId = item.RewardId,
                    RewardTitle = item.RewardTitle,
                    IsEnabled = item.IsEnabled,
                    IsPaused = item.IsPaused,
                    IsExisting = isExisting
                });
            }

            var missingCount = ChannelPointPresetItemFormList.Count(x => !x.IsExisting);

            PresetItemListView.Visibility = Visibility.Visible;
            PresetDetailStatus.Text =
                $"「{preset.PresetName}」：有効 {ChannelPointPresetItemFormList.Count(x => x.IsActiveState)}件 / "
                + $"一時停止 {ChannelPointPresetItemFormList.Count(x => x.IsPausedState)}件 / "
                + $"無効 {ChannelPointPresetItemFormList.Count(x => x.IsDisabledState)}件"
                + (missingCount > 0 ? $"　※（削除済み）の{missingCount}件はプリセットから取り除きました" : "")
                + $"　最終適用: {preset.LastUsedDate}";

            // Twitch の Web 画面や他アプリで削除された報酬をプリセットから掃除する。
            // 今表示している内容は（削除済み）付きで残し、次回以降は出てこなくなる。
            PurgeMissingPresetItems();
        }


        /// <summary>
        /// 現存しない報酬をプリセットから取り除く。
        ///
        /// アプリから削除した報酬は削除時にプリセットからも消えるため、
        /// ここで消えるのは Twitch の Web 画面や他アプリで削除された報酬になる。
        /// </summary>
        private void PurgeMissingPresetItems()
        {
            // 一覧を取得できていない状態で実行すると全件を「存在しない」と誤判定してしまう
            if (!_isRewardListLoaded) return;

            var existingRewardIds = ChannelPointRewardFormList.Select(x => x.RewardId).ToList();

            var removedCount = DAO_ChannelPointPreset.DeleteItemsNotInRewardIds(existingRewardIds);
            if (removedCount == 0) return;

            mainWindow.AppLogPanel.Success(GetType().Name,
                $"存在しない報酬をプリセットから除去（{removedCount}件）");
        }


        /// <summary>
        /// プリセット一覧のダブルクリックで適用する
        /// </summary>
        private async void PresetComboBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox ||
                ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject)
                    is not ListBoxItem { DataContext: ChannelPointPresetForm preset }) return;

            listBox.IsEnabled = false;
            try
            {
                // 画面の一覧をそのまま渡すことで、更新結果が即座に画面へ反映される
                var result = await ChannelPointService.ApplyPresetAsync(
                    preset.PresetId,
                    ChannelPointRewardFormList.ToList());

                RefreshRewardStateLists();

                // 適用日時・件数・適用中表示を反映する
                ReloadPreset(preset.PresetId);

                if (result.IsSuccess)
                {
                    mainWindow.AppLogPanel.Success(GetType().Name, result.SummaryText);
                }
                else
                {
                    MessageBox.Show($"{result.SummaryText}\n\n{result.ErrorMessage}");
                }
            }
            finally
            {
                listBox.IsEnabled = true;
            }
        }


        /// <summary>
        /// 追加ボタン押下：今の一覧の有効/無効を新しいプリセットとして保存する
        /// </summary>
        private void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            const string baseName = "新しいプリセット";
            var existingNames = ChannelPointPresetFormList
                .Select(x => x.PresetName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var presetName = baseName;
            for (var suffix = 2; existingNames.Contains(presetName); suffix++)
                presetName = $"{baseName} ({suffix})";

            var savedPresetId = SavePreset(presetName, null);
            if (savedPresetId == null) return;

            ReloadPreset(savedPresetId);

            BeginPresetNameEdit();
        }


        /// <summary>
        /// 現在の一覧をプリセットとして保存する共通処理
        /// </summary>
        /// <param name="presetName">プリセット名</param>
        /// <param name="presetId">上書き対象。nullなら新規</param>
        /// <returns>保存したプリセットID。保存できなかった場合はnull</returns>
        private long? SavePreset(string presetName, long? presetId)
        {
            var savedPresetId = ChannelPointService.SavePreset(
                presetName,
                ChannelPointRewardFormList.ToList(),
                presetId);

            if (savedPresetId == null)
            {
                MessageBox.Show("保存できる報酬がありません。\n\nプリセットに保存できるのは「操作可能（✔）」の報酬だけです。");

                mainWindow.AppLogPanel.Error(GetType().Name, $"プリセット保存失敗 「 {presetName} 」：対象の報酬が0件");
                return null;
            }

            mainWindow.AppLogPanel.Success(GetType().Name, $"プリセット保存 「 {presetName} 」");

            return savedPresetId;
        }


        private void PresetNameEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetDetailNameTextBox.Visibility != Visibility.Visible)
            {
                BeginPresetNameEdit();
                return;
            }

            if (PresetComboBox.SelectedItem is not ChannelPointPresetForm preset) return;

            var presetName = PresetDetailNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(presetName))
            {
                MessageBox.Show("新しいプリセット名を入力してください。");
                return;
            }

            var isSuccess = DAO_ChannelPointPreset.UpdateName(preset.PresetId, presetName);

            mainWindow.AppLogPanel.AddSwitchLog(isSuccess, GetType().Name,
                $"プリセット名変更 「 {preset.PresetName} 」→「 {presetName} 」",
                $"プリセット名変更失敗 「 {preset.PresetName} 」"
            );

            ReloadPreset(preset.PresetId);
        }

        private void BeginPresetNameEdit()
        {
            if (PresetComboBox.SelectedItem is not ChannelPointPresetForm preset) return;

            PresetDetailNameTextBox.Text = preset.PresetName;
            SetPresetNameEditing(true);
            PresetDetailNameTextBox.Focus();
            PresetDetailNameTextBox.SelectAll();
        }

        private void SetPresetNameEditing(bool isEditing)
        {
            PresetDetailTitleTextBlock.Visibility = isEditing
                ? Visibility.Collapsed
                : Visibility.Visible;
            PresetDetailNameTextBox.Visibility = isEditing
                ? Visibility.Visible
                : Visibility.Collapsed;
            PresetNameEditButton.Content = isEditing ? "✓" : "✎";
            PresetNameEditButton.ToolTip = isEditing
                ? "名前の変更を確定"
                : "プリセット名を編集";
        }

        private void PresetItemStateRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is not ChannelPointPresetForm preset ||
                sender is not RadioButton { DataContext: ChannelPointPresetItemForm item }) return;

            var saved = DAO_ChannelPointPreset.UpdateItemState(
                preset.PresetId, item.RewardId, item.IsEnabled, item.IsPaused);
            if (!saved)
            {
                MessageBox.Show("プリセット内容を更新できませんでした。");
            }

            RefreshSelectedPresetDetail();
        }

        private void DeletePresetItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is not ChannelPointPresetForm preset ||
                sender is not Button { Tag: ChannelPointPresetItemForm item }) return;

            e.Handled = true;
            if (!DAO_ChannelPointPreset.DeleteItem(preset.PresetId, item.RewardId))
            {
                MessageBox.Show("プリセットからCPを削除できませんでした。");
                return;
            }

            ReloadPreset(preset.PresetId);
        }

        private void RegisterPresetRewardsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is not ChannelPointPresetForm preset)
            {
                MessageBox.Show("CPを登録するプリセットを選択してください。");
                return;
            }

            var existingIds = DAO_ChannelPointPreset.SelectItemsByPresetId(preset.PresetId)
                .Select(x => x.RewardId)
                .ToHashSet();
            var window = new ChannelPointPresetRewardSelectionWindow(existingIds)
            {
                Owner = mainWindow
            };

            if (window.ShowDialog() != true || window.SelectedRewards.Count == 0) return;

            var now = DateTime.Now;
            DAO_ChannelPointPreset.AddItems(
                preset.PresetId,
                window.SelectedRewards.Select(reward => new T_ChannelPointPresetItem
                {
                    PresetId = preset.PresetId,
                    RewardId = reward.RewardId,
                    RewardTitle = reward.Title,
                    IsEnabled = reward.IsEnabled,
                    IsPaused = reward.IsPaused,
                    CreatedDateTime = now,
                    UpdatedDateTime = now,
                    LastUsedDateTime = now
                }));

            ReloadPreset(preset.PresetId);
        }


        /// <summary>
        /// 削除ボタン押下
        /// </summary>
        private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ChannelPointPresetForm preset }) return;
            e.Handled = true;

            var confirm = MessageBox.Show(
                $"プリセット「{preset.PresetName}」を削除します。よろしいですか？",
                "プリセットの削除", MessageBoxButton.OKCancel);

            if (confirm != MessageBoxResult.OK) return;

            var isSuccess = DAO_ChannelPointPreset.Delete(preset.PresetId);
            if (isSuccess && preset.IsApplied)
            {
                DAO_Setting.InsertUpdate(
                    DAO_Setting.SettingName.AppliedChannelPointPresetId,
                    string.Empty);
            }

            mainWindow.AppLogPanel.AddSwitchLog(isSuccess, GetType().Name,
                $"プリセット削除 「 {preset.PresetName} 」",
                $"プリセット削除失敗 「 {preset.PresetName} 」"
            );

            ReloadPreset();
        }

        private void PresetDeleteButton_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        #endregion


        #region ==================== 削除 ====================

        /// <summary>
        /// 行内の削除ボタン押下。取り返しがつかないので必ず確認してから実行する。
        /// </summary>
        private async void DeleteRewardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ChannelPointRewardForm reward) return;

            if (!ConfirmDeleteReward(reward)) return;

            var result = await ChannelPointService.DeleteRewardAsync(reward);

            mainWindow.AppLogPanel.AddSwitchLog(result.IsSuccess, GetType().Name,
                $"報酬削除 「 {reward.Title} 」",
                $"報酬削除失敗 「 {reward.Title} 」：{result.ErrorMessage}"
            );

            if (!result.IsSuccess)
            {
                MessageBox.Show($"削除に失敗しました。\n\n{result.ErrorMessage}");
                return;
            }

            // 一覧・キャッシュ・プリセット内訳をまとめて更新する
            // （プリセットからの除去は削除処理側で済んでいるので「削除済み」表示にはならない）
            await ReloadChannnelPoint();
            ReloadPreset(GetSelectedPresetId());

            MessageBox.Show($"報酬「{reward.Title}」を削除しました。");
        }


        /// <summary>
        /// 削除前の確認ダイアログ。
        /// プリセットで使われている場合は、どのプリセットに影響するかも示す。
        /// </summary>
        /// <param name="reward">削除対象</param>
        /// <returns>true：削除してよい</returns>
        private static bool ConfirmDeleteReward(ChannelPointRewardForm reward)
        {
            var message = new System.Text.StringBuilder();

            message.AppendLine($"報酬「{reward.Title}」（{reward.Cost} ポイント）を Twitch から削除します。");
            message.AppendLine();
            message.AppendLine("この操作は取り消せません。視聴者の交換履歴からも参照できなくなります。");

            var usedPresetNames = DAO_ChannelPointPreset.SelectPresetNamesByRewardId(reward.RewardId);
            if (usedPresetNames.Count > 0)
            {
                message.AppendLine();
                message.AppendLine($"※ この報酬は次のプリセットで使われています（{usedPresetNames.Count}件）。");
                message.AppendLine("　 削除後は該当項目が「削除済み」となり、適用時にスキップされます。");

                foreach (var presetName in usedPresetNames.Distinct())
                {
                    message.AppendLine($"　・{presetName}");
                }
            }

            message.AppendLine();
            message.Append("削除してよろしいですか？");

            var confirm = MessageBox.Show(
                message.ToString(),
                "チャンネルポイント報酬の削除",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);

            return confirm == MessageBoxResult.OK;
        }

        #endregion


        #region ==================== コピー ====================

        /// <summary>
        /// 行内のコピーボタン押下（1件だけコピー）
        /// </summary>
        private async void CopyRewardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ChannelPointRewardForm reward) return;

            await CopyRewardsAsync([reward]);
        }


        /// <summary>
        /// 報酬のコピーを実行し、結果をまとめて通知する
        /// </summary>
        /// <param name="targets">コピー対象</param>
        private async Task CopyRewardsAsync(List<ChannelPointRewardForm> targets)
        {
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(GetType().Name, "チャンネルポイント報酬コピー");

            var suffix = ChannelPointService.GetCopySuffix();
            var results = new List<ChannelPointCopyResult>();

            foreach (var target in targets)
            {
                var result = await ChannelPointService.CopyRewardAsync(target, suffix);
                results.Add(result);

                mainWindow.AppLogPanel.AddSwitchLog(result.IsSuccess, GetType().Name,
                    $"報酬コピー成功 「 {result.SourceTitle} 」→「 {result.CreatedTitle} 」",
                    $"報酬コピー失敗 「 {result.SourceTitle} 」：{result.ErrorMessage}"
                );
            }

            // コピー分を一覧へ反映する
            await ReloadChannnelPoint();

            ShowCopyResult(results);

            mainWindow.AppLogPanel.ProcessEnd(GetType().Name, appLogProcessName);
        }


        /// <summary>
        /// コピー結果と、ユーザーが Web 画面で行う必要がある後始末を案内する
        /// </summary>
        /// <param name="results">コピー結果</param>
        private void ShowCopyResult(List<ChannelPointCopyResult> results)
        {
            var successList = results.Where(x => x.IsSuccess).ToList();
            var failureList = results.Where(x => !x.IsSuccess).ToList();

            var message = new System.Text.StringBuilder();

            if (successList.Count > 0)
            {
                message.AppendLine($"■ コピーしました（{successList.Count}件）");
                foreach (var success in successList)
                {
                    message.AppendLine($"　「{success.SourceTitle}」→「{success.CreatedTitle}」");
                }
                message.AppendLine();
                message.AppendLine("・画像は Twitch API では設定できないため引き継がれません。Twitch の Web 画面から設定してください。");
                message.AppendLine("・コピー元の報酬はこのアプリからは削除できません。Twitch の Web 画面で無効化または削除してください。");
            }

            if (failureList.Count > 0)
            {
                if (successList.Count > 0) message.AppendLine();

                message.AppendLine($"■ 失敗しました（{failureList.Count}件）");
                foreach (var failure in failureList)
                {
                    message.AppendLine($"　「{failure.SourceTitle}」：{failure.ErrorMessage}");
                }
            }

            MessageBox.Show(message.ToString(), "チャンネルポイントのコピー結果");
        }


        #endregion
    }
}
