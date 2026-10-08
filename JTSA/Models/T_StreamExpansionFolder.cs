using System.ComponentModel.DataAnnotations;

namespace JTSA.Models;

internal class T_StreamExpansionFolder
{
    [Key]
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
