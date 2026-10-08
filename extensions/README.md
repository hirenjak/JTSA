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

### ローカルの画像・動画・音声を表示する

`IJtsaExpansionMediaPluginContext` に対応したホストでは、ファイルを登録して配信拡張から参照できる URL を受け取れます。
登録したファイルだけが `/expansion-media` から配信されます。

```csharp
if (context is IJtsaExpansionMediaPluginContext media)
{
    var url = media.GetExpansionMediaUrl(@"C:\effects\tanuki.gif");
    context.SetExpansionOverlay(new ExpansionOverlayContent(
        "fx", $"<img src=\"{url}\" style=\"width:100%\">", 0, 0, 400, 300));
}
```

描画 HTML は innerHTML で差し込まれるためスクリプトは動きません。`<video>`・`<audio>` の音量は
`data-jtsa-volume="0〜100"` 属性で指定できます。CSS アニメーションは `data-jtsa-animation-start="UNIX ミリ秒"`
を付けると、再描画やページ再読み込みのあとも開始時刻に合わせて再生されます。

## 配信拡張イベントの受け取り

`IJtsaExpansionTriggerPluginContext` に対応したホストでは、配信拡張のすべてのイベント（チャット・フォロー・
レイド・サブスク・Bits・広告・定時など）を、ルールの一致判定より前に受け取れます。

```csharp
if (context is IJtsaExpansionTriggerPluginContext triggers)
    triggers.ExpansionTriggered += info => context.Log($"{info.TriggerType}: {info.Value}");
```

`TriggerType` は配信拡張のトリガー種別名（`Chat`、`Follow`、`Raid` など）、`Value` は種別ごとの値
（チャット本文、ユーザー名、Bits 数など）です。プラグインの例外は本体の配信拡張を止めません。
古いホストで動かすプラグインは、型を直接参照せずリフレクションで購読してください。

## チャンネルポイントの利用状況

`IJtsaChannelPointStatusPluginContext` に対応したホストでは、報酬のクールダウン・交換できるか・未処理の交換
（TODO）を扱えます。呼ぶたびに Twitch API へ問い合わせるため、定期取得は数秒以上の間隔を空けてください。

```csharp
if (context is IJtsaChannelPointStatusPluginContext points)
{
    var now = DateTimeOffset.UtcNow;
    foreach (var reward in await points.GetChannelPointRewardStatusesAsync())
    {
        if (reward.IsCoolingDown(now))
            context.Log($"↺ {reward.Title} {(reward.CooldownExpiresAt!.Value - now).TotalMinutes:0.0}min");
        else if (reward.IsRedeemable(now))
            context.Log($"交換可能: {reward.Title}");
    }

    // 未処理の交換（TODO）。JTSA が作成した報酬（IsManageable）のみ取得・完了できます。
    foreach (var todo in await points.GetUnfulfilledRedemptionsAsync(rewardId))
        context.Log($"□ {todo.RewardTitle} ({todo.UserName})");
    await points.CompleteRedemptionAsync(rewardId, redemptionId, fulfilled: true);
}
```

`ChannelPointRedeemed` の `ChannelPointRedemptionInfo.RedemptionId` には交換 ID が入ります（古いホストでは空）。
Twitch の Web 画面など JTSA 以外で作った報酬は、交換イベントで受け取った分を TODO として自前で持ってください。

## Desktop Wall 連携

JTSAの予定をDesktopWallManagerへ共有するExtensionを追加しました。
導入手順・切断時の保存動作は [Desktop Wall 連携](JTSA.DesktopWallPlugin/README.md) を参照してください。
JTSA連携に対応したDesktopWallManagerが必要です。
