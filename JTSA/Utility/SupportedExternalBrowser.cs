using System.Diagnostics;
using System.IO;

namespace JTSA.Utility;

internal static class SupportedExternalBrowser
{
    public static void OpenChromeOrFirefox(string url)
    {
        var browserPath = FindBrowserPath()
            ?? throw new InvalidOperationException(
                "StreamTogetherに対応するGoogle ChromeまたはFirefoxが見つかりませんでした。");

        var startInfo = new ProcessStartInfo
        {
            FileName = browserPath,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(url);
        Process.Start(startInfo);
    }

    private static string? FindBrowserPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
            Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe"),
            Path.Combine(localAppData, "Mozilla Firefox", "firefox.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
