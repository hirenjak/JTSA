using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_User : DBBase
{
    [Key]
    public required string UserId { get; set; }

    public required string LoginId { get; set; }

    public required string DisplayName { get; set; }

    public string? ProfielImageUrl { get; set; }
}