using JTSA.Models;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionStreamInfoAppliedTests
{
    [Fact]
    public void MatchesOnlyEnabledStreamInfoAppliedRule()
    {
        var rule = new T_StreamExpansionHeader
        {
            IsActive = true,
            IsStreamInfoApplied = true,
            UpdatedDateTime = DateTime.Now
        };

        Assert.True(StreamExpansionService.Matches(
            rule, StreamExpansionTriggerType.StreamInfoApplied, string.Empty));
        Assert.False(StreamExpansionService.Matches(
            rule, StreamExpansionTriggerType.Follow, string.Empty));

        rule.IsStreamInfoApplied = false;
        Assert.False(StreamExpansionService.Matches(
            rule, StreamExpansionTriggerType.StreamInfoApplied, string.Empty));
    }
}
