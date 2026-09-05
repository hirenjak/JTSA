using System.Globalization;
using System.Text.RegularExpressions;

namespace JTSA.Utility;

/// <summary>
/// タイトル内の組み込みプレースホルダーを表示文字列へ置換する。
/// </summary>
public static class TitlePlaceholderReplacer
{
    public const string TitlePlaceholder = "${title}";
    public const string DatePlaceholder = "${date}";
    public const string JapaneseCategoryPlaceholder = "${category_ja}";

    /// <summary>
    /// テンプレート内の ${title} をタイトル本文へ置換する。
    /// テンプレートが空の場合はタイトル本文をそのまま返す。
    /// </summary>
    public static string ReplaceTitle(
        string titleText,
        string templateText,
        string japaneseCategoryName = "")
    {
        if (string.IsNullOrEmpty(templateText)) return titleText;

        return templateText
            .Replace(TitlePlaceholder, titleText)
            .Replace(JapaneseCategoryPlaceholder, japaneseCategoryName);
    }

    /// <summary>
    /// ${date} / {date} は yyyy/MM/dd、カンマ以降は日付の表示形式として扱う。
    /// </summary>
    public static string ReplaceDate(string titleText, DateTime dateTime)
    {
        return Regex.Replace(titleText, @"\$?\{date(?:\s*,\s*(?<format>[^{}]*))?\}", match =>
        {
            var format = match.Groups["format"].Success
                ? match.Groups["format"].Value.Trim()
                : "yyyy/MM/dd";
            if (format.Length == 0) return match.Value;

            // Date placeholders accept lowercase m as month; quoted/escaped literals stay intact.
            format = Regex.Replace(format, @"'[^']*'|""[^""]*""|\\.|m+", token =>
                token.Value[0] == 'm' ? token.Value.ToUpperInvariant() : token.Value);
            // A single date component is a custom format, not a .NET standard format specifier.
            if (format is "M" or "d" or "y") format = "%" + format;
            try
            {
                return dateTime.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                // A mistyped user format must not interrupt title updates.
                return match.Value;
            }
        });
    }
}
