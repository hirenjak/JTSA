using System.ComponentModel.DataAnnotations;

namespace JTSA.Models;

public class M_ObsTextSource : DBBase
{
    [Key]
    public long Id { get; set; }
    public bool IsSubObs { get; set; }
    public string DisplayName { get; set; } = "";
    public string? SceneName { get; set; }
    public string? SourceName { get; set; }
    public int SortNumber { get; set; }
}
