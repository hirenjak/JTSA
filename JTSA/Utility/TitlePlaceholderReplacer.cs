namespace JTSA.Utility;

/// <summary>
/// タイトル内の組み込みプレースホルダーを表示文字列へ置換する。
/// </summary>
public static class TitlePlaceholderReplacer
{
    public const string DatePlaceholder = "${date}";

    /// <summary>
    /// ${date} を指定日時の日付（yyyy/MM/dd）へ置換する。
    /// </summary>
    public static string ReplaceDate(string titleText, DateTime dateTime)
    {
        return titleText.Replace(DatePlaceholder, dateTime.ToString("yyyy/MM/dd"));
    }
}
