namespace JTSA.Models;

internal class T_StreamExpansionItem : DBBaseTransaction
{
    public long Id { get; set; }

    public long HeaderId { get; set; }
    
    public string ActionType { get; set; } = "Audio";
    
    public string Content { get; set; } = string.Empty;
    
    public int Weight { get; set; } = 1;

    public int Volume { get; set; } = 100;
}
