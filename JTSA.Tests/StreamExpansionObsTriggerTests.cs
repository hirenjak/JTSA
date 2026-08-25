using JTSA.Models;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionObsTriggerTests
{
    [Theory]
    [InlineData(true, false, "main", true)]
    [InlineData(true, false, "sub", false)]
    [InlineData(false, true, "main", false)]
    [InlineData(false, true, "sub", true)]
    [InlineData(true, true, "main", true)]
    [InlineData(true, true, "sub", true)]
    public void ObsStreamStartMatchesConfiguredObs(
        bool isMainObs, bool isSubObs, string eventObs, bool expected)
    {
        var rule = CreateRule();
        rule.IsObsStreamStartMain = isMainObs;
        rule.IsObsStreamStartSub = isSubObs;

        Assert.Equal(expected,
            StreamExpansionService.Matches(rule, StreamExpansionTriggerType.ObsStreamStart, eventObs));
    }

    [Fact]
    public void ObsStreamStartDoesNotMatchWhenNeitherObsIsSelected()
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
