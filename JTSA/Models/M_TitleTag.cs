using JTSA.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class M_TitleTag : DBBaseTransaction
{
    [Key]
    public long Id { get; set; }

    public required string DisplayName { get; set; }
}