# JTSA Extension

Extension は JTSA 本体とは別にビルド・配布します。JTSA 本体はこのフォルダの
プロジェクトを参照しないため、通常の JTSA publish に Extension は含まれません。

## タイマーの配置

1. `dotnet build extensions/JTSA.TimerPlugin/JTSA.TimerPlugin.csproj -c Release`
2. 出力フォルダの `JTSA.TimerPlugin.dll` と `plugin.json` を、JTSA の実行ファイル横にある
   `Plugins/Timer/` へコピーします。
3. JTSA の「Extension」タブで「再読み込み」を押します。

プラグイン固有の依存 DLL がある場合は、同じサブフォルダへ配置してください。

JTSAはExtensionを一時フォルダへシャドウコピーしてから読み込みます。そのためJTSAの起動中でも
`Plugins` 内のDLLと関連ファイルを上書きでき、「再読み込み」で新しいバージョンへ切り替えられます。
再読み込み時には、開いている対象Extensionのウィンドウはいったん閉じます。

## ルーレットの配置

1. `dotnet build extensions/JTSA.RoulettePlugin/JTSA.RoulettePlugin.csproj -c Release`
2. 出力フォルダの `JTSA.RoulettePlugin.dll` と `plugin.json` を、JTSA の実行ファイル横にある
   `Plugins/Roulette/` へコピーします。
3. JTSA の「Extension」タブで「再読み込み」を押します。

## カレンダー画像化の配置

1. `dotnet build extensions/JTSA.CalendarImagePlugin/JTSA.CalendarImagePlugin.csproj -c Release`
2. 出力フォルダの `JTSA.CalendarImagePlugin.dll` と `plugin.json` を、JTSA の実行ファイル横にある
   `Plugins/CalendarImage/` へコピーします。
3. JTSA の「Extension」タブで「再読み込み」を押します。

表示週を選び、JTSAに登録済みの予定を週ごとのカレンダー画像としてプレビューできます。
「PNGで保存」から配信告知などに使える画像を書き出せます。

## 外部プラットフォーム連携の配置

1. `dotnet build extensions/JTSA.MultiPlatformPlugin/JTSA.MultiPlatformPlugin.csproj -c Release`
2. 出力フォルダの `JTSA.MultiPlatformPlugin.dll` と `plugin.json` を、JTSA の実行ファイル横にある
   `Plugins/MultiPlatform/` へコピーします。
3. JTSA の「Extension」タブで「再読み込み」を押します。

JTSAで編集中のタイトルを取り込み、YouTubeとKickへタイトル・カテゴリを反映できます。
YouTubeは `youtube` または `youtube.force-ssl`、Kickは `channel:write` 権限を持つ
ユーザーアクセストークンを起動中だけ入力します。トークンは設定ファイルへ保存しません。

コメント取得を開始すると、YouTubeまたはKickのコメントがJTSA本体のチャット欄へ流れます。
YouTubeは公式Live Chat APIを利用します。Kickはデスクトップだけで受信できる公開Webチャット接続を
利用する簡易対応のため、Kick側の仕様変更時には更新が必要になる場合があります。

## 配信拡張への描画

どの Extension からでも、`IJtsaPluginContext` の共通APIを使って既存の
`http://localhost:8026/expansion` に描画を重ねられます。OBS側に別のブラウザソースを
追加する必要はありません。

```csharp
context.SetExpansionOverlay(new ExpansionOverlayContent(
    Id: "status",
    Html: "<div style=\"color:white;font-size:48px\">表示内容</div>",
    X: 100,
    Y: 100,
    Width: 600,
    Height: 120));
```

同じプラグインIDと描画IDで再度呼ぶと内容・位置・サイズが更新されます。表示を消す場合と
プラグイン終了時には、必ず次を呼んでください。

```csharp
context.RemoveExpansionOverlay("status");
```

描画IDはプラグインIDごとに分離されるため、別のExtensionと同じ名前を使っても衝突しません。

## Desktop Wall 連携

JTSAの予定をDesktopWallManagerへ共有するExtensionを追加しました。
導入手順・切断時の保存動作は [Desktop Wall 連携](JTSA.DesktopWallPlugin/README.md) を参照してください。
JTSA連携に対応したDesktopWallManagerが必要です。
