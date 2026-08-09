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
- [x] P1-11 実機動作確認（一覧取得まで。DB に 29 件の報酬キャッシュを確認）
- [x] P1-12 操作可否の判定失敗と「本当に操作可能 0 件」を画面上で区別できるように（判定失敗時は警告文を出す）

### Phase 1.5 — OAuth 認証まわりの緊急修正（計画外・実機検証の前提として追加）

Phase 1 の動作確認でアプリを起動したところ、OAuth 認証直後に NullReferenceException で強制終了した。
CP タブの初期化まで到達できず検証不能だったため、ユーザー判断のうえこのブランチで修正した。

- [x] P1.5-1 `OAuthButton_Click` に認証後の初期化処理が無く落ちる問題を修正（`InitializeAfterAuthAsync` へ共通化）
- [x] P1.5-2 `accessTokenResponse` / `deviceCodeResponse` が null でも参照していた箇所に null チェックを追加
- [x] P1.5-3 `StreamerDataSet` の null 参照 2 箇所を修正（`streamInfo` が null、未登録カテゴリで `dbCategoryData` が null のまま代入）
- [x] P1.5-4 Twitch ユーザー名の手入力を廃止（`TwitchHelper.GetAuthenticatedUserAsync` でアクセストークンから特定）
- [x] P1.5-5 デバイスコード認証 URL に `user_code` としてログイン名を連結していた誤りを修正（`verification_uri_complete` を使用）

### Phase 2 — 報酬コピー機能

- [x] P2-1 `Utility/TwitchApiResult.cs` 新規作成（失敗理由を分類して呼び出し元へ返す）
- [x] P2-2 `TwitchHelper` の作成/更新を `TwitchApiResult` 返却へ変更、`DeleteCustomRewardAsync` を追加
- [x] P2-3 `SettingName.ChannelPointCopySuffix = 10` を追加（既定値 `'`）
- [x] P2-4 `ChannelPointService.CopyRewardAsync`（45 文字制限・重複時の接尾辞リトライ・画像以外の全項目引き継ぎ）
- [x] P2-5 一覧に「選択」チェック列と行内「コピー」ボタン列を追加、ツールバーに「選択をコピー」を追加
- [x] P2-6 コピー結果ダイアログ（画像とコピー元の後始末を案内）と「Twitchの報酬設定を開く」ボタン
- [x] P2-7 トグル失敗時に理由（403/400 等）をダイアログとログに表示
- [x] P2-8 ビルド確認
- [x] P2-9 実機動作確認（3 件コピー成功。接尾辞 `'` は Twitch に受け付けられた）

### Phase 3 — プリセット機能

（Phase 2 完了時に具体化する）

### Phase 3 — プリセット機能

- [x] P3-1 モデル 3 つ追加（`M_ChannelPoint` / `T_ChannelPointPresetHeader` / `T_ChannelPointPresetItem`）
- [x] P3-2 `AppDbContext` に DbSet と複合キー定義を追加、`M_Category.ChannelPointPresetId` を追加
- [x] P3-3 マイグレーション `20260809075615_channel_point_preset` を生成
- [x] P3-4 `DAO_ChannelPoint`（キャッシュ同期）と `DAO_ChannelPointPreset`（ヘッダ＋アイテム）を追加
- [x] P3-5 `Forms/ChannelPointPresetForm.cs`（一覧用＋内訳用）を追加
- [x] P3-6 `ChannelPointService.SavePreset` / `ApplyPresetAsync`（差分のある報酬だけ更新）
- [x] P3-7 CP タブにプリセット UI（選択・適用・新規保存・上書き・名前変更・削除・内訳表示）
- [x] P3-8 ビルド確認
- [x] P3-9 実機動作確認（プリセット 2 件を保存、それぞれ 4 回 / 2 回の適用を確認）

### Phase 4 — カテゴリ紐づけ自動適用

- [x] P4-1 `CategoryForm.ChannelPointPresetId` を追加（0＝紐づけなし）
- [x] P4-2 CategoryPanel の各カテゴリ行にプリセット選択 ComboBox を追加し、変更時に保存
- [x] P4-3 `ChannelPointService.ApplyPresetForCategoryAsync`（紐づけが無ければ無操作）
- [x] P4-4 `MainWindow.ApplyChannelPointPresetForCategoryAsync` を追加し、タイトル送信後に呼び出し
- [x] P4-5 PlayingGamePanel の「プレイ中」設定時にも呼び出し
- [x] P4-6 プリセット削除時にカテゴリからの紐づけも外す（存在しないプリセットを指し続けないため）
- [x] P4-7 `DAO_Category.SelectAllOrderbyLastUser` の詰め替えに新列を追加（`Update` で紐づけが消えるのを防ぐ）
- [x] P4-8 ビルド確認
- [x] P4-9 実機動作確認（カテゴリ「DCS World」→ プリセット「デフォルト」の紐づけ保存を確認。自動適用の発火は未確認）

### Phase 5 — マージ後のフィードバック対応

PR #39 のマージ後、実際に使ってもらって出た指摘への対応。

- [x] P5-1 CP 一覧がスクロールできず全件見られない問題を修正（`MainWindow.xaml` の CP タブだけ `StackPanel` で包まれており、子に無限の高さが渡っていた）
- [x] P5-2 アプリから作成した報酬をアプリ上で削除できるように（確認ダイアログ付き）
- [x] P5-3 削除確認ダイアログで、影響するプリセット名を提示
- [x] P5-4 ビルド確認
- [ ] P5-5 実機動作確認

## 3. 実行ログ

| 日付 | タスクID | 内容 | 変更ファイル | コミット | 動作確認 |
|---|---|---|---|---|---|
| 2026-08-09 | P0-1〜3 | develop 最新化、feature ブランチ作成、本ドキュメント作成 | `docs/CHANNELPOINT-SYSTEM-TASKS.md` | — | 対象なし |
| 2026-08-09 | P1-1〜10 | 土台整備（Form/Service 追加）と既存バグ 3 件の修正 | `Forms/ChannelPointRewardForm.cs`(新), `Utility/ChannelPointService.cs`(新), `Utility/TwitchHelper.cs`, `Panels/ChannelPointPanel.xaml(.cs)`, `MainWindow.xaml.cs` | `3bc99c5` | `dotnet build` 成功（0 エラー／新規ファイルの警告なし）。**実機動作は未検証** |
| 2026-08-09 | P1.5-1〜5 | Phase 1 の実機確認で OAuth 認証直後のクラッシュが発覚し修正。合わせてユーザー名の手入力を廃止 | `MainWindow.xaml(.cs)`, `Utility/TwitchHelper.cs` | — | `dotnet build` 成功（0 エラー）。**実機動作は未検証** |
| 2026-08-09 | P2-1〜8 | 報酬コピー機能と API エラー理由の伝播 | `Utility/TwitchApiResult.cs`(新), `Utility/ChannelPointService.cs`, `Utility/TwitchHelper.cs`, `Dao/DAO_Setting.cs`, `Forms/ChannelPointRewardForm.cs`, `Panels/ChannelPointPanel.xaml(.cs)` | `d687645` | `dotnet build` 成功（0 エラー）。**実機動作は未検証** |
| 2026-08-09 | P3-1〜8 | プリセット機能（モデル・マイグレーション・DAO・保存/適用・UI） | `Models/M_ChannelPoint.cs`(新), `Models/T_ChannelPointPreset*.cs`(新), `AppDbContext.cs`, `Migrations/`(新), `Dao/DAO_ChannelPoint*.cs`(新), `Forms/ChannelPointPresetForm.cs`(新), `Utility/ChannelPointService.cs`, `Panels/ChannelPointPanel.xaml(.cs)` | — | `dotnet build` 成功（0 エラー）。**実機動作は未検証** |
| 2026-08-09 | P4-1〜8 | カテゴリ紐づけと自動適用 | `Forms/CategoryForm.cs`, `Panels/CategoryPanel.xaml(.cs)`, `Panels/PlayingGamePanel.xaml.cs`, `MainWindow.xaml.cs`, `Dao/DAO_Category.cs`, `Dao/DAO_ChannelPointPreset.cs` | `ae8789e` | `dotnet build` 成功（0 エラー）。**実機動作は未検証** |
| 2026-08-09 | P1-11 | 実機起動による確認（DB をバックアップのうえ実施） | — | — | **成功**: 異常終了なし（前回は exit 82 で NRE）。マイグレーション `channel_point_preset` 適用済み（既存データ保持）。`M_Setting.UserName=xiphelier` がトークンから自動設定され、ユーザー名入力の廃止が機能。`M_ChannelPoint` に 29 件キャッシュ＝一覧取得が成功。**トグル・コピー・プリセット・カテゴリ連動は未検証** |
| 2026-08-09 | P1-12 | 実機で「操作可能 0 件」となったため、判定失敗との区別が付くようステータス表示を改善 | `Utility/ChannelPointService.cs`, `Panels/ChannelPointPanel.xaml.cs` | `0dbcef4` | `dotnet build` 成功（0 エラー）。**実機動作は未検証** |
| 2026-08-09 | — | PR #39 を develop へマージ | — | `9e00999` | — |
| 2026-08-09 | P5-1〜4 | 一覧スクロール不能の修正と、報酬削除機能の追加 | `MainWindow.xaml`, `Panels/ChannelPointPanel.xaml(.cs)`, `Forms/ChannelPointRewardForm.cs`, `Utility/ChannelPointService.cs`, `Dao/DAO_ChannelPointPreset.cs` | `fcfaed3` | `dotnet build` 成功（0 エラー） |
| 2026-08-09 | P2-9 / P3-9 / P4-9 | 実機での主要機能の確認（DB の変化から確認） | — | — | **成功**: 異常終了なし。報酬 29→32 件（コピー 3 件成功、`'` 付きの名前で作成され `IsManageable=1`）。プリセット 2 件保存（「デフォルト」全 OFF / 「デフォルト2」2 件 ON）。適用回数 4 回 / 2 回を記録。カテゴリ「DCS World」への紐づけを保存。**未確認: 一覧のスクロール、報酬削除、カテゴリ切替による自動適用の発火** |

## 4. 課題・保留

| # | 内容 | 状態 |
|---|---|---|
| 1 | 報酬タイトルの接尾辞 `'`（シングルクオート）は **Twitch に受け付けられることを実機で確認**（「ものを投げる'」等が作成できた） | **解決** |
| 2 | コピー後の元報酬（操作不可）はアプリから削除できないため、Web UI での無効化/削除をユーザーに案内する必要がある | 設計に反映済み |
| 3 | `DAO_GamePlaylist.InsertUpdate(header, items)` に既存アイテム削除の未実装バグがある（本改修の対象外だが、プリセット DAO で同じ実装を真似しないこと） | 別課題として放置 |
| 4 | `only_manageable_rewards=true` の GET に失敗した場合、操作可否が判定できない。安全側に倒して**全件を操作不可**として表示する仕様にした（誤操作で 403 を踏むより良いと判断） | 仕様として確定 |
| 5 | 新規作成フォームは名前・コストのみのまま。Prompt・背景色・クールダウン等はコピー機能では引き継ぐが、手動作成時の入力欄は未実装 | 未対応（優先度低） |
| 6 | `SettingName.UserName` は表示と Twitch ダッシュボード URL 用にのみ使う値になった（認証の前提条件ではなくなり、アクセストークンから毎回上書きされる） | 仕様変更として確定 |
| 7 | `TwitchHelper.GetBroadcasterIdAsync(userName)` は起動経路からは呼ばれなくなったが、FriendPanel / ChatPanel が他ユーザーの情報取得に使っているため残す | 確認済み・残置 |
| 8 | プリセットの内訳リストは表示専用。ON/OFF の編集は報酬一覧側で行い「上書き保存」する運用にした（同じ状態を 2 箇所で編集できると食い違うため） | 仕様として確定 |
| 9 | `M_ChannelPoint`（報酬キャッシュ）は現状プリセット内訳の「削除済み」判定の補助のみで、実質は報酬一覧の取得結果から判定している。将来オフライン表示が必要になったら活用する | 用途限定 |
| 10 | プリセット適用は差分のある報酬だけ PATCH する。報酬数が多い場合の Twitch レート制限は未検証 | 未検証 |
| 11 | `dotnet-ef` 9.0.9 をグローバルツールとして導入した（マイグレーション生成に必要） | 環境構築として実施 |
| 12 | 実機で 29 件すべてが「操作不可」と判定された件は、**判定ロジックの問題ではなく実際に全件が Web 画面から作成されていたため**と確定。コピーで作成した 3 件は正しく `IsManageable=1` となった | **解決** |
| 13 | 操作可能な報酬が 0 件の間はプリセットを 1 つも保存できない（保存対象が操作可能な報酬のみのため）。まずコピーで操作可能な報酬を作る必要がある。ステータス表示でその旨を案内している | 仕様どおり |
| 14 | 報酬の削除は操作可能（✔）な報酬のみ。🔒 の報酬は Twitch の Web 画面から削除してもらう（API 仕様上アプリからは削除できない） | 仕様として確定 |
| 15 | 報酬を削除してもプリセットの項目は残る（「削除済み」表示となり適用時にスキップ）。プリセット側から自動で除去はしない | 仕様として確定 |
| 16 | `Setting` / `AppLog` タブも `StackPanel` で包まれたまま。今回スクロール不具合が出たのは CP タブだけだが、同じ構造なので将来内容が増えると同様の問題が起きうる | 本改修の対象外 |
