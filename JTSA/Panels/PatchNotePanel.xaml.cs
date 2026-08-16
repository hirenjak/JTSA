using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Controls;

namespace JTSA.Panels
{
    /// <summary>アプリ内パッチノート表示パネル。</summary>
    public partial class PatchNotePanel : UserControl
    {
        public string CurrentVersion { get; }

        public ObservableCollection<PatchNoteForm> PatchNotes { get; } = new();

        public PatchNotePanel()
        {
            CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "-";

            PatchNotes.Add(new PatchNoteForm
            {
                Version = "v1.1.8",
                ReleaseDate = "2026/08/15",
                Summary = "既存機能の改善",
                Changes =
                [
                    "チャットパネル：",
                    "・棒読みちゃん連携に対応",
                    "設定パネル：",
                    "・",

                ]
            });

            PatchNotes.Add(new PatchNoteForm
            {
                Version = "v1.1.8",
                ReleaseDate = "2026/08/15",
                Summary = "既存機能の改善",
                Changes =
                [
                    "配信概要パネル：${date}で本日日付を挿入するプレースホルダーを追加",
                    "設定パネル：X投稿用の文章のテンプレートを編集できるように機能追加",
                    "配信拡張パネル：トリガー動作からの遅延発火を可能に機能追加",
                    "配信拡張パネル：チャット送信にプレースホルダーでレイド元配信者情報を設定可能に機能追加",
                    "配信拡張パネル：フォロー時と配信中初チャット時のトリガーを追加",
                    "チャットパネル：OBS側でブラウザソースを設定するとOBSにチャットが表示される機能を追加",
                    "チャットパネル：オーバーレイの表示サイズ、フォントサイズ、アイコンの表示非表示を切り替えれるように機能追加",
                    "外部アプリパネル：機能復元してパネルとして追加",
                    "外部アプリパネル：登録されているアプリをJTSA側から起動できる機能"
                ]
            });

            PatchNotes.Add(new PatchNoteForm
            {
                Version = "v1.1.7",
                ReleaseDate = "2026/08/14",
                Summary = "既存機能の改善",
                Changes =
                [
                    "配信概要パネル：カテゴリ検索および既存カテゴリ一覧を表示するように変更",
                    "配信概要パネル：タイトルタグをプレースホルダー形式で挿入できるように変更",
                    "プレイリストパネル：カテゴリ検索により追加していたのを既存カテゴリから追加するように変更",
                    "プレイリストパネル：ヘッダー画像をTwitchカテゴリのボックスアートへ統一",
                    "プレイリストパネル：プレイ中の次にプレイ中断中のステータスを追加",
                    "チャットパネル：チャットユーザー一覧表示を追加",
                    "チャットパネル：ユーザー一覧にて右クリックでフレンドに追加機能を追加",
                    "フレンドパネル：フレンドに追加しているユーザーのみが表示されるように変更",
                    "パネル一覧：パッチノートパネルを追加",
                ]
            });

            DataContext = this;
            InitializeComponent();
        }
    }
}
