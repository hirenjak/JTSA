namespace JTSA.Forms;

public sealed record ParticipationUserForm(string UserId, string DisplayName, string UserInput, DateTime RedeemedAt)
{
    public string ProfileImageUrl { get; init; } = string.Empty;
    public int ParticipationCount { get; init; }
    public int MatchCount { get; init; }
    public Guid EntryKey { get; init; } = Guid.NewGuid();

    public ParticipationUserForm AdjustMatches(int delta)
    {
        var next = (int)Math.Clamp((long)MatchCount + delta, 0, int.MaxValue);
        return this with
        {
            MatchCount = next,
            ParticipationCount = MatchCount == 0 && next == 1
                ? (int)Math.Min(int.MaxValue, (long)ParticipationCount + 1) : ParticipationCount
        };
    }
}
