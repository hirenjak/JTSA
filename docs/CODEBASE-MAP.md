# コードベースマップ: JTSA

> このドキュメントは `/analyze-codebase` により自動生成された。
> 生成日時: 2026-08-09 14:26 / 対象コミット: 7b2ceaa
> コードとの乖離が疑われる場合は再生成すること。

## 1. 概要

JTSA は Twitch 配信者向けの配信支援デスクトップアプリ。配信タイトル・カテゴリの設定を履歴から流用して素早く行うことを中核機能とし、加えてチャット表示（透過オーバーレイ含む）、チャンネルポイント管理、レイド実行、X（Twitter）告知、OBS 用プレイリストオーバーレイ配信などの機能を持つ。個人開発（緋蓮じゃく氏）の趣味プロジェクトであり、単一の WPF プロジェクトで構成される。

- **主要言語 / フレームワーク**: C# (.NET 8, WPF) / EF Core 9 + SQLite / TwitchLib / NAudio / Velopack
- **エントリポイント**: `JTSA/App.xaml.cs` — `Main()` で Velopack 初期化後に WPF アプリを起動
- **実行方法**: `dotnet run --project JTSA`（配布は Velopack インストーラ経由）
- **テスト**: なし（テストプロジェクトは存在しない）

## 2. アーキテクチャ

WPF のコードビハインド中心の構成で、MVVM は採用していない。画面は `MainWindow` に 9 枚の `UserControl`（`Panels/`）を静的に配置する形で、各パネルは `(MainWindow)Application.Current.MainWindow` で直接メインウィンドウを取得して相互作用する。つまり **MainWindow がハブ（事実上のグローバル状態置き場）** であり、「現在のタイトル」「現在のカテゴリ」といったアプリ状態は ViewModel ではなく MainWindow 上の TextBlock/TextBox コントロールそのものに保持され、`CurrentTitleText` などのプロパティがそのラッパーになっている。

データの流れは大きく 3 系統ある。

1. **配信設定系（中核）**: UI 操作 → `MainWindow` / 各 Panel → `TwitchHelper`（Twitch Helix API）→ 成功したら `Dao/` 経由で SQLite に履歴保存 → `Reload〜()` メソッドで `ObservableCollection<〜Form>` を作り直して画面反映。
2. **チャット系（受信イベント駆動）**: `TwitchChatService`（IRC 匿名接続）と `TwitchEventSubService`（EventSub WebSocket）がイベントを発火 → `ChatPanel` が Dispatcher 経由で受けて `TwitchChatForm` に変換・通知音再生 → リストと `ChatOverlayWindow` に反映。
3. **OBS 連携系**: `PlayingGamePanel` が `ObsHttpServer`（localhost:8026 の HttpListener）を起動し、OBS のブラウザソースが `/obs` の HTML を読み、その HTML が 500ms ごとに `/data` の JSON をポーリングしてプレイリスト状態を描画する。

認証は Twitch の **Device Code Grant フロー**。リフレッシュトークンを SQLite（`M_Setting`）に永続化し、起動時と 3 時間ごとのタイマーでアクセストークンを再取得する。アクセストークン自体はメモリ（`TwitchHelper.AccessToken`、実体は TwitchLib の設定）にのみ置く。

### 主要な設計判断

- **DB アクセスは静的 DAO + 都度 DbContext 生成** — 各 `DAO_*` メソッドが `using var db = new AppDbContext()` で短命コンテキストを開閉する。長寿命コンテキストの追跡問題を避けるためと思われるが、エンティティを手動で詰め替えて返す冗長なコードが多い。
- **UI コントロール＝状態ストア** — MainWindow のプロパティ群（`CurrentTitleText` 等）は TextBlock の Text を直接読み書きする。バインディングより単純だが、UI スレッド以外から触れない・値の変更にセッターの副作用（Steam URL 取得など）が連鎖する、という暗黙の前提を生んでいる（コードからの推測）。
- **静的ヘルパーによるグローバル状態** — `TwitchHelper`（AccessToken / BroadcasterId / ClientID）、`JTSAHelper`（LoginName）が静的クラスで、アプリ全体がここに依存する。ClientID はソースにハードコードされている。
- **ログは AppLogPanel が一手に担う** — `ProcessStart`/`ProcessEnd`/`Success`/`Error` という独自の運用ログ規約があり、ほぼ全処理がこれで囲まれる。ファイル出力はなく画面内リスト＋ステータスバー表示のみ。
- **自動更新は Velopack + GitHub Releases** — `App.OnStartup` で更新確認し、ユーザー同意後に適用・再起動する。

## 3. ディレクトリツリー

```
JTSA/
├── .github/workflows/
│   └── jekyll-gh-pages.yml      # GitHub Pages（Jekyll）デプロイ。アプリのCI/CDではない
├── JTSA.sln
├── README.md                    # 配布物向けの説明（インストール・操作手順）
└── JTSA/                        # 唯一のプロジェクト
    ├── App.xaml / App.xaml.cs   # エントリポイント・Velopack更新確認
    ├── AppDbContext.cs          # EF Core DbContext（SQLite）
    ├── MainWindow.xaml / .cs    # メイン画面＝アプリのハブ
    ├── ChatOverlayWindow.xaml / .cs  # クリック貫通の透過チャットオーバーレイ
    ├── Win32Helper.cs           # ウィンドウ位置制御用 P/Invoke 群
    ├── Dao/                     # テーブル別の静的DAO（7ファイル）
    ├── Models/                  # EFエンティティ（M_=マスタ, T_=トランザクション）
    ├── Migrations/              # EF Core マイグレーション（起動時に自動適用）
    ├── Panels/                  # 機能別 UserControl（9画面 + カテゴリ検索）
    ├── Forms/                   # 画面バインド用DTO（〜Form クラス群）
    ├── TwitchIF/                # Twitch APIレスポンスの自前DTO
    ├── Utility/                 # Twitch/IGDB/Steam連携・OBSサーバ等の非UIロジック
    ├── Properties/ Resources/   # リソース（通知音 wav 等）
    └── FodyWeavers.xml          # Costura.Fody（アセンブリ埋め込み）設定
```

### ディレクトリの役割

| パス | 役割 |
|---|---|
| `JTSA/Dao/` | SQLite への CRUD。1テーブル1クラス、全メソッド静的 |
| `JTSA/Models/` | EF エンティティ。`DBBase`（監査日時列）を全員が継承 |
| `JTSA/Panels/` | 機能単位の UserControl。MainWindow に直接参照される |
| `JTSA/Forms/` | ListBox 等にバインドする表示用 DTO。DB エンティティとは別物 |
| `JTSA/TwitchIF/` | TwitchLib の型をアプリ内に持ち込まないための変換先 DTO |
| `JTSA/Utility/` | 外部サービス（Twitch/IGDB/Steam/OBS）との境界と汎用処理 |
| `JTSA/Migrations/` | 自動生成物。手で編集しない |

## 4. 主要ファイル詳細

### `JTSA/App.xaml.cs`

**役割**: エントリポイント。Velopack の初期化と GitHub Releases からの更新確認・適用。

**設計思想**: `StartupUri` に頼らず `Main()` を自前定義しているのは、WPF 初期化より前に `VelopackApp.Build().Run()` を実行する必要があるため（Velopack の要件）。csproj の `GenerateApplicationDefinition=false` と `StartupObject` 指定はこのための構成。

**変更時の注意**: `App.xaml` を ApplicationDefinition に戻すと `Main` が二重定義になりビルドが壊れる。

### `JTSA/AppDbContext.cs`

**役割**: SQLite（`%AppData%\Roaming\JTSA\userdata\JTSA.db`）への EF Core コンテキスト。

**設計思想**: 接続設定を `OnConfiguring` に埋め込み、DI なしでどこからでも `new AppDbContext()` できるようにしている。DB パスは静的フィールド `dbDirectory` に保持され、MainWindow の「DBフォルダを開く」機能が参照する。複合キーは `T_GamePlaylistItem`（GamePlayListId + CategoryId）のみ `OnModelCreating` で定義。

**主要な要素**:
- `DbSet` 8 つ — `T_TitleText`, `M_Category`, `M_User`, `M_Setting`, `M_TitleTag`, `T_GamePlaylistHeader`, `T_GamePlaylistItem`, `T_ChatUser`

**変更時の注意**: このファイルの日本語コメントは文字化けしている（エンコーディング事故の痕跡）。エンティティを変えたら `dotnet ef migrations add` が必須。マイグレーションは MainWindow のコンストラクタで `db.Database.Migrate()` により自動適用される。

### `JTSA/MainWindow.xaml.cs`

**役割**: アプリのハブ。起動シーケンス（DB マイグレーション → 認証 → 配信者情報取得 → 各パネル初期化）、タイトル/カテゴリの編集・送信、OAuth 認証、X 告知を担う。

**設計思想**: `CurrentTitleText` / `CurrentCategoryId` などのプロパティは XAML 上の TextBlock を直接読み書きするラッパーで、これがアプリの「現在の配信設定」の唯一の実体。`CurrentCategoryId` のセッターは Steam URL 取得（`SteamUrlTextSet` → IgdbService）という非同期副作用を持つ。タイトル内の `${friend}` プレースホルダは表示時に FriendPanel の選択状態で置換される（Twitch 用は `@ユーザーID`、X 用は表示名の読点区切り、と置換規則が 2 種類ある）。

**主要な要素**:
- `MainWindow_LoadedAsync` — 起動シーケンス本体。失敗段階に応じて `LoadSubPanel`（認証 UI）を出し分ける
- `ResetAccessTokenAsync` — リフレッシュトークンからの再取得＋新トークンの保存
- `SendTitleButton_Click` — タイトルを HttpClient 直叩き（PATCH /helix/channels）、カテゴリを TwitchLib 経由で送信し、成功時に履歴（T_TitleText）へ保存
- `OAuthButton_Click` — Device Code フロー実行
- `InsertTextAtCaret` — TitleTagSidePanel からのタグ挿入用に公開

**依存関係**: ほぼ全パネル・全 DAO・TwitchHelper / IgdbService / JTSAHelper。逆に全パネルからも参照される（双方向依存）。

**変更時の注意**: `StreamerDataSet` は DB にカテゴリが無い場合 `dbCategoryData`（null）のプロパティに代入しており、初回起動などカテゴリ未登録の状態で NullReferenceException になり得る。タイトル送信はなぜか HttpClient 直叩きと TwitchLib が混在している点も把握しておくこと。

### `JTSA/Utility/TwitchHelper.cs`

**役割**: Twitch Helix API との境界。認証（Device Code / リフレッシュ）、配信情報・カテゴリの取得と設定、チャンネルポイント CRUD、チャット送信・ピン止め、レイド、絵文字パース。

**設計思想**: TwitchLib の `TwitchAPI` インスタンスを静的に 1 つ持ち、`AccessToken` プロパティはその設定への委譲。API レスポンスは TwitchLib の型のまま返さず `TwitchIF/` の自前 DTO に詰め替えるのが基本方針（ただしチャンネルポイントだけは TwitchLib の `CustomReward` を素通しで返しており一貫していない）。TwitchLib が対応していない API（チャットのピン止め）は HttpClient 直叩きで補う。ログ出力のために `Application.Current.MainWindow` へキャストして AppLogPanel を掴む、という UI への逆依存がある。

**主要な要素**:
- `RequestDeviceCodeAsync` / `PollDeviceTokenAsync` / `RefreshAccessTokenAsync` — 認証フロー。要求スコープはここにハードコード
- `GetTwitchStreamInfo` / `SetCategoryAsync` / `GetCategoryByGameId` / `SearchCategoriesByGameNameAsync` — 配信設定系
- `GetCustomRewardsAsync` / `UpdateCustomRewardAsync` / `CreateCustomRewardAsync` — チャンネルポイント
- `SendChat` / `PinedChat` / `PinedDeleteChat` / `GetPinedChat` — チャット
- `CreateParts(ChatMessage)` — メッセージをテキストと絵文字画像 URL のパーツ列に分解（表示は `TwitchMessageTextBlock` が担当）

**変更時の注意**: 多くのメソッドが例外を空 catch で握りつぶし null や `true` を返す。`SetCategoryAsync` は失敗しても常に `true` を返すため、呼び出し側の成功ログは信用できない。スコープを増やしたら再認証が必要。

### `JTSA/Panels/AppLogPanel.xaml.cs`

**役割**: アプリ内ログ表示パネル兼、全クラスが使うロガー。

**設計思想**: `ProcessStart`/`ProcessEnd` で処理の開始終了を対にして記録する運用ログ規約の実装。ログ追加時に MainWindow のステータスバー（`StatusTextBlock`）も同時に更新するため、「最後のログ＝ステータス表示」という関係が成り立つ。ファイル永続化はしない。

**変更時の注意**: `ClearLogButton_Click` は空実装。ロガーでありながら UserControl なので、UI 生成前・別スレッドからの呼び出しには弱い。

### `JTSA/Panels/ChatPanel.xaml.cs`

**役割**: チャット受信・表示・送信、通知音再生、チャットオーバーレイの起動管理。

**設計思想**: `Initialize()` は MainWindow の起動シーケンスから呼ばれる遅延初期化（認証完了後でないと接続できないため、コンストラクタでは接続しない）。起動時に `T_ChatUser` を全削除しており、「その配信回で初めてチャットした人」を検出して入室音（JoinChat.wav）と通常通知音（CommentNotification.wav）を鳴らし分ける、配信セッション単位の記録として使っている。チャットユーザーのプロフィール画像は `M_User` にキャッシュされる。

**依存関係**: `TwitchChatService`（IRC 受信）、`TwitchEventSubService`（チャンネルポイント通知）、`ChatOverlayWindow`、DAO_User / DAO_ChatUser / DAO_Setting。

**変更時の注意**: 受信イベントは非 UI スレッドで来るため必ず `Dispatcher.InvokeAsync` を経由している。`ChatAddAsync` はチャット 1 件ごとにピン止めチャットを API 取得し直す実装で、高頻度チャットでは負荷になり得る。

### `JTSA/Utility/TwitchChatService.cs` / `JTSA/Utility/TwitchEventSubService.cs`

**役割**: 前者は TwitchLib.Client による IRC チャット受信（匿名接続・閲覧専用）、後者は EventSub WebSocket でチャンネルポイント消化イベントを購読するサービス。

**設計思想**: どちらもイベント（`MessageReceived` / `ChannelPointRedeemed`）を発火するだけで UI を知らない、このコードベースでは数少ない疎結合な層。EventSubService は TwitchLib の要件により内部で `ServiceCollection` を組み立てて DI コンテナを自前構築している。Twitch 起因の再接続（購読引き継ぎあり）と通信断による再接続（要再購読）を区別して処理する。

**変更時の注意**: 購読イベントを増やす場合は `SubscribeChannelPointsAsync` に倣い、SessionId 確立後に購読する必要がある。エラーは `Debug.WriteLine` のみで AppLogPanel に出ない。

### `JTSA/ChatOverlayWindow.xaml.cs`

**役割**: ゲーム画面等の上に重ねる透過チャットオーバーレイウィンドウ。

**設計思想**: Win32 拡張スタイル（`WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW`）を P/Invoke で直接設定してクリック貫通を実現する。設定モード（ドラッグ移動可）と貫通モードをトグルでき、位置は `M_Setting` に保存、復元時にモニタ構成変更で画面外に出ていないか検証する。ChatPanel の `TwitchChatFormList`（新着先頭）を `CollectionChanged` で監視し、自前のリスト（新着末尾）に並べ替えて自動スクロールする。

**変更時の注意**: 同種の P/Invoke が `Win32Helper.cs` にもあるが共有されていない（このファイル内に重複定義）。

### `JTSA/Panels/PlayingGamePanel.xaml.cs`

**役割**: ゲームプレイリスト（複数ゲームの消化状況）の管理と、OBS オーバーレイ用 HTTP サーバのホスト。

**設計思想**: プレイリストの ID に Unix ミリ秒タイムスタンプを使う（連番の代わり）。アイテムの状態は左クリックで「完了」、右クリックで「プレイ中」をトグルし、「プレイ中」にすると MainWindow の現在カテゴリにも反映するという UI 規約。OBS 表示用の HTML/JSON 生成器（`CreateObsHtml` / `CreateObsJson`）をこのパネルが直接持ち、コンストラクタで `ObsHttpServer` を起動する。JSON 生成は非 UI スレッドから呼ばれるため `Dispatcher.Invoke` で UI 値を読む。

**依存関係**: DAO_GamePlaylist / DAO_Category、TwitchHelper、ObsHttpServer。CategoryPanel / CategorySearchPanel からアイテム追加される。

**変更時の注意**: OBS 側の見た目を変える場合は `CreateObsHtml` 内の埋め込み HTML/CSS/JS を編集する（外部ファイルではない）。

### `JTSA/Utility/ObsHttpServer.cs`

**役割**: `http://localhost:8026/` で `/obs`（HTML）と `/data`（JSON）を返すだけの極小 HTTP サーバ。

**設計思想**: コンテンツ生成を `Func<string>` 2 本の注入で受け取り、サーバ自体は配信内容を知らない。OBS のブラウザソースから使われる前提。

**変更時の注意**: ポート 8026 固定。多重起動すると HttpListener が例外を投げる。

### `JTSA/Utility/IgdbService.cs` / `JTSA/Utility/SteamHelper.cs`

**役割**: カテゴリ（ゲーム）を Steam のストアページ・ヘッダー画像に対応付けるための外部 API 連携。IgdbService は Twitch カテゴリ ID → IGDB ID → Steam URL の 2 段解決、SteamHelper は Steam の appdetails API からヘッダー画像 URL を取得。

**設計思想**: IGDB は Twitch 傘下のため Twitch の ClientID/AccessToken をそのまま流用できる、という関係を利用している。`IgdbService.Initialize` で依存を注入する準静的シングルトン。取得した Steam 情報は `M_Category` にキャッシュされ、プレイリストのサムネイル（BoxArt より横長で見栄えの良いヘッダー画像）に使われる。

**変更時の注意**: `Initialize` が MainWindow の起動シーケンスでしか呼ばれないため、認証前に `GetSteamUrlsAsync` を呼ぶと null 参照になる。

### `JTSA/Dao/`（共通パターン）

**役割**: テーブルごとの CRUD。`DAO_Setting` は enum `SettingName` をキーにした Key-Value ストアとして特殊。

**設計思想**: 全メソッド静的・都度コンテキスト生成・例外処理なし（呼び出し側の AddSwitchLog で成否ログのみ）。`Update` 系は `CreatedDateTime` を既存レコードから引き継いでから上書きする規約。`UpdateLastUse(d)` で「最終使用日時＋選択回数」を更新し、一覧は基本 `LastUsedDateTime` 降順で返す——「よく使うものが上に来る」というアプリのコンセプトを DAO 層が支えている。

**変更時の注意**: `DAO_GamePlaylist.InsertUpdate(header, items)` 内に結果を捨てている `Where(...)` 行があり、既存アイテムの削除を意図した未完成コードと思われる。このメソッド経由の更新はアイテム重複の可能性があるため注意（現状の主経路は `InsertItemList` / `UpdatePlaylistItemStatus` 側）。

### `JTSA/Utility/TwitchMessageTextBlock.cs`

**役割**: チャットメッセージ（テキスト＋Twitch 絵文字画像の混在）を表示するカスタム RichTextBox（クラス名は `TwitchMessageRichTextBox`）。

**設計思想**: `MessageParts` 添付プロパティに `TwitchHelper.CreateParts` の結果をバインドすると FlowDocument を組み立てる。絵文字画像は静的 `ConcurrentDictionary` + `Lazy<Task<byte[]>>` で URL 単位にキャッシュし、GIF は XamlAnimatedGif でアニメーション再生する。

## 5. その他のファイル

### ルート / `JTSA/`
- `JTSA.sln` — 単一プロジェクトのソリューション
- `JTSA/JTSA.csproj` — net8.0-windows / WPF。Velopack・Costura.Fody（単一 exe 化）・TwitchLib（preview 版）等を参照
- `JTSA/Win32Helper.cs` — ウィンドウ位置取得・移動系 P/Invoke。`SetAppWindowRect(AppInfoForm)` の呼び出し元が現存せず、旧「アプリ配置」機能の残骸と思われる
- `JTSA/GlobalSuppressions.cs`, `AssemblyInfo.cs`, `FodyWeavers.xml` — ビルド周辺の定型物

### `JTSA/Models/`
- `DBBase.cs` — 全エンティティ共通の監査列（LastUsed/Created/UpdatedDateTime）
- `DBBaseTransaction.cs` — 上記＋SelectedCount / SortNumber（使用頻度ソート用）
- `M_Category.cs` — カテゴリキャッシュ（Steam URL・ヘッダー画像 URL 含む）
- `M_User.cs` — フレンド兼チャットユーザーのプロフィールキャッシュ（画像は Base64 保存の場合あり）
- `M_Setting.cs` — enum キーの設定 KV
- `M_TitleTag.cs` / `T_TitleText.cs` — タイトル用タグ・タイトル履歴
- `T_GamePlayListHeader.cs` / `T_GamePlayListItem.cs` — プレイリスト（ヘッダ＋複合キーのアイテム）
- `T_ChatUser.cs` — 配信セッション中のチャット参加者記録（起動時全削除）

### `JTSA/Panels/`（上記以外）
- `CategoryPanel.xaml(.cs)` — 使用履歴カテゴリの一覧・選択・Steam URL 編集
- `CategorySearchPanel.xaml(.cs)` — Twitch カテゴリ検索（1 秒デバウンス付き）。ダブルクリックで DB 登録＋プレイリスト追加。CategoryPanel と PlayingGamePanel の両 XAML に埋め込まれている
- `ChannelPointPanel.xaml(.cs)` — チャンネルポイント報酬の一覧（列ソート付き）・有効/一時停止トグル・新規作成
- `FriendPanel.xaml(.cs)` — コラボ相手の登録と選択。選択結果がタイトルの `${friend}` 置換に使われる
- `RaidPanel.xaml(.cs)` — フォロー中の配信中チャンネル一覧、ダブルクリックでレイド実行
- `SettingPanel.xaml(.cs)` — 再 OAuth 認証
- `TitleTagSidePanel.xaml(.cs)` — 定型タグをクリックでタイトル編集欄のカーソル位置に挿入

### `JTSA/Forms/`
画面バインド用 DTO 群（`CategoryForm`, `TitleTextForm`, `FriendForm`, `TwitchChatForm`, `ChannelPointForm`, `PlaylistHeaderForm`, `PlaylistItemForm`(INPC 実装), `RaidUserForm`, `AppLogForm`, `AppInfoForm`, `TitleTagForm`, `EditTitleTextForm`）。`EditTitleTextForm` と `AppInfoForm` は現在有効な呼び出し元が見当たらない。

### `JTSA/TwitchIF/`
Twitch API レスポンスの自前 DTO（`TwitchUserIF`, `TwitchStreamIF`, `TwitchCategoryIF`, `TwitchModifyChannelInformationIF`, `AccessTokenResponseIF`, `DeviceCodeResponseIF`）。OAuth 系のみ JSON プロパティ名に合わせた snake_case/camelCase。

### その他
- `.github/workflows/jekyll-gh-pages.yml` — GitHub Pages 用。アプリのビルド CI ではない
- `JTSA/Resources/*.wav` — チャット通知音・入室音

## 6. 横断的な規約

- **命名規則**: DB は `M_`（マスタ）/ `T_`（トランザクション）接頭辞、DAO は `DAO_<テーブル名>`、表示 DTO は `<機能>Form`、API DTO は `<名前>IF`。タイポがそのまま定着している識別子がある（`ProfielImageUrl`, `CopyClipBoad`, `isnertData` 等）ので、grep 時は正しい綴りで探さないこと。
- **エラー処理**: 例外は境界（TwitchHelper 等)で空 catch → null / 空リスト返却が基本。呼び出し側は `AppLogPanel.AddSwitchLog(bool, ...)` で成否をログに残す。例外がユーザーに見えることはほぼない。
- **設定の与え方**: ユーザー設定はすべて SQLite の `M_Setting`（enum キー）。ClientID・ポート番号（8026）・OAuth スコープはソースにハードコード。設定ファイル・環境変数は使わない。
- **ログ**: `AppLogPanel.ProcessStart` → 処理 → `ProcessEnd` で囲む。クラス名は `GetType().Name` か `nameof()` で渡す。
- **UI 更新**: 一覧は毎回 `ObservableCollection.Clear()` → 全件 Add で作り直す（差分更新はしない）。非 UI スレッドからは必ず Dispatcher 経由。
- **言語**: コメント・ログ・UI 文言はすべて日本語。

## 7. 注意点・未整理の領域

- **null 安全の穴**: `MainWindow.StreamerDataSet` は未登録カテゴリで null 参照になる経路がある。`ResetAccessTokenAsync` は非 null 宣言の `Task<string>` で null を返す。`TwitchHelper.SetCategoryAsync` は失敗しても true を返す。この 3 つは動作確認の際に踏みやすい。
- **未使用・残骸コード**: `Win32Helper.SetAppWindowRect` / `AppInfoForm` / `EditTitleTextForm` は呼び出し元が見当たらない（旧機能の残骸と推測）。`AppLogPanel.ClearLogButton_Click` は空。csproj が参照する `obs-websocket-dotnet` を使うコードも見当たらない（OBS 連携は自前 HTTP サーバ方式に移行済みと思われる）。
- **未実装 TODO**: フレンド検索（FriendPanel）とタイトルタグ検索（TitleTagSidePanel）の TextChanged がプレースホルダのまま。
- **エンコーディング混在**: `AppDbContext.cs` のコメントが文字化けしている。ファイルによって BOM 有無・エンコーディングが揺れている可能性があるため、一括置換ツールの使用時は注意。
- **DAO_GamePlaylist.InsertUpdate の未完成疑い**: 既存アイテム削除を意図したと思われる無効な行があり、ヘッダ＋アイテム同時更新経路はアイテムが重複し得る。
- **テスト・CI なし**: 自動テストは存在せず、GitHub Actions は Pages デプロイのみ。リファクタリング時のセーフティネットは手動確認だけである。
- **秘密情報**: Twitch ClientID はソース内にハードコード（Device Code フローのためシークレットは無いが、変更時は `TwitchHelper.ClientID` の一箇所)。
