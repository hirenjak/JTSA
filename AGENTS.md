# エージェント向け作業メモ

## ビルド確認時の権限

Windowsサンドボックス内の `dotnet build` は、`C:\Users\hiren\AppData\Local\Microsoft SDKs` へのアクセス拒否（MSB4184）で失敗することがある。このエラーが出たらソースのビルド失敗と判断せず、同じビルドコマンドを所定の権限昇格で再実行して確認する。権限昇格に伴う承認はツールの手続きに従い、ユーザーへ毎回この既知の原因の説明や確認依頼を繰り返さない。権限昇格でも失敗した場合は実際のエラーを報告する。

## メインアプリのリリースビルド・再起動

ユーザーからJTSA本体のリリースビルドを依頼された場合は、現在リリースビルドに使用している既存の出力フォルダへ再度ビルドし、更新したアプリを再起動する。

- 対象プロジェクト: `JTSA/JTSA.csproj`
- 作業前に、起動中のJTSAの実行ファイルパスや既存のビルド・発行設定を確認し、現在使用しているリリース出力先を特定する。新しい出力先を勝手に作ったり、既定の出力先だと推測したりしない。
- 既存のビルド・発行方式と設定を引き継ぎ、Release構成で同じフォルダを更新する。
- 起動中のアプリが更新を妨げる場合は、対象のJTSAを通常終了してから更新する。設定やユーザーデータ、プラグインは保持する。
- ビルド成功と出力先の更新を確認した後、そのフォルダのJTSA実行ファイルを起動し、起動できたことを確認する。起動中だった旧プロセスは終了させ、新しいビルドへ切り替える。
- ビルドに失敗した場合は成功扱いにせず、失敗内容とアプリの起動状態を報告する。

## プラグインのリリースビルド・ローカル配置

プラグインのReleaseビルドを依頼された場合は、ビルド成功後に現在使用中のローカルPluginsフォルダへの配置まで行う。ソース変更のみを明示された場合は配置しない。
配置先は作業前に既存の `plugin.json` のIDとエントリDLLを照合して特定する。フォルダ名をプロジェクト名から推測しない（Rouletteプラグインの既存フォルダ名は `Roulet`）。
ビルド出力内のファイルをサブフォルダ構成を保って上書きコピーし、コピーした各ファイルのハッシュを比較して検証する。配置先の設定ファイルや他のプラグインは削除しない。
JTSAはプラグインをシャドウコピーして読み込むため、起動中でも配置ファイルを更新できる。配置完了後は、JTSAの「Extension」→「再読み込み」で反映できることをユーザーに案内する。再読み込み時は開いている対象プラグインのウィンドウが閉じる。

### 外部アプリプラグイン

外部アプリプラグインには以下のプロジェクトと配置先を使う。

- 対象プロジェクト: `extensions/JTSA.ExternalAppsPlugin/JTSA.ExternalAppsPlugin.csproj`
- リポジトリのルートで実行するビルドコマンド:
  ```powershell
  dotnet build extensions/JTSA.ExternalAppsPlugin/JTSA.ExternalAppsPlugin.csproj -c Release --no-restore
  ```
- 復元済みの依存関係がない場合は、`--no-restore` を外してビルドする。
- ビルド出力: `extensions/JTSA.ExternalAppsPlugin/bin/Release/net8.0-windows/`
- ユーザー指定のPluginsルート: `C:\Users\hiren\AppData\Local\JTSA\UserData\Plugins`
- 外部アプリプラグインの配置先: `C:\Users\hiren\AppData\Local\JTSA\UserData\Plugins\ExternalApps`

ビルド成功後、`JTSA.ExternalAppsPlugin.dll`、`plugin.json`、依存DLL、`runtimes` 以下なども含めて配置する。
配置先がワークスペース外のため書き込み権限が必要な場合は、所定の権限昇格を使う。

- 2026-09-27 Desktop Wallの直近予定から月制限を外す変更: extensions/JTSA.DesktopWallPluginのCalendarRequestに任意UpcomingFrom、CalendarResponseにIsUpcomingを追加。直近要求では現在以降の全将来範囲から開始順に最大3件を返す。通常の月要求との互換性を維持し、DesktopWallManager側の通信定義も同時変更済み。2026-09-29にプラグインをReleaseビルド・ローカル配置済み。実機確認は未実施。
