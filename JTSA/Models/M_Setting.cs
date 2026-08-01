using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_Setting : DBBase
{
    [Key]
    public int Name { get; set; }

    public required string Value { get; set; }

    public enum SettingName : int
    {
        UserName = 1,
        RefreshToken = 2,
        ExpiresIn = 3,
    }
}