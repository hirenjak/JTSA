using JTSA.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class T_TitleText : DBBaseTransaction
{
    [Key]
    public long Id { get; set; }

    public required string Content { get; set; }

    public string TitlePlaceholder { get; set; } = string.Empty;

    public required string CategoryId { get; set; }

    public required string CategoryName { get; set; }

    public required string CategoryBoxArtUrl { get; set; }
}
