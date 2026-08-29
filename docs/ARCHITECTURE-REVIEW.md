# JTSA 構成レビュー指摘事項

> 作成日: 2026-08-29 / 対象: develop ブランチ (コミット 9695424)
> リポジトリ全体（プロジェクト構成・ソース・CI/CD・テスト・ドキュメント）を対象としたレビュー。
> 重要度: **高** = 実害が出うる/出ている、**中** = 品質・保守性に効く、**低** = 気になったら直す

---

## 1. セキュリティ

### 1-1. Twitch リフレッシュトークンが SQLite に平文保存されている（重要度: 高）

- 対象: `JTSA/Models/M_TwitchAccount.cs`（`RefreshToken` 列）、`JTSA/Dao/DAO_Setting.cs`（`SettingName.RefreshToken`）
- `%AppData%\JTSA\userdata\JTSA.db` を読めれば、誰でも配信者アカウントのリフレッシュトークンを取得できる。リフレッシュトークンから長期間有効なアクセストークンが再発行できるため、DB ファイルの持ち出し＝アカウント操作権限の漏洩になる。
- **推奨**: Windows 専用アプリなので DPAPI（`ProtectedData.Protect` / `Unprotect`、`DataProtectionScope.CurrentUser`）で暗号化してから保存する。DAO の入出力箇所（`DAO_TwitchAccount.InsertUpdate` / `UpdateRefreshToken` / `Select*`）に変換を挟むだけで済み、スキーマ変更も不要（既存の平文値は復号失敗時に平文とみなす移行処理を1バージョン挟む）。

### 1-2. OBS WebSocket パスワードも平文保存（重要度: 中）

- 対象: `DAO_Setting.SettingName.ObsWebSocketPassword` / `SubObsWebSocketPassword`
- 影響範囲はローカル OBS のみだがトークンと同様に DPAPI 暗号化の対象に含めるのが望ましい。

### 1-3. ClientID のハードコード（重要度: 情報・対応不要）

- 対象: [TwitchHelper.cs:420](../JTSA/Utility/TwitchHelper.cs)
- Device Code Grant の公開クライアントでは ClientID は秘密情報ではないため、ハードコード自体は問題ない。ただし「秘密ではない」ことを示すコメントを付けておくと、後から見た人が慌てない。

---

## 2. アーキテクチャ

### 2-1. MainWindow が神クラス化している（重要度: 高）

- 対象: `JTSA/MainWindow.xaml.cs`（**2,143 行**）
- タイトル/カテゴリ状態の保持、Twitch 認証・トークン更新タイマ、OBS 2系統（メイン/サブ）の接続管理、配信状態ポーリング、ウィンドウ位置復元、シーン/ソース切替ボタン生成…と責務が集中している。
- さらに `(MainWindow)Application.Current.MainWindow` によるグローバル参照が **13 ファイル・36 箇所** あり、パネル・ヘルパーすべてが MainWindow に直結している。
- **推奨**（段階的に）:
  1. OBS 接続管理（`mainObsController` / `subObsController` + ロック + 状態フラグ一式）を `ObsConnectionManager` のようなクラスに切り出す。メイン/サブでほぼ同じ処理が二重に書かれているのも同時に解消できる。
  2. トークン管理（更新タイマ・アカウント切替）を `TwitchAuthManager` に切り出す。
  3. 新規機能からは `(MainWindow)Application.Current.MainWindow` を増やさず、イベント/コールバック渡しにする。

### 2-2. UI コントロールが状態ストアになっている（重要度: 中）

- 対象: `MainWindow.CurrentTitleText` 等（TextBlock.Text を直接読み書きするプロパティ群）
- UI スレッド以外から触れない・セッターに副作用が連鎖する、という暗黙の前提が生まれており、テストも書けない。フル MVVM 化は現実的でないが、「現在の配信設定」だけでも plain なステートクラスに持ち、UI へは反映のみとする形が安全。

### 2-3. 非 UI ロジックが UI に逆依存している（重要度: 中）

- 対象: [TwitchHelper.cs:31](../JTSA/Utility/TwitchHelper.cs)、`Utility/` 各所の `mainWindow.AppLogPanel.Error(...)`
- API ヘルパーがログ出力のために MainWindow → AppLogPanel を直接掴んでおり、`Utility/` → UI の逆依存になっている。これが TwitchHelper（1,164 行）をテスト不能にしている主因。
- また `TwitchHelper` の静的フィールド `mainWindow` は**静的コンストラクタ実行時点**の `Application.Current.MainWindow` を捕まえるため、初回タッチが早すぎると null になる時限爆弾でもある。
- **推奨**: `AppLog.Error(source, message)` のような静的ログ窓口を1つ作り、MainWindow 側が起動時にハンドラを登録する形に反転する。`Utility/` から `MainWindow` 参照を消せる。

---

## 3. 信頼性・エラー処理

### 3-1. オフライン起動のたびにエラーダイアログが出る（重要度: 中）

- 対象: [App.xaml.cs:156-159](../JTSA/App.xaml.cs)（`UpdateCheck` の catch）
- ネットワーク未接続や GitHub 障害時、起動のたびに「アップデート確認中にエラーが発生しました」の MessageBox が出て起動をブロックする。更新確認の失敗は起動を妨げるべきではない。
- **推奨**: 失敗はログ（クラッシュログと同じ場所）に落とすだけにして黙って起動を続行する。あわせて `OnStartup` で `await UpdateCheck()` してからウィンドウを出しているため、確認が遅いと起動体感も遅くなる。バックグラウンド実行（起動後に確認→見つかったら通知）への変更を検討。

### 3-2. ObsHttpServer の例外処理・停止手段がない（重要度: 中）

- 対象: [ObsHttpServer.cs](../JTSA/Utility/ObsHttpServer.cs)
  - `Process()` に try/catch がなく、provider（HTML/JSON 生成）が例外を投げると `Task.Run` 内の未観測例外になり、該当リクエストは応答なしで放置される。
  - `Stop()` / `Dispose()` が存在せず、`StartAsync` のループは `listener.Stop()` 相当の割り込みで `GetContextAsync` が例外を投げて終わる設計になっている。
  - ポート 8026 固定のため、他プロセスが使用中だと `listener.Start()` が例外を投げる。呼び出し側でユーザーに分かるメッセージになるか未保証。
  - このクラスだけ**名前空間宣言がない**（グローバル名前空間）。`JTSA.Utility` に入れるべき。
- **推奨**: `Process` 全体を try/catch で包み 500 を返す。`Stop()` を実装し、ポート競合時は AppLog に「ポート 8026 が使用中」と出す。

### 3-3. DB アクセスがすべて UI スレッドの同期呼び出し（重要度: 低〜中)

- 対象: `Dao/` 全般（`SaveChanges()` / 同期 LINQ）
- ローカル SQLite なので普段は問題ないが、ウイルス対策ソフトによる I/O 遅延やチャット集計系の書き込み頻度次第で UI が引っかかる。新規の高頻度パス（チャットユーザー集計など）だけでも `async` 版 DAO を検討。

---

## 4. データベース / EF Core

### 4-1. 日時がローカルタイムで保存されている（重要度: 中）

- 対象: リポジトリ全体で `DateTime.Now` が 86 箇所（DAO・モデル既定値含む）
- 集計（配信ごとのチャット数など）や履歴が PC のタイムゾーン変更・夏時間の影響を受ける。少なくとも DB 保存値は `DateTime.UtcNow` に統一し、表示時にローカル変換するのが安全。既存データとの互換を考えると新テーブル/新機能から適用でもよい。

### 4-2. マイグレーションの管理が不統一（重要度: 中）

- 対象: `JTSA/Migrations/`
  - scaffold 生成（`.Designer.cs` あり）と手書き（`20260810050000_StreamExpansion.cs` など `.Designer.cs` なし）が混在。手書き分はモデルスナップショットに反映されないため、以後の `dotnet ef migrations add` が差分を誤検出するリスクを常に抱える。
  - 命名も `table_name_change` / `user_add_colomn`（snake_case、**colomn は typo**）と `StreamExpansion`（PascalCase）が混在。
- **推奨**: 適用済みマイグレーションの ID は変更不可（ユーザー DB の `__EFMigrationsHistory` と不一致になる）ので放置でよいが、**今後は必ず `dotnet ef migrations add` で生成し、PascalCase に統一**する運用をドキュメント化する。

### 4-3. DAO の細かい非効率（重要度: 低）

- [DAO_Setting.cs:86-115](../JTSA/Dao/DAO_Setting.cs) `InsertUpdate`: `Count` → `Single` と 2 回クエリしている。`SingleOrDefault` 1 回で分岐でき、追跡済みエンティティへの `db.M_Setting.Update(existing)` 呼び出しも不要。
- `AppDbContext.dbDirectory` が public static な可変フィールド。`OnConfiguring` のたびに書き換わる設計で、読み取り側との整合が保証されない。プロパティ化＋設定は 1 箇所に。

---

## 5. 依存パッケージ

### 5-1. preview 版・0.0.x 版への依存が多い（重要度: 中）

- 対象: [JTSA.csproj](../JTSA/JTSA.csproj)

| パッケージ | バージョン | 懸念 |
|---|---|---|
| Microsoft.Extensions.DependencyInjection | 11.0.0-**preview** | .NET 11 世代の preview を net8.0 アプリに参照。安定版 8.x/9.x に下げるべき |
| TwitchLib.Api | 3.10.1-**preview** | EventSub 対応のため已むなしだが、更新時の破壊的変更に注意 |
| TwitchLib.EventSub.Websockets | 0.9.0-**preview** | 同上 |
| Velopack | 0.0.1298 | CI の vpk バージョン（deploy.yml）と番号を揃えて固定できているのは良い |

- 特に DI パッケージは EventSub 用の `ServiceCollection` にしか使っていないので、安定版へ即時ダウングレード可能なはず。

### 5-2. Costura.Fody と self-contained publish の併用（重要度: 低）

- Costura はアセンブリを exe に埋め込むツールだが、CI では `--self-contained` の Velopack パッケージングも行っている。両方が本当に必要か（Costura を外して起動時間・ビルド時間を比較）を一度確認する価値がある。

---

## 6. CI/CD

### 6-1. develop / PR に対する CI がない（重要度: 高）

- 対象: `.github/workflows/deploy.yml`（トリガーは `master` push と手動のみ）
- 日常の開発ブランチである develop へのコミットやフィーチャーブランチの PR ではビルドもテストも走らず、壊れた状態が master へのマージ時（＝リリース時）まで検出されない。
- **推奨**: `on: { push: { branches: [develop] }, pull_request: {} }` で `dotnet build` + `dotnet test` だけを行う軽量な `ci.yml` を追加する（`runs-on: windows-latest` が必要な点は deploy.yml と同じ）。

### 6-2. リリースのテスト実行は担保されている（良い点）

- deploy.yml はテスト → publish → vpk pack → GitHub Release の順で、`JTSA.db` の除外、`concurrency` による多重実行防止も設定済み。ここは良くできている。

---

## 7. テスト

### 7-1. 中核ロジックがテスト不能な構造（重要度: 中）

- 現状 12 クラス・約 49 テストがあるのは良いが、対象は純粋関数的な Utility（プレースホルダ置換、フォーマッタ等）と DAO に限られる。アプリの中核である `TwitchHelper` / 各 Panel / MainWindow のロジックは、静的クラス + UI 直依存（指摘 2-3）のためテストが書けない。
- **推奨**: 2-3 のログ窓口反転を先に行うと、`TwitchHelper` の API 呼び出し以外のロジック（レスポンス変換など）が切り出してテスト可能になる。

### 7-2. テスト用 DB 切替が静的プロパティ（重要度: 低）

- 対象: `AppDbContext.DatabasePathOverride`（static）と `JTSA.Tests/DaoTests.cs`
- xUnit はテストクラス単位で並列実行するため、将来 DB を使うテストクラスが 2 つ以上になると static な override を取り合って壊れる。DB を使うテストを 1 つの xUnit Collection にまとめる（`[Collection("Database")]`）と安全。

---

## 8. ドキュメント・リポジトリ衛生

### 8-1. README が開発者向けになっていない（重要度: 中）

- 対象: [README.md](../README.md)
- 内容が配布物同梱の readme.txt（インストール手順）そのもの。GitHub 上のトップとしては、ビルド方法（`dotnet build JTSA.sln`）、実行方法、マイグレーション追加手順、リリースフロー（master push → 自動リリース）といった開発者向け情報がどこにもない。
- **推奨**: README を開発者向けに書き直し、配布向け文面は `docs/DISTRIBUTION-README.txt` 等へ移動する。

### 8-2. docs/CODEBASE-MAP.md が古い（重要度: 中）

- 生成日 2026-08-09 時点の内容で、「テスト: なし（テストプロジェクトは存在しない）」「workflows は jekyll-gh-pages.yml」など現状と食い違う記述が多数。信じて読むと誤る状態なので、再生成するか冒頭に「stale」の注記を付ける。

### 8-3. 命名の typo・不統一（重要度: 低）

- `Panels/StereamExpansionPanel.xaml(.cs)` — **Steream** → Stream の typo。クラス名・ファイル名ともリネーム可能（マイグレーション ID と違い互換性の制約はない）。
- `Forms/` フォルダの中身は WinForms ではなく表示用 DTO（`〜Form` クラス）。WPF プロジェクトで "Form" は紛らわしいので、新規クラスからでも `〜ViewItem` / `〜Dto` 等への移行を検討。
- インデントがタブとスペースで混在（例: `MainWindow.xaml.cs`）。`.editorconfig` が実質 1 ルールしかないので、`indent_style` / `indent_size` / `charset` を定義して固定する。

### 8-4. ブランチの整理（重要度: 低)

- `feature/add-channel-point-manager`、`fix/category-panel` などマージ済みと思われるローカル/リモートブランチが残っている。定期的に削除するとリポジトリの見通しが良くなる。

---

## 対応優先度まとめ

| # | 指摘 | 重要度 | 工数感 |
|---|---|---|---|
| 1-1 | リフレッシュトークンの DPAPI 暗号化 | 高 | 小 |
| 6-1 | develop/PR 用 CI 追加 | 高 | 小 |
| 3-1 | オフライン時の起動エラーダイアログ抑止 | 中 | 小 |
| 5-1 | DI パッケージを安定版へ | 中 | 小 |
| 3-2 | ObsHttpServer の例外処理・Stop 実装 | 中 | 小 |
| 8-1 / 8-2 | README 書き直し・CODEBASE-MAP 再生成 | 中 | 小 |
| 2-1 | MainWindow の責務分割（OBS 管理から） | 高 | 大 |
| 2-3 | ログ窓口の反転（Utility→UI 依存の解消） | 中 | 中 |
| 4-1 | DB 保存日時の UTC 化 | 中 | 中 |
| 4-2 | マイグレーション運用ルールの明文化 | 中 | 小 |
| その他 | 低重要度項目 | 低 | — |

「工数感: 小」の上 6 件はそれぞれ独立して着手でき、リスクも低い。構造改善（2-1, 2-3）は新機能追加のついでに少しずつ進めるのが現実的。
