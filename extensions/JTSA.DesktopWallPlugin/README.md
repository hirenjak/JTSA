# Desktop Wall カレンダー連携

JTSAのExtensionとして読み込むと、DesktopWallManagerからJTSAの予定を閲覧できます。
同じPC・同じWindowsユーザーで両方のアプリを起動してください。
JTSAのDBやGoogleの認証情報は共有しません。予定の編集・逆方向同期は行いません。

## 導入

1. `dotnet build extensions/JTSA.DesktopWallPlugin/JTSA.DesktopWallPlugin.csproj -c Release`
2. 出力先の `JTSA.DesktopWallPlugin.dll` と `plugin.json` を、JTSA実行ファイル横の
   `Plugins/DesktopWall/` に配置します。Abstractions DLLはJTSA本体のものを使用します。
3. JTSAのExtensionタブで「再読み込み」。読込時に共有を開始します。
4. JTSA連携対応版のDesktopWallManagerでカレンダーパネルを選び、
   「JTSAの予定を表示（同じPC・閲覧のみ）」をオンにします。

表示月の予定を30秒ごと、月変更時、「更新」ボタンで取得します。Googleの予定と併記できます。
日付の印をクリックすると、開始時刻・解決済みタイトル・カテゴリを表示します。
JTSAに終了時刻はないため、開始日時のみの予定として扱います。
JTSA側のExtension画面では共有の開始・停止ができます。停止は次のExtension読込まで有効です。

## 接続が切れたとき

DesktopWallManagerは取得済みの月ごとのスナップショットを
`%LOCALAPPDATA%/DesktopWallManager/JtsaCalendar/yyyy-MM.json` に保存します。
接続が切れても最後に取得した予定を保持し、アプリの再起動後も復元します。
未接続時は「前回取得分」と最終更新日時を表示します。まだ取得したことがない月の予定は表示できません。
表示設定をオフにしても保存済みデータは消しません。
再接続時には、その月の全件を置き換えるため、JTSA側で削除した予定も反映されます。
空の月を正常取得した場合も空のスナップショットで更新します。
保存失敗時は画面に通知し、現在の表示と以前に保存できたファイルを保持します。

## 通信仕様

Windows名前付きパイプ `JTSA.Calendar.v1.<Windows SID>`。両端にCurrentUserOnlyを指定。
UTF-8 JSONの前に4バイトのlittle-endian本文長を付け、上限4MiB、要求処理は5秒でタイムアウト。
要求は `Version`, `Year`, `Month`、応答は `Version`, `Entries`, `Error`。
予定は `Id`, `Start`, `Title`, `Category`。StartはこのPCのローカル日時（オフセットなし）。
表示月のみを取得し、DBへの書込み用操作は公開しません。
`CalendarBridgeProtocol.cs` はDesktopWallManagerにも同一内容を配置します。

DesktopWallManagerのソース変更は隣接リポジトリにあります。
このJTSAリポジトリだけを配布しても、旧DesktopWallManagerでは受信できません。
