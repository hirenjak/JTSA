using JTSA.Models;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// チャンネルポイント報酬のキャッシュ。
/// プリセット編集画面が Twitch API を叩かずに報酬名を表示できるようにするために持つ。
/// 一覧取得のたびに上書きされる。
/// </summary>
public class M_ChannelPoint : DBBase
{
    /// <summary> 報酬ID（TwitchのカスタムリワードID） </summary>
    [Key]
    public required string RewardId { get; set; }

    /// <summary> 報酬名 </summary>
    public required string Title { get; set; }

    /// <summary> コスト </summary>
    public int Cost { get; set; }

    /// <summary> アイコンURL（1x） </summary>
    public string? ImageUrl { get; set; }

    /// <summary> このアプリ（同一client_id）から操作できるか </summary>
    public bool IsManageable { get; set; }
}
