using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTSA.NizimaLivePlugin;

internal enum NizimaTriggerValueInputMode
{
    None,
    ChannelPoint,
    Chat,
    ScheduledTime,
    AdUpcoming,
    ObsStreamStart
}

internal static class NizimaTriggerUi
{
    public static NizimaTriggerValueInputMode InputMode(string? triggerType) =>
        triggerType switch
        {
            NizimaTriggerTypes.ChannelPoint => NizimaTriggerValueInputMode.ChannelPoint,
            NizimaTriggerTypes.Chat => NizimaTriggerValueInputMode.Chat,
            NizimaTriggerTypes.ScheduledTime => NizimaTriggerValueInputMode.ScheduledTime,
            NizimaTriggerTypes.AdUpcoming => NizimaTriggerValueInputMode.AdUpcoming,
            NizimaTriggerTypes.ObsStreamStart => NizimaTriggerValueInputMode.ObsStreamStart,
            _ => NizimaTriggerValueInputMode.None
        };

    public static string ValueFieldLabel(string? triggerType) =>
        InputMode(triggerType) switch
        {
            NizimaTriggerValueInputMode.ChannelPoint => "チャンネルポイント報酬",
            NizimaTriggerValueInputMode.Chat => "含まれる文字列（部分一致）",
            NizimaTriggerValueInputMode.ScheduledTime => "指定時刻（PC の時計・24時間制）",
            NizimaTriggerValueInputMode.AdUpcoming => "CM 開始何分前",
            NizimaTriggerValueInputMode.ObsStreamStart => "OBS",
            _ => "トリガー値"
        };

    public static IReadOnlyList<NizimaChoice> AdUpcomingChoices { get; } =
        Enumerable.Range(1, 10)
            .Select(minute => new NizimaChoice(minute.ToString(CultureInfo.InvariantCulture), $"{minute} 分前"))
            .ToList();

    public static IReadOnlyList<NizimaChoice> ObsStreamChoices { get; } =
    [
        new("main", "メイン OBS"),
        new("sub", "サブ OBS")
    ];

    public static bool TryFormatScheduledTime(int hour, int minute, out string formatted)
    {
        formatted = "";
        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            return false;
        formatted = string.Create(CultureInfo.InvariantCulture, $"{hour:00}:{minute:00}");
        return true;
    }

    public static bool TryParseScheduledTime(string? value, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var match = Regex.Match(value.Trim(), @"^(\d{1,2}):(\d{1,2})$");
        if (!match.Success)
            return false;
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out hour))
            return false;
        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out minute))
            return false;
        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    public static bool TryParseScheduledParts(string? hourText, string? minuteText, out string formatted, out string? error)
    {
        formatted = "";
        error = null;
        if (string.IsNullOrWhiteSpace(hourText) || string.IsNullOrWhiteSpace(minuteText))
        {
            error = "時と分を入力してください。";
            return false;
        }

        if (!int.TryParse(hourText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(minuteText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
        {
            error = "時と分は半角数字で入力してください。";
            return false;
        }

        if (!TryFormatScheduledTime(hour, minute, out formatted))
        {
            error = "時は 0～23、分は 0～59 の範囲で入力してください。";
            return false;
        }

        return true;
    }

    public static string FormatTriggerValueForSummary(string triggerType, string triggerValue)
    {
        if (string.IsNullOrWhiteSpace(triggerValue))
            return "";

        return triggerType switch
        {
            NizimaTriggerTypes.ScheduledTime => triggerValue,
            NizimaTriggerTypes.AdUpcoming =>
                AdUpcomingChoices.FirstOrDefault(c => c.Id == triggerValue)?.Label ?? $"{triggerValue} 分前",
            NizimaTriggerTypes.ObsStreamStart =>
                ObsStreamChoices.FirstOrDefault(c => c.Id == triggerValue)?.Label ?? triggerValue,
            _ => triggerValue
        };
    }

    public static void AttachDigitsOnly(TextBox textBox, int maxLength)
    {
        textBox.MaxLength = maxLength;
        textBox.PreviewTextInput += (_, e) => e.Handled = e.Text.Length > 0 && e.Text.Any(ch => !char.IsDigit(ch));
        DataObject.AddPastingHandler(textBox, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var paste = (string)e.DataObject.GetData(typeof(string))!;
                if (paste.Any(ch => !char.IsDigit(ch)))
                    e.CancelCommand();
            }
        });
    }
}
