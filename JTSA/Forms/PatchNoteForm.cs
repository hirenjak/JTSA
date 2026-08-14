namespace JTSA.Forms
{
    /// <summary>パッチノートの1リリース分の表示データ。</summary>
    public class PatchNoteForm
    {
        public required string Version { get; set; }
        public required string ReleaseDate { get; set; }
        public required string Summary { get; set; }
        public required IReadOnlyList<string> Changes { get; set; }
    }
}
