using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_Category : DBBase
{
    [Key]
    public required string CategoryId { get; set; }

    public required string DisplayName { get; set; }

    public required string BoxArtUrl { get; set; }

    public string? SteamUrl { get; set; }

    public string? SteamHeaderArtUrl { get; set; }

    /// <summary>
    /// このカテゴリに紐づくチャンネルポイントプリセットID。
    /// null（未紐づけ）の場合はカテゴリを切り替えてもプリセットを適用しない。
    /// </summary>
    public long? ChannelPointPresetId { get; set; }
}