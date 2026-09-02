using System.ComponentModel.DataAnnotations;

namespace JTSA.Models;

public class M_ObsCaptureSource : DBBase
{
    [Key]
    public long Id { get; set; }
    public bool IsSubObs { get; set; }
    public string InputName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
