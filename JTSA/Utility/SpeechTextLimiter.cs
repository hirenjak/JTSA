using System.Globalization;
using System.Text;

namespace JTSA.Utility;

public static class SpeechTextLimiter
{
    public const int DefaultMaxChars = 80;
    public const int DefaultMaxSameToken = 3;

    public static string Limit(string? text, int maxChars, int maxSameToken)
    {
        var limited = LimitConsecutiveSameTokens(text, maxSameToken);
        return TruncateChars(limited, maxChars);
    }

    public static string LimitConsecutiveSameTokens(string? text, int maxRepeat)
    {
        if (string.IsNullOrWhiteSpace(text) || maxRepeat <= 0)
            return text ?? string.Empty;

        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return string.Empty;

        var kept = new List<string>(tokens.Length);
        string? prev = null;
        var run = 0;

        foreach (var token in tokens)
        {
            if (prev is not null
                && string.Equals(token, prev, StringComparison.OrdinalIgnoreCase))
            {
                run++;
                if (run <= maxRepeat) kept.Add(token);
                continue;
            }

            prev = token;
            run = 1;
            kept.Add(token);
        }

        return string.Join(' ', kept);
    }

    public static string TruncateChars(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || maxChars <= 0)
            return text ?? string.Empty;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var sb = new StringBuilder(Math.Min(text.Length, maxChars * 2));
        var count = 0;
        while (enumerator.MoveNext())
        {
            if (count >= maxChars) break;
            sb.Append(enumerator.GetTextElement());
            count++;
        }

        return sb.ToString();
    }

    public static int ParseNonNegative(string? value, int fallback)
    {
        if (!int.TryParse(value, out var parsed) || parsed < 0)
            return fallback;
        return parsed;
    }
}
