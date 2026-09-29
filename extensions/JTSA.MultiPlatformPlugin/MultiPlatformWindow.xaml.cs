using JTSA.Plugin.Abstractions;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace JTSA.MultiPlatformPlugin;

public partial class MultiPlatformWindow : Window
{
    private readonly IJtsaPluginContext context;
    private readonly IJtsaStreamMetadataPluginContext? metadataContext;
    private readonly IJtsaExternalChatPluginContext? chatContext;
    private readonly nint mainWindowHandle;
    private readonly DispatcherTimer dockTimer;
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
        mainWindowHandle = context.MainWindowHandle;
        settingsPath = Path.Combine(context.DataDirectory, "platform-settings.json");
        youTubeService = new YouTubeService(httpClient);
        kickService = new KickService(httpClient);
        LoadSettings();
        ImportFromJtsa();
        if (mainWindowHandle != nint.Zero)
            new WindowInteropHelper(this) { Owner = mainWindowHandle };
        dockTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background,
            (_, _) => DockToMainWindowLeft(), Dispatcher);
        dockTimer.Stop();
        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    private void LoadSettings()
    {
        var settings = PlatformSettings.Load(settingsPath);
        YouTubeBroadcastIdBox.Text = settings.YouTubeBroadcastId;
        SetSavedCategory(YouTubeCategoryComboBox, settings.YouTubeCategoryId);
        YouTubeLiveChatIdBox.Text = settings.YouTubeLiveChatId;
        SetSavedCategory(KickCategoryComboBox, settings.KickCategoryId);
        KickChannelSlugBox.Text = settings.KickChannelSlug;
    }

    private void SaveSettings() => new PlatformSettings
    {
        YouTubeBroadcastId = YouTubeBroadcastIdBox.Text.Trim(),
        YouTubeCategoryId = YouTubeCategoryComboBox.SelectedValue?.ToString() ?? string.Empty,
        YouTubeLiveChatId = YouTubeLiveChatIdBox.Text.Trim(),
        KickCategoryId = KickCategoryComboBox.SelectedValue?.ToString() ?? string.Empty,
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
                YouTubeCategoryComboBox.SelectedValue?.ToString() ?? string.Empty, token);
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
                KickTokenBox.Password, TitleTextBox.Text,
                KickCategoryComboBox.SelectedValue?.ToString() ?? string.Empty, token);
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

    private async void LoadYouTubeCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedId = YouTubeCategoryComboBox.SelectedValue?.ToString();
        await RunAsync(async token =>
        {
            var categories = await youTubeService.GetCategoriesAsync(YouTubeTokenBox.Password, token);
            YouTubeCategoryComboBox.ItemsSource = categories;
            YouTubeCategoryComboBox.SelectedValue = selectedId;
            if (YouTubeCategoryComboBox.SelectedIndex < 0 && categories.Count > 0)
                YouTubeCategoryComboBox.SelectedIndex = 0;
            SetStatus($"YouTubeカテゴリを{categories.Count}件取得しました。");
        }, "YouTubeカテゴリ取得");
    }

    private async void SearchKickCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedId = KickCategoryComboBox.SelectedValue?.ToString();
        await RunAsync(async token =>
        {
            var categories = await kickService.SearchCategoriesAsync(
                KickTokenBox.Password, KickCategorySearchBox.Text, token);
            KickCategoryComboBox.ItemsSource = categories;
            KickCategoryComboBox.SelectedValue = selectedId;
            if (KickCategoryComboBox.SelectedIndex < 0 && categories.Count > 0)
                KickCategoryComboBox.SelectedIndex = 0;
            SetStatus(categories.Count == 0
                ? "Kickカテゴリが見つかりませんでした。"
                : $"Kickカテゴリを{categories.Count}件取得しました。");
        }, "Kickカテゴリ検索");
    }

    private static void SetSavedCategory(System.Windows.Controls.ComboBox comboBox, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        comboBox.ItemsSource = new[] { new PlatformCategory(id, $"保存済みカテゴリ") };
        comboBox.SelectedValue = id;
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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DockToMainWindowLeft();
        dockTimer.Start();
    }

    private void DockToMainWindowLeft()
    {
        if (mainWindowHandle == nint.Zero || IsIconic(mainWindowHandle) || !GetWindowRect(mainWindowHandle, out var bounds))
            return;
        var transform = HwndSource.FromHwnd(mainWindowHandle)?.CompositionTarget?.TransformFromDevice;
        var topLeft = transform?.Transform(new Point(bounds.Left, bounds.Top))
            ?? new Point(bounds.Left, bounds.Top);
        var bottomRight = transform?.Transform(new Point(bounds.Right, bounds.Bottom))
            ?? new Point(bounds.Right, bounds.Bottom);
        Top = topLeft.Y;
        Height = Math.Max(MinHeight, bottomRight.Y - topLeft.Y);
        Left = topLeft.X - ActualWidth + 1;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        dockTimer.Stop();
        SaveSettings();
        youTubeChatCancellation?.Cancel();
        kickChatCancellation?.Cancel();
        httpClient.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
