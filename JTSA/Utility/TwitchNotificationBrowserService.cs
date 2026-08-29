using Microsoft.Playwright;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace JTSA.Utility;

/// <summary>
/// Twitch の配信マネージャーを操作して、ライブ配信通知欄へ文言を入力する。
/// Twitch の公開 API では扱えない項目のため、実験機能としてブラウザ UI を操作する。
/// </summary>
public sealed class TwitchNotificationBrowserService : IAsyncDisposable
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? browserContext;

    public async Task FillAsync(string userName, string notificationText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationText);

        if (notificationText.Length > 140)
            throw new ArgumentException("ライブ配信通知は140文字以内で入力してください。");

        await EnsureBrowserAsync();

        var page = browserContext!.Pages.FirstOrDefault(page => !page.IsClosed)
            ?? await browserContext.NewPageAsync();
        await page.BringToFrontAsync();
        await page.GotoAsync(
            $"https://dashboard.twitch.tv/u/{Uri.EscapeDataString(userName)}/stream-manager",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        var loginDialog = page.GetByRole(AriaRole.Heading, new()
        {
            NameRegex = new System.Text.RegularExpressions.Regex(
                "Twitchにログイン|Log in to Twitch",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        });
        if (page.Url.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            await IsVisibleAfterDelayAsync(loginDialog))
        {
            await OpenLoginChromeAsync(userName);
            throw new InvalidOperationException(
                "ログイン用の通常Chromeを開きました。TwitchへログインしてChromeを閉じた後、もう一度自動入力してください。");
        }

        try
        {
            // Twitchでは編集ボタンに表示名が付かない場合があるため、
            // 安定して付与されるdata-a-targetを優先して取得する。
            var editButton = page.Locator(
                "button[data-a-target='stream-info-edit-button'], " +
                "[data-a-target='stream-info-edit-button'] button, " +
                "button[data-a-target*='edit-stream-info'], " +
                "button:has-text('配信情報を編集'), " +
                "button:has-text('配信情報の編集'), " +
                "button:has-text('Edit Stream Info')");

            await editButton.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });
            await editButton.First.ScrollIntoViewIfNeededAsync();
            await editButton.First.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });

            var notificationInput = page.GetByLabel(
                new System.Text.RegularExpressions.Regex(
                    "ライブ配信通知|Go Live Notification",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            await notificationInput.First.FillAsync(
                notificationText,
                new LocatorFillOptions { Timeout = 15_000 });
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(
                "Twitch画面の自動入力に失敗しました。開いた画面で手動入力するか、画面構成の変更を確認してください。",
                ex);
        }
    }

    public async Task OpenLoginChromeAsync(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        if (browser is not null)
        {
            await browser.CloseAsync();
            browser = null;
            browserContext = null;
        }

        var chromePath = FindChromePath()
            ?? throw new InvalidOperationException(
                "Google Chromeが見つかりませんでした。Chromeがインストールされているか確認してください。");
        var profileDirectory = GetProfileDirectory();
        Directory.CreateDirectory(profileDirectory);
        var dashboardUrl = $"https://dashboard.twitch.tv/u/{Uri.EscapeDataString(userName)}/stream-manager";

        Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = $"--user-data-dir=\"{profileDirectory}\" " +
                        $"--no-first-run --no-default-browser-check \"{dashboardUrl}\"",
            UseShellExecute = false
        });
    }

    private async Task EnsureBrowserAsync()
    {
        if (browserContext is not null)
            return;

        // Costura で Microsoft.Playwright.dll が埋め込まれると、Playwright は
        // ドライバーを .NET ランタイムの配置先から探してしまう。
        // NuGet が出力した実行フォルダ直下の .playwright を明示的に使わせる。
        var driverRoot = AppContext.BaseDirectory;
        var driverDirectory = Path.Combine(driverRoot, ".playwright");
        if (!Directory.Exists(driverDirectory))
        {
            throw new InvalidOperationException(
                $"ブラウザ操作用ファイルが見つかりません。JTSAの再ビルドが必要です。探索先: {driverDirectory}");
        }

        Environment.SetEnvironmentVariable(
            "PLAYWRIGHT_DRIVER_SEARCH_PATH",
            driverRoot,
            EnvironmentVariableTarget.Process);

        playwright = await Playwright.CreateAsync();
        var profileDirectory = GetProfileDirectory();
        Directory.CreateDirectory(profileDirectory);

        var chromePath = FindChromePath()
            ?? throw new InvalidOperationException(
                "Google Chromeが見つかりませんでした。Chromeがインストールされているか確認してください。");
        var debuggingPort = ReserveTcpPort();

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = $"--remote-debugging-port={debuggingPort} " +
                            $"--user-data-dir=\"{profileDirectory}\" " +
                            "--no-first-run --no-default-browser-check about:blank",
                UseShellExecute = false
            });

            var endpoint = $"http://127.0.0.1:{debuggingPort}";
            await WaitForChromeAsync(endpoint);
            browser = await playwright.Chromium.ConnectOverCDPAsync(endpoint);
            browserContext = browser.Contexts.FirstOrDefault()
                ?? throw new InvalidOperationException("Chromeの操作セッションを取得できませんでした。");
        }
        catch (Exception ex) when (ex is PlaywrightException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Google Chromeへ接続できませんでした。専用Chromeを閉じてから、もう一度実行してください。",
                ex);
        }
    }

    private static string GetProfileDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JTSA",
        "TwitchNotificationChrome");

    private static async Task<bool> IsVisibleAfterDelayAsync(ILocator locator)
    {
        try
        {
            await locator.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3_000
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    private static string? FindChromePath()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForChromeAsync(string endpoint)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync($"{endpoint}/json/version");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Chrome のデバッグ受付開始まで待つ。
            }
            catch (TaskCanceledException)
            {
                // 起動直後のタイムアウトは再試行する。
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("Chromeの起動待ちがタイムアウトしました。");
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
            await browser.CloseAsync();
        playwright?.Dispose();
    }
}
