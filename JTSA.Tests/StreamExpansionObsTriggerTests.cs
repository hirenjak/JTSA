using JTSA.Models;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionObsTriggerTests
{
    [Theory]
    [InlineData(false, "main", true)]
    [InlineData(false, "sub", false)]
    [InlineData(true, "main", false)]
    [InlineData(true, "sub", true)]
    public void ObsStreamStartMatchesOnlyConfiguredObs(bool isSubObs, string eventObs, bool expected)
    {
        var rule = CreateRule();
        rule.IsObsStreamStart = true;
        rule.IsObsStreamStartSub = isSubObs;

        Assert.Equal(expected,
            StreamExpansionService.Matches(rule, StreamExpansionTriggerType.ObsStreamStart, eventObs));
    }

    [Fact]
    public void ObsStreamStartDoesNotMatchDisabledTrigger()
    {
        var rule = CreateRule();

        Assert.False(StreamExpansionService.Matches(
            rule, StreamExpansionTriggerType.ObsStreamStart, "main"));
    }

    private static T_StreamExpansionHeader CreateRule() => new()
    {
        Name = "test",
        IsActive = true,
        UpdatedDateTime = DateTime.Now
    };
}
