using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_User : DBBase
{
    [Key]
    public required string BroadcastId { get; set; }

    public required string UserId { get; set; }

    public required string DisplayName { get; set; }
}