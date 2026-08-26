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
    /// ${date} を指定日時の日付（yyyy/MM/dd）へ置換する。
    /// </summary>
    public static string ReplaceDate(string titleText, DateTime dateTime)
    {
        return titleText.Replace(DatePlaceholder, dateTime.ToString("yyyy/MM/dd"));
    }
}
