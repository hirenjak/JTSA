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
}