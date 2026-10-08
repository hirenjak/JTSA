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

## nizima LIVE の配置

この Extension は **`IJtsaExpansionTriggerPluginContext`（ExpansionTriggered）を公開している JTSA ホストとセット** です。
対応していない古いホストでは読み込み時に実行時例外になります。

1. `dotnet build extensions/JTSA.NizimaLivePlugin/JTSA.NizimaLivePlugin.csproj -c Release`
2. 出力の `JTSA.NizimaLivePlugin.dll` と `plugin.json` を `Plugins/NizimaLive/` へコピーします。
3. JTSA の「プラグイン」タブで「再読み込み」を押し、「開く」で設定画面を出します。
4. nizima LIVE のプラグインマネージャーでポート（既定 22022）を合わせ、**JTSA を有効化**します。
   接続成功だけでは API は使えません。有効化後にモデル操作が通ります。

ウィンドウを閉じても接続は維持されます。再読み込み時に切断します。
トークンは `%LocalAppData%\JTSA\UserData\PluginData\jtsa.nizimalive\settings.json` に保存されます。
プラグイン名は `JTSA` 固定です。nizima 側で削除すると InvalidToken となり再登録します。

## ともぞっちの配置

tomozow_streaming_tool のともぞっち（tamagotchi-twitch）のゲージ機能を移したものです。
ゲージは配信拡張に表示され、チャネポ交換で増減・追加・削除できます。

1. `dotnet build extensions/JTSA.TomozotchiPlugin/JTSA.TomozotchiPlugin.csproj -c Release`
2. 出力の `JTSA.TomozotchiPlugin.dll` と `plugin.json` を `Plugins/Tomozotchi/` へコピーします。
3. JTSA の「プラグイン」タブで「再読み込み」を押し、「開く」で設定画面を出します。
4. 「ゲージ」タブの「ファイルから読み込み」で、tamagotchi-twitch の `game_config.json` を選ぶと
   ゲージとチャネポ割り当てをそのまま移せます。

設定は `%LocalAppData%\JTSA\UserData\PluginData\jtsa.tomozotchi\settings.json`、プリセットは同じフォルダの
`presets\` に保存されます。ゲージの値は保存せず、JTSA を起動するたびに初期値から始まります。

「チャネポ状況」タブでリワードごとに TODO・クールダウン・交換可能をチェックすると、元アプリと同じく
ゲージの下に交換可能アイコン、「⌛️クールダウン中 リワード名 4m05s」、TODO（☑ リワード名）を表示します。
`game_config.json` の `todoRewardIds` / `cooldownRewardIds` / `redeemableRewardIds` もそのまま読み込みます。

- TODO は交換イベントから作るので、配布版の JTSA でも動きます。「完了」「キャンセル」で一覧から消せます。
- クールダウンと交換可能、JTSA で作ったリワードの TODO の Twitch 側への完了・キャンセル（ポイント返却）は、
  `IJtsaChannelPointStatusPluginContext` に対応した JTSA が必要です（[hirenjak/JTSA#49](https://github.com/hirenjak/JTSA/pull/49)）。
  対応していない JTSA では、この部分は表示されません。状況は 10 秒ごとに取得します。

## カレンダー画像化の配置

1. `dotnet build extensions/JTSA.CalendarImagePlugin/JTSA.CalendarImagePlugin.csproj -c Release`
2. 出力フォルダの `JTSA.CalendarImagePlugin.dll` と `plugin.json` を、JTSA の実行ファイル横にある
   `Plugins/CalendarImage/` へコピーします。
3. JTSA の「Extension」タブで「再読み込み」を押します。

表示週を選び、JTSAに登録済みの予定を週ごとのカレンダー画像としてプレビューできます。
「PNGで保存」から配信告知などに使える画像を書き出せます。

## 外部プラットフォーム連携

外部プラットフォーム連携プラグインは `JTSA_pulugins_multiplatform` リポジトリで管理します。ビルドと配置方法はそちらの README を参照してください。
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
