namespace JTSA.Models
{
    public abstract class DBBase
    {
        public required DateTime LastUsedDateTime { get; set; }

        public required DateTime CreatedDateTime { get; set; }

        public required DateTime UpdatedDateTime { get; set; }
    }
}
