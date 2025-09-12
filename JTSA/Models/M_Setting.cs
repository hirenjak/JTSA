using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class M_Setting
{
    [Key]
    public int Name { get; set; }

    public required string Value { get; set; }
    
    public DateTime CreatedDateTime { get; set; }
    
    public DateTime UpdateDateTime { get; set; }
}