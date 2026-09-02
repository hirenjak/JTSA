using System.Collections.ObjectModel;
using System.Windows;

namespace JTSA;

public partial class StreamExpansionPlaceholderHelpWindow : Window
{
    public ObservableCollection<PlaceholderHelpItem> PlaceholderItems { get; } =
    [
        new("{chat_user}", "チャットしたユーザーの表示名（チャット・初回チャット時）"),
        new("{chat_login}", "チャットしたユーザーのログイン名（チャット・初回チャット時）"),
        new("{raid_user}", "レイド元ユーザーの表示名（レイド時）"),
        new("{raid_title}", "レイド元ユーザーの直近配信タイトル（レイド時）"),
        new("{raid_category}", "レイド元ユーザーの直近配信カテゴリ（レイド時）"),
        new("{trigger_type}", "発火したトリガーの種類"),
        new("{trigger_value}", "コメント本文、ユーザー名、報酬ID、Bits数などのトリガー値"),
        new("{trigger_obs}", "配信開始イベントの発火元OBS（main / sub）"),
        new("{stream_title}", "現在の配信タイトル"),
        new("{stream_category}", "現在の配信カテゴリ"),
        new("{channel_point_input}", "チャンネルポイントの入力文言、またはトリガーコメントより後ろの文言"),
        new("{stream_category_ja}", "現在の配信カテゴリ名（日本語。未取得時はTwitchカテゴリ名）"),
        new("{stream_bits_users}", "配信内のビッツユーザーと累計Bits（多い順・改行区切り）"),
        new("{stream_subscribe_users}", "配信内のサブスク月数・Tier、またはサブギフ数・Tier（改行区切り）"),
        new("{stream_raid_users}", "配信内のレイドユーザーと累計視聴者数（多い順・改行区切り）"),
        new("{stream_follow_users}", "配信内のフォローユーザー（新しい順・改行区切り）")
    ];

    public StreamExpansionPlaceholderHelpWindow()
    {
        InitializeComponent();
        DataContext = this;
    }
}

public sealed record PlaceholderHelpItem(string Token, string Description);
