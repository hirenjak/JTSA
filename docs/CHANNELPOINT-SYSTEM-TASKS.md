# チャンネルポイント管理システム — タスク分割と実行記録

> 対象ブランチ: `feature/manage-channelpoint`（`develop` から分岐）
> 起点コミット: `90f988d`
> 作業開始: 2026-08-09

## 1. 概要

### 目的

JTSA の CP タブ（`ChannelPointPanel`）を実用に足る管理画面へ整備する。

1. **既存の壊れている部分を直す** — 一覧が一度も読み込まれない、一時停止トグルが別の行を更新する、等
2. **操作不可な報酬をアプリ管理下へコピーする導線を作る** — Twitch Web 画面から作った報酬は OAuth アプリから操作できないため
3. **プリセット機能** — 報酬 ON/OFF の組み合わせを保存し、ワンクリックで切り替える。カテゴリに紐づけて自動適用も行う

### 決定事項

| 論点 | 決定 |
|---|---|
| コピー時のタイトル重複回避 | 接尾辞に `'`（シングルクオート）を付ける。設定で変更可 |
| プリセットの ON/OFF | `IsEnabled`（有効/無効）で切り替える。`IsPaused` はプリセット対象外 |
| プリセット未登録の報酬 | **全件スナップショット方式**。保存時に「操作可能な全報酬」の `IsEnabled` をまとめて記録し、適用時は全件をその状態に戻す |
| 適用タイミング | CP タブの手動ボタン ＋ カテゴリに紐づけての自動適用。カテゴリにプリセット指定が無ければ**何もしない** |

### Twitch API の制約（設計の前提・検証済み）

- `GET /channel_points/custom_rewards` の `only_manageable_rewards=true` は「自分の client_id が作成した報酬」のみを返す。**`false` の結果との差分＝Web/他アプリ作成分＝操作不可**。これが「操作可能」列の判定ロジック。
- **画像は作成/更新 API で指定できない**（Twitch 公式 UI のみ）。コピー先は既定アイコンになる。
- **タイトルはチャンネル内で一意**、最大 45 文字。接尾辞付与時は 45 文字を超えないよう元タイトル側を切り詰める。
- **操作不可な報酬はアプリから削除できない**。コピー後の元報酬の始末は Web UI 側でユーザーが行う。
- TwitchLib 3.10.1-preview で使えるプロパティ（reflection で確認済み）:
  - `CustomReward`: `Id / Title / Prompt / Cost / Image / BackgroundColor / IsEnabled / IsUserInputRequired / MaxPerStreamSetting / MaxPerUserPerStreamSetting / GlobalCooldownSetting / IsPaused / IsInStock / ShouldRedemptionsSkipQueue`
  - `CreateCustomRewardsRequest` / `UpdateCustomRewardRequest`: 上記のうち画像以外すべて設定可能
  - `api.Helix.ChannelPoints.DeleteCustomRewardAsync` も利用可能

## 2. タスク一覧

### Phase 0 — ブランチ準備

- [x] P0-1 `develop` を `origin/develop` まで最新化
- [x] P0-2 `feature/manage-channelpoint` ブランチ作成
- [x] P0-3 本ドキュメント作成

### Phase 1 — 土台整備と既存バグ修正

- [x] P1-1 `Forms/ChannelPointRewardForm.cs` 新規作成（INotifyPropertyChanged 実装）
- [x] P1-2 `TwitchHelper.GetCustomRewardsAsync` に `onlyManageableRewards` 引数を追加
- [x] P1-3 `Utility/ChannelPointService.cs` 新規作成（`FetchRewardsAsync` — manageable 差分で操作可否を判定）
- [x] P1-4 `ChannelPointPanel` を `ObservableCollection<ChannelPointRewardForm>` バインドへ移行（`_cachedRewards` 廃止）
- [x] P1-5 「操作可能」列の DataTemplate 実装（✔ / 🔒 ＋ ToolTip）と、操作不可行のトグル無効化
- [x] P1-6 `Initialize()` 新設 ＋ `MainWindow_LoadedAsync` から呼び出し ＋ 手動更新ボタン追加
- [x] P1-7 バグ修正: 一時停止トグルが `SelectedItem` 基準で別行を更新する問題
- [x] P1-8 バグ修正: 一時停止トグル後にキャッシュを更新せず古い状態を再表示する問題
- [x] P1-9 バグ修正: `Mode=OneWay` バインドのチェックボックスをクリックするとバインドが外れる問題（TwoWay ＋ 失敗時ロールバックへ変更）
- [x] P1-10 ビルド確認
- [ ] P1-11 実機動作確認

### Phase 2 — 報酬コピー機能

（Phase 1 完了時に具体化する）

### Phase 3 — プリセット機能

（Phase 2 完了時に具体化する）

### Phase 4 — カテゴリ紐づけ自動適用

（Phase 3 完了時に具体化する）

## 3. 実行ログ

| 日付 | タスクID | 内容 | 変更ファイル | コミット | 動作確認 |
|---|---|---|---|---|---|
| 2026-08-09 | P0-1〜3 | develop 最新化、feature ブランチ作成、本ドキュメント作成 | `docs/CHANNELPOINT-SYSTEM-TASKS.md` | — | 対象なし |
| 2026-08-09 | P1-1〜10 | 土台整備（Form/Service 追加）と既存バグ 3 件の修正 | `Forms/ChannelPointRewardForm.cs`(新), `Utility/ChannelPointService.cs`(新), `Utility/TwitchHelper.cs`, `Panels/ChannelPointPanel.xaml(.cs)`, `MainWindow.xaml.cs` | — | `dotnet build` 成功（0 エラー／新規ファイルの警告なし）。**実機動作は未検証** |

## 4. 課題・保留

| # | 内容 | 状態 |
|---|---|---|
| 1 | 報酬タイトルに `'`（シングルクオート）が使えるかは実機未検証。使えない場合は接尾辞を別文字（例 `*` や `_`）へ切り替えられるよう、設定値（`SettingName.ChannelPointCopySuffix`）で持たせる | 未検証 |
| 2 | コピー後の元報酬（操作不可）はアプリから削除できないため、Web UI での無効化/削除をユーザーに案内する必要がある | 設計に反映済み |
| 3 | `DAO_GamePlaylist.InsertUpdate(header, items)` に既存アイテム削除の未実装バグがある（本改修の対象外だが、プリセット DAO で同じ実装を真似しないこと） | 別課題として放置 |
| 4 | `only_manageable_rewards=true` の GET に失敗した場合、操作可否が判定できない。安全側に倒して**全件を操作不可**として表示する仕様にした（誤操作で 403 を踏むより良いと判断） | 仕様として確定 |
| 5 | Phase 1 時点では新規作成フォームは名前・コストのみ（従来どおり）。Prompt・背景色・クールダウン等の入力は Phase 2 でコピー機能と共用する形で追加する | Phase 2 で対応 |
