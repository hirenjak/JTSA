namespace JTSA.Models
{
    public abstract class DBBase
    {
        public DateTime LastUsedDateTime { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public required DateTime UpdatedDateTime { get; set; }
    }
}
