using System.ComponentModel.DataAnnotations;

namespace JTSA.Models;

public class T_StreamWindow : DBBase
{
    [Key]
    public required string ProcessName { get; set; }

    public required string WindowTitle { get; set; }

    public required string AppExePath { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}
