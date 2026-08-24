namespace JTSA.Utility;

internal static class ChannelPointChatFormatter
{
    public const string RewardColor = "#66D98A";
    public const string UserInputColor = "#98F5B3";

    public static string Format(string rewardTitle, string userInput)
    {
        var title = $"🎁 {rewardTitle}";
        return string.IsNullOrWhiteSpace(userInput)
            ? title
            : $"{title}{Environment.NewLine}{userInput}";
    }

    public static List<Panels.TwitchChatPart> CreateParts(string rewardTitle, string userInput)
    {
        var rewardParts = new List<Panels.TwitchChatPart>
        {
            new() { Text = $"🎁 {rewardTitle}", Foreground = RewardColor }
        };

        if (string.IsNullOrWhiteSpace(userInput)) return rewardParts;

        rewardParts.Add(new Panels.TwitchChatPart
        {
            Text = $"{Environment.NewLine}{userInput}",
            Foreground = UserInputColor
        });
        return rewardParts;
    }
}
