# Twitch配信アクティビティ履歴：現状調査と実装方針

調査日：2026-09-04。対象は現在の作業ツリー（先行するクラッシュ・トークン同期修正を含む）。本調査ではアプリ・DBを変更していない。

**調査後の決定：Activity Feed読取は採用しない。** WebView2によるDOM取得・非公開GraphQLでの補完は実装対象外とする。今後は公式EventSubの受信、DBへの明細保存・重複排除、既存の取りこぼし対策を中心に進める。JTSAが受信・保存していない起動前イベントは復元対象外。以下のDOM補完案は検討時の記録として残す。

## 結論

推奨順序は、既存の取りこぼし対策 → イベント明細の永続化 → サブスク系のEventSub化 → Activity FeedのDOM実証 → 補完取り込み。

EventSubとDOM補完の併用は設計可能。ただしDOMの表示範囲・日時精度・安定IDが不明な現段階で「起動前の全件復元」を約束できない。DBを正本とし、元データ、推定値、照合未確定を区別する。

## 1. 現行の取得・保存・表示

| 対象 | 現行経路 | 保存内容 |
|---|---|---|
| 受け取ったレイド | `TwitchEventSubService.OnChannelRaid` → `StreamSupportTracker.AddRaid` | 表示名単位の人数合計 |
| Bits / Cheer | `OnChannelCheer` → `AddBits` | 表示名単位のBits合計。匿名は同じ表示名へ集約 |
| 新規サブスク・再サブスク通知 | `TwitchChatService` のIRC `OnNewSubscriber` / `OnReSubscriber` → `AddSubscription` | 表示名単位のTier・累計月数の最大値 |
| サブギフト | IRC `OnGiftedSubscription` → `AddGiftSubscription` | 送信者名＋Tier単位で1件ずつ加算 |
| レイド先候補 | `GetStreamingFollowUserAsync` → Helix Get Followed Streams → `RaidPanel.RaidUserList` | 現在配信中のフォロー先一覧。受信レイド履歴とは別 |

主要箇所：

- `JTSA/Utility/TwitchEventSubService.cs:338`：受信レイド。送信元UserId、メッセージID・時刻は集計に渡していない。
- `JTSA/Utility/TwitchChatService.cs:218`：サブスク系IRC処理。コミュニティギフト一括通知の購読は見当たらない。個別通知を前提とした数量集計で、匿名・一括・重複通知は実例検証が必要。
- `JTSA/Utility/StreamSupportTracker.cs:304`：各変更で集計JSONを `M_Setting.StreamSupportSnapshot` に保存。配信IDごとの保存・再起動復元はすでにあるが、個々のイベント履歴ではない。
- `JTSA/Panels/RaidPanel.xaml.cs:136`：ChangedをDispatcher経由で受け、ObservableCollectionを全件入れ替える。
- `JTSA/AppDbContext.cs`：`T_StreamHistory` は存在。`T_StreamEvent` は未実装。

再起動復元できるのは「JTSAが以前に受信・保存した集計」。JTSAが一度も受信していない起動前イベントを取得する処理はない。

## 2. 取りこぼし・誤集計の候補

### A. レイド先候補：ページネーション未実装（コード上で確認）

`JTSA/Utility/TwitchHelper.cs:161` は `GetFollowedStreamsAsync(broadcasterId)` を1回だけ呼ぶ。`Pagination.Cursor` を見ず、`after` も渡さない。

利用中TwitchLibのメソッド既定値をアセンブリから確認すると `first=100`。公式APIも既定100・最大100であり、「20件で切れる」とは限らない。100件を超える場合は残りが欠落する。公式はページ取得中の順位変動による重複・欠落も明記している。[Get Followed Streams](https://dev.twitch.tv/docs/api/reference/#get-followed-streams)

修正方針：`first:100` を明示し、返されたcursorを次のafterへ渡す。空cursorで終了、同一cursor再出現・キャンセル・ページ取得失敗を検知する。取得後UserIdで重複排除。異常終了時は「取得不完全」として扱い、黙って全件成功にしない。

`RaidPanel` の `DistinctBy(UserId)` は同一チャンネルの重複除去、`OrderByDescending(StartedAt)` は並べ替え。取得経路に件数を削る `Take` / `Where` は見当たらない。ObservableCollectionにも全件を追加している。

別の問題として、取得Taskをアカウント単位でなく共用し、完了時の対象アカウント照合がない。Aの取得中にBへ切り替えるとAの結果を表示し得る。カテゴリ画像の逐次awaitでも表示が遅れる。アカウント・更新世代チェックとキャンセルを追加し、カテゴリ取得を失敗しても候補行自体は表示する設計がよい。

### B. 受信レイド：履歴APIのページネーションではない

受信レイドは単発の `channel.raid` 通知から取得している。起動前・初回購読完了前・通常の通信断からの再購読中は欠落する。通常の切断期間のイベントにリプレイはない。一方、Twitch指定のセッション移行は購読を引き継ぐ別経路。[WebSocketの仕様](https://dev.twitch.tv/docs/eventsub/handling-websocket-events/)

追加の実装上の候補：

1. **購読の失敗が連鎖する。** チャネポ → follow → raid が同じtry内の逐次処理。前段が権限不足等で失敗するとraidまで到達しない。Bits等の購読だけ成立して接続が維持されると、raid未購読のままになる可能性がある。種類ごとに失敗を分離し、購読状態・取消通知・再試行を管理する。
2. **受信先は選択アカウントだけ。** `ChatPanel.InitializeCoreAsync` が切替時に旧EventSubを破棄する。非選択アカウントを同時収集している構成ではない。
3. **配信IDがグローバル。** `TargetAccountComboBox_SelectionChanged` は新アカウント接続後に `UpdateStreamStatusAsync` を呼ぶ。`ChatPanel` はそれより先に旧 `CurrentStreamId` で `StartStream` するため、切替直後の通知を旧配信へ集計し得る。
4. **オフラインと取得失敗を区別していない。** `GetCurrentStreamAsync` は通信失敗もnull。呼出側はnullをオフラインとして配信ID・集計をクリアする。IDが空の期間はSaveSnapshotが何もしないため、その間に受けたイベントは後で失われ得る。Online / Offline / Unknownを分ける必要がある。
5. **集計はイベント識別できない。** 同じ表示名の複数レイドは人数合計になる。再配送された通知にも再加算する。保存例外は握りつぶすので、メモリ上だけ反映され再起動で失われるケースもある。
6. **保存より先にUIログを同期呼出しする。** OnChannelRaid冒頭のLogSuccessはDispatcher.Invoke。UI待ちで保存も遅れる。DBへの受領保存をUI更新・演出から分離する。

前ターンで対応したトークン同期漏れも再購読失敗の原因候補だったが、上記はその修正後も残る。

## 3. 公式APIで補える範囲

現在のHelixリファレンスに、Creator Dashboard Activity Feed全履歴を返す公開エンドポイントは見当たらない。Get Broadcaster Subscriptionsは現在の加入者一覧、Get Bits Leaderboardは期間別集計であり、各発生時刻を持つ今回配信のイベント履歴には置き換えられない。[Helix Reference](https://dev.twitch.tv/docs/api/reference/)

今後のリアルタイム正本は次のEventSubを推奨：

| 種別 | 購読 | 注意 |
|---|---|---|
| レイド | `channel.raid` | 受信側条件はto_broadcaster_user_id |
| Cheer | `channel.cheer` | bits:read、匿名UserIdはnullとして保持 |
| 新規サブスク | `channel.subscribe` | 再サブスクを含まない。ギフト受取通知を通常購入として二重加算しない |
| サブギフト | `channel.subscription.gift` | 今回件数はtotal。cumulative_totalは累積値なので使わない |
| 再サブスク共有 | `channel.subscription.message` | ユーザーが共有した通知。すべての自動更新決済履歴ではない |

サブスク系には `channel:read:subscriptions` が必要で、現在の `RequestDeviceCodeAsync` のscopeにはない。移行時は追加認可が必要。IRCの集計を残したままEventSubでも加算すると二重になるため、IRCは表示用途へ分離する。[EventSub Subscription Types](https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/)

## 4. Activity Feed DOMの実査状況

利用可能なブラウザで `https://dashboard.twitch.tv/` を開いたが、Twitchログイン画面だった。認証済みActivity Feedに到達していないため、以下は**未検証**：

- 実際の行ルートと一意なセレクタ、イベントID属性
- 種別・ユーザー・人数・Tier・ギフト数・BitsのDOM表現
- `time[datetime]`、title、aria-label等に絶対時刻があるか
- 無限スクロール、仮想リスト、保持期間、最大件数
- スクロールで今回配信の開始時刻まで遡れるか

具体的な `data-a-target` 名は推測で実装しない。Twitchの新しいChat & Eventsパネルもあり、アカウント・レイアウト差を確認する。[Twitch Chat & Events](https://help.twitch.tv/s/article/creator-chat-and-events?language=en_US)

実証時は各種イベントの行DOMだけを採取する。data-a-target → role/aria → 表示テキスト＋親子構造の順で候補を検証する。これらも公式の安定契約ではない。日英表示、匿名、同名・同額連続イベント、まとまったギフト、空フィード、フィルタ適用時を比較する。IDがDOMの単なる描画キーなら永続キーにしない。

## 5. WebView2読取の設計

`ActivityFeedImportWindow` にWebView2を置き、読取を `ActivityFeedDomReader`、正規化を `ActivityFeedParser` に分離。`IActivityFeedReader` 経由にすればブラウザ方式を差し替えられる。

既存の `TwitchNotificationBrowserService` は専用Chrome＋PlaywrightでDashboardの通知文を入力しており、WebView2は未導入。ウィンドウ組込という目的にはWebView2が合うが、既存Chromeのログイン状態をそのまま使えるわけではない。ログイン・Runtime配布・アカウント切替を検証する。ブラウザ認証とHelix OAuthは別管理とし、CookieやTokenを取り出して流用しない。

読取手順案：

1. 対象BroadcasterIdと配信区間を確定。UIのログイン先・表示中チャンネルが対象と一致するか確認。
2. NavigationCompletedだけで完了扱いにせず、フィード行または明示的な空状態、ローディング終了を有限時間待つ。SPAの後続更新は短いポーリングまたはMutationObserverで検知。
3. 表示中の行を抽出し、その都度C#側へ退避。仮想リストなら画面外の行がDOMから消えるため、最後に一括抽出する方式は使わない。
4. ページ全体ではなく実際のフィードスクロール領域を少しずつ送る。前後に重なりを残し、新規行と最古時刻を追跡。
5. 配信開始以前への到達、明示的末尾、キャンセル、時間・件数上限、複数回の無進展で停止する。「無進展」は全件取得の証明ではなく停止理由として保存。
6. 候補件数、最古・最新時刻、時刻不明件数、重複候補、取得終了理由を表示し、確認後に取り込む。

ExecuteScriptAsyncは戻り値をJSON化するので、JS側でオブジェクト配列を返せばよい。提示例のJSON.stringifyを残すとJSON文字列が二重エンコードされる。[Microsoftの説明](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/javascript)

```csharp
// rowSelectorは実DOMで検証した行セレクタ。現時点では未確定。
var selectorJson = JsonSerializer.Serialize(rowSelector);
var json = await webView.CoreWebView2.ExecuteScriptAsync($$"""
    (() => Array.from(document.querySelectorAll({{selectorJson}}))
        .map(row => ({
            text: row.innerText,
            datetime: row.querySelector('time[datetime]')?.getAttribute('datetime') ?? null
        })))()
    """);
// C#側でDTOへデシリアライズし、null・件数・文字列長・型を検証する。
```

これは返却形式だけの案で、実セレクタや実データ抽出の動作確認済みコードではない。DOMにないユーザーID・絶対時刻・Tierを推測で埋めない。

WebView2は専用ユーザーデータ領域を使う。ExecuteScript前後の正確なoriginとアカウントを確認し、ナビゲーション変更で読取をキャンセル。ログイン画面には抽出スクリプトを実行しない。汎用HostObjectを公開せず、DBアクセスや任意コード実行をWeb側へ渡さない。RawJsonは抽出行データのみでCookie・localStorage・ページ全体を保存しない。[WebView2 security](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)

## 6. DBと重複排除

ユーザー案のT_StreamEventを基礎とし、次を追加・調整する。

| 項目 | 方針 |
|---|---|
| BroadcasterId | 必須。表示中アカウントとは独立に、イベント受信元から決定 |
| StreamId | 未確定ならnullable。配信者＋発生時刻とT_StreamHistoryから後で紐付け |
| UserId / UserLogin / UserName | ID優先、名前はスナップショット。匿名はフラグ＋null ID |
| EventType | Raid / Cheer / Subscription / ResubscriptionMessage / SubscriptionGiftなどを区別 |
| Amount / GiftCount / Tier | 不明はnull。人数・Bits・件数の意味を種別ごとに固定 |
| OccurredAt | UTC、nullable。通知送信時刻と真の発生時刻を混同しない |
| ReceivedAt / ObservedAt | EventSub受信時刻・DOM読取時刻を別途保存 |
| TimeSource / TimePrecision | event timestamp / message timestamp / DOM absolute / relative / unknown |
| OccurredAtFrom / OccurredAtTo | 相対表示しかない場合の推定区間。単一の正確な時刻を捏造しない |
| ExternalEventId / Source | ソース内での識別子。EventSubのsubscription.idは購読IDでありイベントIDではない |
| IsAnonymous / CumulativeMonths | 匿名と再サブスク表示を保持 |
| RawJson / ParserVersion | 正規化再実行に必要な最小データ |

同一EventSub通知の再配送は `metadata.message_id` で排除する。これはActivity FeedのIDと共通である保証はない。[EventSubの重複通知](https://dev.twitch.tv/docs/eventsub/)

一つのイベントが両ソースから観測されるため、推奨は次の2層：

- `T_StreamEvent`：表示・集計の対象になる正規化イベント。
- `T_StreamEventObservation`：Source、ExternalEventId、RawJson、ObservedAt、照合状態、正規化イベントへの参照。非空ExternalEventIdについて `(BroadcasterId, Source, ExternalEventId)` の一意制約。

ソース間は BroadcasterId、種別、UserId（なければlogin）、金額/人数/件数、Tier、時刻区間で候補照合。ハッシュは候補検索の補助にし、曖昧なハッシュだけを一意制約にしない。同じ人が同じ分に同額Cheerを2回行うケースを潰すため。相対時刻は読取ごとに揺れるのでハッシュ材料にそのまま使わない。

1対1に確定できない候補は両方の観測を保存し、「照合待ち」として合計へ自動加算しない。匿名イベントは名前で同一人物判定しない。ギフトのまとめ行と個別受取行は同じ粒度ではなく、単純な1対1照合・加算は不可。代表イベントと受取詳細の関連を持たせるか、今回ギフト合計には送信者側のtotalだけを採用する。

EventSubの購読開始・切断・復旧時刻を `T_StreamEventCoverage` 等に種別別で保存する。DOM補完の候補区間は配信開始～最初の購読完了と、記録された切断区間。ただし取得範囲には照合用の重なりを設ける。時刻が不明な行は今回配信へ自動確定しない。

旧集計JSONから個別イベントを復元してはならない。既存集計はLegacySnapshotとして別に保持し、対象配信を新明細方式へ切り替えるときの基準を定める。既存合計＋同じ時間帯の新明細を無条件に足すと重複する。

## 7. 自然な組込位置と実装順序

```text
EventSub受信 ──→ StreamEventRecorder ──→ SQLite明細・観測記録
                                       ↑
ActivityFeedImportWindow → DOM読取 → 正規化 → 照合・確定
                                       ↓
                            DB集計 → RaidPanel / プレースホルダー
```

1. `GetStreamingFollowUserAsync` の全ページ取得、アカウント切替応答ガード、配信状態Unknownの分離、EventSub購読の失敗分離を先行する。
2. `T_StreamEvent` / Observation / Coverage、DAO、EF migrationを追加。EventSub受信元のBroadcasterIdとメッセージIDを保存し、UIを待たず受領を確定する。保存失敗は可視化・再試行し、無制限メモリキューや黙った破棄にしない。
3. サブスク系をEventSubへ追加し、IRCとの二重集計を解消。配信者別の収集コンテキストを持つ。選択中だけ集めるか全登録アカウントを集めるかを仕様として明示する。
4. `RaidPanel` に「履歴を補完」を置く。WebView2は専用Window。`StreamSupportTracker` はDBから既存Formを作る集計層に変更し、ChangedをUI更新の通知として利用する。
5. 補完は演出・チャット送信・OBS操作を再発火させない。DB保存後、新しいリアルタイムイベントだけを既存 `StreamExpansionService` へ渡す。副作用の厳密な一度だけ実行まで必要ならoutbox管理を別途設ける。
6. ログイン済み実DOMの実証結果が揃ってからセレクタ・日時・照合条件を固定し、補完機能を有効化する。

必要な検証：101件以上のレイド先候補、取得途中の失敗と切替、EventSubの重複message_id、先行購読失敗後のraid成功、DB障害と再起動、配信ID未確定、複数アカウント同時通知、相対日時の日付跨ぎ、同名同額連続イベント、匿名、一括ギフト、仮想リスト、DOM変更、期限到達前の読取停止、再取り込みの冪等性、補完で演出が発火しないこと。

## 8. 非公開API・利用規約

内部GraphQLのエンドポイント・操作名・レスポンスは本調査では検証しておらず、採用しない。利用したとしても非公開スキーマ・persisted query変更、Web認証依存、追加検証への追従が必要になり、Helix用Tokenで使える保証もない。

DOM読取も規約上の自動抽出に該当し得る。Twitch利用規約にはdata mining等の抽出手段や自動アクセスを制限する条項があるため、「ログイン済みの自分の画面だから許可済み」とは判断できない。製品搭載・配布前にTwitchの書面許諾または適用可能な許可条件を確認する。これはDOMの技術的実現性とは別の採用条件。[Twitch Terms of Service](https://legal.twitch.com/en/legal/terms-of-service/)

完全性を優先する別案は、今後の配信について常時稼働のEventSub受信サービスで保存し、JTSA起動時に同期する構成。これも収集開始以前の過去イベントは復元できないが、JTSA停止中の観測を継続できる。

今回の成果はコード調査・公式資料の確認・設計文書。実Twitchへの購読変更、DB migration、WebView2実装、ログイン後DOM抽出、長時間実機テストは実施していない。
