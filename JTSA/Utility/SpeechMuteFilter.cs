namespace JTSA.Utility;

public static class SpeechMuteFilter
{
    public static HashSet<string> Parse(string? value)
    {
        var muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) return muted;

        foreach (var line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var login = line.Trim();
            if (login.Length > 0) muted.Add(login);
        }

        return muted;
    }

    public static string Serialize(IEnumerable<string>? logins)
    {
        var muted = Parse(string.Join('\n', logins ?? []));
        return string.Join('\n', muted.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    public static bool IsMuted(ISet<string> mutedLogins, string? userLogin) =>
        !string.IsNullOrWhiteSpace(userLogin) && mutedLogins.Contains(userLogin);

    public static HashSet<string> Toggle(IEnumerable<string>? mutedLogins, string? userLogin)
    {
        var muted = Parse(string.Join('\n', mutedLogins ?? []));
        var login = userLogin?.Trim() ?? string.Empty;
        if (login.Length == 0) return muted;
        if (!muted.Remove(login)) muted.Add(login);
        return muted;
    }
}
