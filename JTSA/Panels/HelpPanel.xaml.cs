using System.Collections.Generic;
using System.Windows.Controls;

namespace JTSA.Panels;

public partial class HelpPanel : UserControl
{
    public IReadOnlyList<HelpTopic> Topics { get; } =
    [
        
        new(
            "OBSとの連携について",
            "OBS側の設定とJTSAからのOBS接続設定",
            "OBS WebSocketへの接続と、JTSAの表示をOBSへ取り込むためのブラウザソースを設定できます。メインOBSとサブOBSは個別に接続できます。",
            "設定方法",
            [
                "～～～～～OBS側設定～～～～～",
                "ツール ⇒ WebSocketサーバー設定から設定画面を開きます。" ,
                "WebSocketサーバーを有効にします。",
                "ポート、パスワードを設定します。" ,
                "（パスワードを設定する場合は認証を有効にするをチェックする）",
                "～～～～～JTSA側設定～～～～～",
                "設定パネルを開きます。",
                "OBS連携設定にてURLのポート部分（ws～:＜ポート＞）とパスワードを入力します。",
                "（アカウントはサブ垢設定をしていなければ関係なし）",
                "（OBS側でパスワード設定していない場合はパスワードを空にしてください）",
                "保存・接続テストを押して接続済みになれば接続できてます。",
            ],
            "※接続できない場合はポートとパスワードを見直して、それでも解決しなければDiscordでご連絡ください。"
        ),

        new(
            "OBS側ソース設定について",
            "配信拡張やプレイリスト・チャット表示などをOBSに表示",
            "アプリ側でサーバーを立てていますので、OBSのブラウザソースでURLを指定すると表示されるようになります。",
            "設定方法",
            [
                "ブラウザソースを追加",
                "URL（下部参照）を指定してソースを保存すれば表示されます",
                "表示されない場合はブラウザソースの詳細設定最下部にある「現在のページのキャッシュを更新」ボタンを押してください。"
            ],""
            )
        {
            SourceSettings =
            [
                new("チャット表示", "http://localhost:8026/chat"),
                new("ゲームプレイリスト表示", "http://localhost:8026/obs"),
                new("配信拡張画像出力（1920x1080でブラウザソースを設定推奨）", "http://localhost:8026/expansion")
            ]
        },
    ];

    public HelpPanel()
    {
        InitializeComponent();
        DataContext = this;
        TopicListBox.SelectedIndex = 0;
    }
}

public sealed record HelpTopic(
    string Title,
    string Summary,
    string Description,
    string SectionTitle,
    IReadOnlyList<string> Steps,
    string Tip)
{
    public IReadOnlyList<ObsSourceSetting> SourceSettings { get; init; } = [];
}

public sealed record ObsSourceSetting(string Name, string Url);
