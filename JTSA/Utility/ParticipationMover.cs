using JTSA.Forms;

namespace JTSA.Utility;

internal static class ParticipationMover
{
    internal static bool Move(IList<ParticipationUserForm> waiting, IList<ParticipationUserForm> playing,
        Guid entryKey, bool toPlaying, int insertionIndex)
    {
        var source = waiting.Any(x => x.EntryKey == entryKey) ? waiting : playing;
        var user = source.FirstOrDefault(x => x.EntryKey == entryKey);
        if (user is null) return false;
        var target = toPlaying ? playing : waiting;
        var oldIndex = source.IndexOf(user);
        var index = Math.Clamp(insertionIndex, 0, target.Count);
        if (ReferenceEquals(source, target) && oldIndex < index) index--;
        if (ReferenceEquals(source, target) && oldIndex == index) return false;
        source.RemoveAt(oldIndex);
        if (!ReferenceEquals(source, target) && toPlaying)
            user = user with { MatchCount = 0 };
        target.Insert(index, user);
        return true;
    }
}
