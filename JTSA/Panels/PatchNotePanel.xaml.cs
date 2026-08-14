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
                Version = "v1.1.7",
                ReleaseDate = "2026/08/14",
                Summary = "既存機能の改善",
                Changes =
                [
                    "配信概要パネル：カテゴリ検索および既存カテゴリ一覧を表示するように変更",
                    "配信概要パネル：タイトルタグをプレースホルダー形式で挿入できるように変更",
                    "プレイリストパネル：カテゴリ検索により追加していたのを既存カテゴリから追加するように変更",
                    "プレイリストパネル：ヘッダー画像をTwitchカテゴリのボックスアートへ統一",
                    "チャットパネル：チャットユーザー一覧表示を追加",
                    "パネル一覧：パッチノートパネルを追加",
                ]
            });

            DataContext = this;
            InitializeComponent();
        }
    }
}
