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
