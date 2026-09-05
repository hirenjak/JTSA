using JTSA.Forms;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class ParticipationMoverTests
{
    [Fact]
    public void MovingToPlayingResetsMatchesAndPreservesParticipations()
    {
        var user = new ParticipationUserForm("u", "User", "", DateTime.Now) { ParticipationCount = 3, MatchCount = 5 };
        var waiting = new List<ParticipationUserForm> { user };
        var playing = new List<ParticipationUserForm>();
        Assert.True(ParticipationMover.Move(waiting, playing, user.EntryKey, true, 0));
        Assert.Empty(waiting);
        Assert.Equal(3, Assert.Single(playing).ParticipationCount);
        Assert.Equal(0, Assert.Single(playing).MatchCount);
        Assert.True(ParticipationMover.Move(waiting, playing, user.EntryKey, false, 0));
        Assert.Empty(playing);
        Assert.Equal(3, Assert.Single(waiting).ParticipationCount);
        Assert.False(ParticipationMover.Move(waiting, playing, Guid.NewGuid(), true, 0));
    }

    [Fact]
    public void ParticipationIncrementsOnlyWhenMatchesChangeFromZeroToOne()
    {
        var user = new ParticipationUserForm("u", "User", "", DateTime.Now);
        user = user.AdjustMatches(-1);
        Assert.Equal(0, user.MatchCount);
        Assert.Equal(0, user.ParticipationCount);
        user = user.AdjustMatches(1).AdjustMatches(1);
        Assert.Equal(2, user.MatchCount);
        Assert.Equal(1, user.ParticipationCount);
        user = user.AdjustMatches(-1).AdjustMatches(-1);
        Assert.Equal(1, user.ParticipationCount);
        user = user.AdjustMatches(1);
        Assert.Equal(2, user.ParticipationCount);
    }

    [Fact]
    public void ReorderCorrectsIndexAfterRemovalAndPreservesCount()
    {
        var first = new ParticipationUserForm("a", "A", "", DateTime.Now) { ParticipationCount = 2, MatchCount = 4 };
        var second = first with { UserId = "b", EntryKey = Guid.NewGuid() };
        var playing = new List<ParticipationUserForm> { first, second };
        Assert.True(ParticipationMover.Move(new List<ParticipationUserForm>(), playing, first.EntryKey, true, 2));
        Assert.Equal(new[] { second, first }, playing);
        Assert.False(ParticipationMover.Move(new List<ParticipationUserForm>(), playing, first.EntryKey, true, 2));
    }
}
