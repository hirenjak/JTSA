using JTSA.Plugin.Abstractions;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;

namespace JTSA.MultiPlatformPlugin;

public partial class MultiPlatformWindow : Window
{
    private readonly IJtsaPluginContext context;
    private readonly IJtsaStreamMetadataPluginContext? metadataContext;
    private readonly IJtsaExternalChatPluginContext? chatContext;
    private readonly string settingsPath;
    private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly YouTubeService youTubeService;
    private readonly KickService kickService;
    private CancellationTokenSource? youTubeChatCancellation;
    private CancellationTokenSource? kickChatCancellation;

    public MultiPlatformWindow(IJtsaPluginContext context)
    {
        InitializeComponent();
        this.context = context;
        metadataContext = context as IJtsaStreamMetadataPluginContext;
        chatContext = context as IJtsaExternalChatPluginContext;
        settingsPath = Path.Combine(context.DataDirectory, "platform-settings.json");
        youTubeService = new YouTubeService(httpClient);
        kickService = new KickService(httpClient);
        LoadSettings();
        ImportFromJtsa();
        if (context.MainWindowHandle != nint.Zero)
            new WindowInteropHelper(this) { Owner = context.MainWindowHandle };
        Closed += Window_Closed;
    }

    private void LoadSettings()
    {
        var settings = PlatformSettings.Load(settingsPath);
        YouTubeBroadcastIdBox.Text = settings.YouTubeBroadcastId;
        YouTubeCategoryIdBox.Text = settings.YouTubeCategoryId;
        YouTubeLiveChatIdBox.Text = settings.YouTubeLiveChatId;
        KickCategoryIdBox.Text = settings.KickCategoryId;
        KickChannelSlugBox.Text = settings.KickChannelSlug;
    }

    private void SaveSettings() => new PlatformSettings
    {
        YouTubeBroadcastId = YouTubeBroadcastIdBox.Text.Trim(),
        YouTubeCategoryId = YouTubeCategoryIdBox.Text.Trim(),
        YouTubeLiveChatId = YouTubeLiveChatIdBox.Text.Trim(),
        KickCategoryId = KickCategoryIdBox.Text.Trim(),
        KickChannelSlug = KickChannelSlugBox.Text.Trim()
    }.Save(settingsPath);

    private void ImportButton_Click(object sender, RoutedEventArgs e) => ImportFromJtsa();

    private void ImportFromJtsa()
    {
        if (metadataContext is null)
        {
            SetStatus("このJTSA本体は配信情報の取り込みに対応していません。");
            return;
        }
        var current = metadataContext.GetCurrentStreamMetadata();
        TitleTextBox.Text = current.Title;
        SetStatus(string.IsNullOrWhiteSpace(current.CategoryName)
            ? "JTSAのタイトルを取り込みました。"
            : $"JTSAから取り込みました。カテゴリ: {current.CategoryName}（各サービスのカテゴリIDは別途指定）");
    }

    private async void UpdateYouTubeButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async token =>
        {
            var id = await youTubeService.UpdateMetadataAsync(
                YouTubeTokenBox.Password, YouTubeBroadcastIdBox.Text, TitleTextBox.Text,
                YouTubeCategoryIdBox.Text, token);
            YouTubeBroadcastIdBox.Text = id;
            SaveSettings();
            SetStatus("YouTubeのタイトル・カテゴリを更新しました。");
        }, "YouTube更新");
    }

    private async void UpdateKickButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async token =>
        {
            await kickService.UpdateMetadataAsync(
                KickTokenBox.Password, TitleTextBox.Text, KickCategoryIdBox.Text, token);
            SaveSettings();
            SetStatus("Kickのタイトル・カテゴリを更新しました。");
        }, "Kick更新");
    }

    private void YouTubeChatButton_Click(object sender, RoutedEventArgs e)
    {
        if (youTubeChatCancellation is not null)
        {
            youTubeChatCancellation.Cancel();
            youTubeChatCancellation = null;
            YouTubeChatButton.Content = "コメント取得を開始";
            SetStatus("YouTubeコメント取得を停止しました。");
            return;
        }
        if (!EnsureChatContext()) return;
        SaveSettings();
        youTubeChatCancellation = new CancellationTokenSource();
        YouTubeChatButton.Content = "コメント取得を停止";
        _ = RunChatAsync("YouTube", youTubeChatCancellation, token => youTubeService.PollCommentsAsync(
            YouTubeTokenBox.Password, YouTubeLiveChatIdBox.Text,
            message => chatContext!.AddExternalChatMessage(message), SetStatusThreadSafe, token));
    }

    private void KickChatButton_Click(object sender, RoutedEventArgs e)
    {
        if (kickChatCancellation is not null)
        {
            kickChatCancellation.Cancel();
            kickChatCancellation = null;
            KickChatButton.Content = "コメント取得を開始";
            SetStatus("Kickコメント取得を停止しました。");
            return;
        }
        if (!EnsureChatContext()) return;
        SaveSettings();
        kickChatCancellation = new CancellationTokenSource();
        KickChatButton.Content = "コメント取得を停止";
        _ = RunChatAsync("Kick", kickChatCancellation, token => kickService.ReceiveCommentsAsync(
            KickChannelSlugBox.Text,
            message => chatContext!.AddExternalChatMessage(message), SetStatusThreadSafe, token));
    }

    private bool EnsureChatContext()
    {
        if (chatContext is not null) return true;
        SetStatus("このJTSA本体は外部コメントの受け取りに対応していません。");
        return false;
    }

    private async Task RunChatAsync(string platform, CancellationTokenSource source, Func<CancellationToken, Task> run)
    {
        try
        {
            await run(source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        catch (Exception ex)
        {
            context.LogError($"{platform}コメント取得に失敗しました。", ex);
            SetStatusThreadSafe($"{platform}コメント取得失敗: {ex.GetBaseException().Message}");
        }
        finally
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (platform == "YouTube" && ReferenceEquals(youTubeChatCancellation, source))
                {
                    youTubeChatCancellation = null;
                    YouTubeChatButton.Content = "コメント取得を開始";
                }
                if (platform == "Kick" && ReferenceEquals(kickChatCancellation, source))
                {
                    kickChatCancellation = null;
                    KickChatButton.Content = "コメント取得を開始";
                }
                source.Dispose();
            });
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, string operation)
    {
        try
        {
            IsEnabled = false;
            SetStatus($"{operation}中…");
            using var source = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await action(source.Token);
        }
        catch (Exception ex)
        {
            context.LogError($"{operation}に失敗しました。", ex);
            SetStatus($"{operation}失敗: {ex.GetBaseException().Message}");
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;
    private void SetStatusThreadSafe(string message) => Dispatcher.BeginInvoke(() => SetStatus(message));

    private void Window_Closed(object? sender, EventArgs e)
    {
        SaveSettings();
        youTubeChatCancellation?.Cancel();
        kickChatCancellation?.Cancel();
        httpClient.Dispose();
    }
}
