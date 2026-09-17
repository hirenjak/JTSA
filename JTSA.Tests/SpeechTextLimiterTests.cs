using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class SpeechTextLimiterTests
{
    [Fact]
    public void LimitConsecutiveSameTokensCapsRepeats()
    {
        var actual = SpeechTextLimiter.LimitConsecutiveSameTokens(
            "Kappa Kappa Kappa Kappa Kappa", 3);

        Assert.Equal("Kappa Kappa Kappa", actual);
    }

    [Fact]
    public void LimitConsecutiveSameTokensResetsOnDifferentToken()
    {
        var actual = SpeechTextLimiter.LimitConsecutiveSameTokens(
            "Kappa LUL Kappa Kappa Kappa Kappa", 3);

        Assert.Equal("Kappa LUL Kappa Kappa Kappa", actual);
    }

    [Fact]
    public void LimitConsecutiveSameTokensIgnoresCase()
    {
        var actual = SpeechTextLimiter.LimitConsecutiveSameTokens(
            "kappa KAPPA Kappa Kappa", 3);

        Assert.Equal("kappa KAPPA Kappa", actual);
    }

    [Fact]
    public void LimitConsecutiveSameTokensKeepsJapaneseWithoutSpaces()
    {
        Assert.Equal("こんにちは", SpeechTextLimiter.LimitConsecutiveSameTokens("こんにちは", 3));
    }

    [Fact]
    public void ZeroMaxRepeatDoesNotChangeText()
    {
        const string text = "Kappa Kappa Kappa Kappa";
        Assert.Equal(text, SpeechTextLimiter.LimitConsecutiveSameTokens(text, 0));
    }

    [Fact]
    public void TruncateCharsCutsByTextElement()
    {
        Assert.Equal("こんに", SpeechTextLimiter.TruncateChars("こんにちは", 3));
    }

    [Fact]
    public void LimitAppliesTokenCapThenCharCap()
    {
        var actual = SpeechTextLimiter.Limit(
            "Kappa Kappa Kappa Kappa hello", 20, 2);

        Assert.Equal("Kappa Kappa hello", actual);
        Assert.True(actual.Length <= 20);
    }

    [Fact]
    public void ParseNonNegativeFallsBackOnInvalid()
    {
        Assert.Equal(80, SpeechTextLimiter.ParseNonNegative("abc", 80));
        Assert.Equal(80, SpeechTextLimiter.ParseNonNegative("-1", 80));
        Assert.Equal(0, SpeechTextLimiter.ParseNonNegative("0", 80));
        Assert.Equal(12, SpeechTextLimiter.ParseNonNegative("12", 80));
    }
}
