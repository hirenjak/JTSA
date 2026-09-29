# エージェント向け作業メモ

## メインアプリのリリースビルド・再起動

ユーザーからJTSA本体のリリースビルドを依頼された場合は、現在リリースビルドに使用している既存の出力フォルダへ再度ビルドし、更新したアプリを再起動する。

- 対象プロジェクト: `JTSA/JTSA.csproj`
- 作業前に、起動中のJTSAの実行ファイルパスや既存のビルド・発行設定を確認し、現在使用しているリリース出力先を特定する。新しい出力先を勝手に作ったり、既定の出力先だと推測したりしない。
- 既存のビルド・発行方式と設定を引き継ぎ、Release構成で同じフォルダを更新する。
- 起動中のアプリが更新を妨げる場合は、対象のJTSAを通常終了してから更新する。設定やユーザーデータ、プラグインは保持する。
- ビルド成功と出力先の更新を確認した後、そのフォルダのJTSA実行ファイルを起動し、起動できたことを確認する。起動中だった旧プロセスは終了させ、新しいビルドへ切り替える。
- ビルドに失敗した場合は成功扱いにせず、失敗内容とアプリの起動状態を報告する。

## 外部アプリプラグインのリリースビルド・ローカル配置

ユーザーから外部アプリプラグインのリリースビルド・配置を依頼された場合は、以下の手順を使う。
JTSA本体や他のプラグインの配置とは区別する。

- 対象プロジェクト: `extensions/JTSA.ExternalAppsPlugin/JTSA.ExternalAppsPlugin.csproj`
- リポジトリのルートで実行するビルドコマンド:
  ```powershell
  dotnet build extensions/JTSA.ExternalAppsPlugin/JTSA.ExternalAppsPlugin.csproj -c Release --no-restore
  ```
- 復元済みの依存関係がない場合は、`--no-restore` を外してビルドする。
- ビルド出力: `extensions/JTSA.ExternalAppsPlugin/bin/Release/net8.0-windows/`
- ユーザー指定のPluginsルート: `C:\Users\hiren\AppData\Local\JTSA\UserData\Plugins`
- 外部アプリプラグインの配置先: `C:\Users\hiren\AppData\Local\JTSA\UserData\Plugins\ExternalApps`

ビルド成功後、出力フォルダ内のファイルを、サブフォルダ構成を保って配置先へ上書きコピーする。
`JTSA.ExternalAppsPlugin.dll`、`plugin.json`、依存DLL、`runtimes` 以下なども含める。
配置先の設定ファイルや他のプラグインは削除しない。コピーした各ファイルのハッシュを比較し、配置を検証する。
配置先がワークスペース外のため書き込み権限が必要な場合は、所定の権限昇格を使う。

JTSAはプラグインをシャドウコピーして読み込むため、起動中でも配置ファイルを更新できる。
配置完了後は、JTSAの「Extension」→「再読み込み」で反映できることをユーザーに案内する。
再読み込み時は、開いている対象プラグインのウィンドウが閉じる。

- 2026-09-27 Desktop Wallの直近予定から月制限を外す変更: extensions/JTSA.DesktopWallPluginのCalendarRequestに任意UpcomingFrom、CalendarResponseにIsUpcomingを追加。直近要求では現在以降の全将来範囲から開始順に最大3件を返す。通常の月要求との互換性を維持し、DesktopWallManager側の通信定義も同時変更済み。ソース編集のみ、ビルド・配置・実機確認は未実施。
