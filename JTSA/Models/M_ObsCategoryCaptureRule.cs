using System.ComponentModel.DataAnnotations;

namespace JTSA.Models;

public class M_ObsCategoryCaptureRule : DBBase
{
    [Key]
    public string CategoryId { get; set; } = string.Empty;
    public bool IsSubObs { get; set; }
    public string InputName { get; set; } = string.Empty;
    public string DestinationValue { get; set; } = string.Empty;
}
