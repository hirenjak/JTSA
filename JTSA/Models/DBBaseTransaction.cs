namespace JTSA.Models
{
    public abstract class DBBaseTransaction : DBBase
    {
        public int SelectedCount { get; set; }

        public int SortNumber { get; set; }
    }
}
