using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_Setting : DBBase
{
    [Key]
    public int Name { get; set; }

    public required string Value { get; set; }
}