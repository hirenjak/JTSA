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

## nizima LIVE の配置

この Extension は **ExpansionTriggered を公開している JTSA ホストとセット** です。
対応していない古いホストでは読み込み時に実行時例外になります。

1. `dotnet build extensions/JTSA.NizimaLivePlugin/JTSA.NizimaLivePlugin.csproj -c Release`
2. 出力の `JTSA.NizimaLivePlugin.dll` と `plugin.json` を `Plugins/NizimaLive/` へコピーします。
3. JTSA の「プラグイン」タブで「再読み込み」を押し、「開く」で設定画面を出します。
4. nizima LIVE のプラグインマネージャーでポート（既定 22022）を合わせ、**JTSA を有効化**します。
   接続成功だけでは API は使えません。有効化後にモデル操作が通ります。

ウィンドウを閉じても接続は維持されます。再読み込み時に切断します。
トークンは `%AppData%\JTSA\Plugins\jtsa.nizimalive\settings.json` に保存されます。
プラグイン名は `JTSA` 固定です。nizima 側で削除すると InvalidToken となり再登録します。

## ともぞっちの配置

tomozow_streaming_tool のともぞっち（tamagotchi-twitch）のゲージ機能を移したものです。
ゲージは配信拡張に表示され、チャネポ交換で増減・追加・削除できます。

1. `dotnet build extensions/JTSA.TomozotchiPlugin/JTSA.TomozotchiPlugin.csproj -c Release`
2. 出力の `JTSA.TomozotchiPlugin.dll` と `plugin.json` を `Plugins/Tomozotchi/` へコピーします。
3. JTSA の「プラグイン」タブで「再読み込み」を押し、「開く」で設定画面を出します。
4. 「ゲージ」タブの「ファイルから読み込み」で、tamagotchi-twitch の `game_config.json` を選ぶと
   ゲージとチャネポ割り当てをそのまま移せます。

設定は `%AppData%\JTSA\Plugins\jtsa.tomozotchi\settings.json`、プリセットは同じフォルダの
`presets\` に保存されます。ゲージの値は保存せず、JTSA を起動するたびに初期値から始まります。
チャネポ状況（交換可能・クールダウン・TODO）の表示は未対応です。

## ルーレットの配置

1. `dotnet build extensions/JTSA.RoulettePlugin/JTSA.RoulettePlugin.csproj -c Release`
2. 出力フォルダの `JTSA.RoulettePlugin.dll` と `plugin.json` を、JTSA の実行ファイル横にある
   `Plugins/Roulette/` へコピーします。
3. JTSA の「Extension」タブで「再読み込み」を押します。

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
