using JTSA.Plugin.Abstractions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JTSA.RoulettePlugin;

public partial class RouletteWindow : Window
{
    private const string UnsavedCandidateListName = "（未保存）";
    private readonly IJtsaPluginContext context;
    private readonly DispatcherTimer animationTimer;
    private IReadOnlyList<RouletteEntry> spinningEntries = [];
    private DateTime spinEndsUtc;
    private int finalIndex;
    private double wheelRotation;
    private long spinStartedUnixMilliseconds;
    private double spinTotalRotation;
    private double spinCruiseRotation;
    private TimeSpan spinDuration = TimeSpan.FromSeconds(4);
    private TimeSpan spinCruiseDuration = TimeSpan.FromSeconds(1);
    private TimeSpan spinSlowdownDuration = TimeSpan.FromSeconds(3);
    private bool isLoadingOverlaySettings = true;
    private string savedChannelPointRewardId = string.Empty;
    private readonly Stack<RouletteEntry> removedCandidates = new();
    private static readonly Color[] SliceColors =
    [
        Color.FromRgb(239, 71, 111), Color.FromRgb(255, 209, 102),
        Color.FromRgb(6, 214, 160), Color.FromRgb(17, 138, 178),
        Color.FromRgb(131, 56, 236), Color.FromRgb(255, 127, 80),
        Color.FromRgb(76, 201, 240), Color.FromRgb(247, 37, 133)
    ];
    private static readonly Color DefaultResultColor = Color.FromRgb(255, 209, 102);
    private Color resultColor = DefaultResultColor;
    public ObservableCollection<RouletteCandidate> Candidates { get; } =
    [
        new("候補1", 1),
        new("候補2", 1),
        new("候補3", 1)
    ];
    public ObservableCollection<ChannelPointRewardInfo> ChannelPointRewards { get; } = [];
    public ObservableCollection<RouletteCandidateList> SavedCandidateLists { get; } = [];

    public RouletteWindow(IJtsaPluginContext context)
    {
        this.context = context;
        InitializeComponent();
        LoadOverlaySettings();
        LoadCandidateLists();
        isLoadingOverlaySettings = false;
        DataContext = this;
        SavedCandidateListComboBox.SelectedIndex = 0;
        RefreshChannelPointRewards();
        context.ChannelPointRedeemed += Context_ChannelPointRedeemed;
        animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        animationTimer.Tick += AnimationTimer_Tick;
        UpdateRemainingTimeFromSetting();
        Closed += (_, _) =>
        {
            animationTimer.Stop();
            context.ChannelPointRedeemed -= Context_ChannelPointRedeemed;
            context.RemoveExpansionOverlay("roulette");
        };
        RefreshCandidateCount();
        DrawWheel(ReadEntries());
        PublishOverlay("待機中");
    }

    private void SpinButton_Click(object sender, RoutedEventArgs e)
    {
        if (animationTimer.IsEnabled) return;
        if (!TryReadSpinTiming(out spinCruiseDuration, out spinSlowdownDuration))
        {
            ResultCaptionTextBlock.Text = "時間は0より大きく120秒以下で入力してください";
            ResultTextBlock.Text = "―";
            SetResultColor(DefaultResultColor);
            return;
        }
        spinDuration = spinCruiseDuration + spinSlowdownDuration;
        spinningEntries = ReadEntries();
        if (spinningEntries.Count < 2)
        {
            ResultCaptionTextBlock.Text = "候補を2件以上入力してください";
            ResultTextBlock.Text = "―";
            SetResultColor(DefaultResultColor);
            PublishOverlay("候補不足");
            return;
        }

        Keyboard.ClearFocus();
        CandidatesEditorPanel.IsHitTestVisible = false;
        SpinButton.IsEnabled = false;
        DeleteResultAndRestartButton.IsEnabled = false;
        ResultCaptionTextBlock.Text = string.Empty;
        ResultTextBlock.Text = string.Empty;
        SetResultColor(DefaultResultColor);
        ElapsedSpinTimeTextBlock.Text = $"残り時間 {spinDuration.TotalSeconds:0.0} 秒";
        spinEndsUtc = DateTime.UtcNow + spinDuration;
        finalIndex = ChooseWeightedIndex(spinningEntries);
        var totalWeight = spinningEntries.Sum(entry => entry.Weight);
        var weightBeforeResult = spinningEntries.Take(finalIndex).Sum(entry => entry.Weight);
        var positionWithinSlice = 0.01 + Random.Shared.NextDouble() * 0.98;
        var selectedWeightPosition = weightBeforeResult +
                                     spinningEntries[finalIndex].Weight * positionWithinSlice;
        wheelRotation = 360 - selectedWeightPosition * 360 / totalWeight;
        const double turnsPerSecond = 4;
        spinCruiseRotation = 360 * turnsPerSecond * spinCruiseDuration.TotalSeconds;
        var idealTotalRotation = spinCruiseRotation +
                                 360 * turnsPerSecond * spinSlowdownDuration.TotalSeconds;
        var totalWholeTurns = Math.Max(
            Math.Ceiling(spinCruiseRotation / 360),
            Math.Round((idealTotalRotation - wheelRotation) / 360));
        spinTotalRotation = 360 * totalWholeTurns + wheelRotation;
        spinStartedUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        animationTimer.Start();
        DrawWheel(spinningEntries);
        PublishOverlay(string.Empty);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        var remaining = Math.Clamp(
            (spinEndsUtc - DateTime.UtcNow).TotalSeconds,
            0,
            spinDuration.TotalSeconds);
        ElapsedSpinTimeTextBlock.Text = $"残り時間 {remaining:0.0} 秒";
        if (DateTime.UtcNow < spinEndsUtc)
            return;

        animationTimer.Stop();
        ElapsedSpinTimeTextBlock.Text = "残り時間 0.0 秒";
        var result = spinningEntries[finalIndex].Text;
        ResultCaptionTextBlock.Text = string.Empty;
        ResultTextBlock.Text = result;
        SetResultColor(SliceColors[finalIndex % SliceColors.Length]);
        DrawWheel(spinningEntries);
        CandidatesEditorPanel.IsHitTestVisible = true;
        SpinButton.IsEnabled = true;
        DeleteResultAndRestartButton.IsEnabled = true;
        PublishOverlay(string.Empty);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        animationTimer.Stop();
        CandidatesEditorPanel.IsHitTestVisible = true;
        SpinButton.IsEnabled = true;
        ResultCaptionTextBlock.Text = string.Empty;
        ResultTextBlock.Text = "―";
        SetResultColor(DefaultResultColor);
        UpdateRemainingTimeFromSetting();
        DeleteResultAndRestartButton.IsEnabled = false;
        wheelRotation = 0;
        DrawWheel(ReadEntries());
        PublishOverlay("待機中");
    }

    private void DeleteResultAndRestartButton_Click(object sender, RoutedEventArgs e)
    {
        if (animationTimer.IsEnabled) return;

        var result = ResultTextBlock.Text.Trim();
        var candidate = Candidates.FirstOrDefault(item =>
            string.Equals(item.Text.Trim(), result, StringComparison.CurrentCulture));
        if (candidate is null) return;

        removedCandidates.Push(new RouletteEntry(candidate.Text, candidate.Weight));
        Candidates.Remove(candidate);
        RestoreRemovedCandidateButton.IsEnabled = true;
        RefreshCandidates();
        ClearButton_Click(sender, e);
        SpinButton_Click(sender, e);
    }

    private void RestoreRemovedCandidateButton_Click(object sender, RoutedEventArgs e)
    {
        if (removedCandidates.Count == 0) return;

        var restored = removedCandidates.Pop();
        if (!Candidates.Any(candidate => string.Equals(
                candidate.Text.Trim(), restored.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            Candidates.Add(new RouletteCandidate(restored.Text, restored.Weight));

        RestoreRemovedCandidateButton.IsEnabled = removedCandidates.Count > 0;
        RefreshCandidates();
        CandidatesListBox.ScrollIntoView(Candidates[^1]);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void CandidateTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CandidateCountTextBlock is not null)
        {
            RefreshCandidateCount();
            if (animationTimer is null || !animationTimer.IsEnabled)
            {
                wheelRotation = 0;
                DrawWheel(ReadEntries());
            }
        }
    }

    private void CandidateWeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (animationTimer?.IsEnabled == true || sender is not TextBox textBox ||
            !int.TryParse(textBox.Text, out var weight) || weight < 1)
            return;

        _ = Dispatcher.InvokeAsync(
            () =>
            {
                RefreshCandidates();
                PublishOverlay(ResultCaptionTextBlock?.Text ?? "待機中");
            },
            DispatcherPriority.Background);
    }

    private void AddCandidateButton_Click(object sender, RoutedEventArgs e)
    {
        Candidates.Add(new RouletteCandidate(string.Empty, 1));
        RefreshCandidates();
        CandidatesListBox.ScrollIntoView(Candidates[^1]);
    }

    private void RemoveCandidateButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RouletteCandidate candidate) return;
        Candidates.Remove(candidate);
        RefreshCandidates();
    }

    private void ChannelPointRewardComboBox_DropDownOpened(object sender, EventArgs e) =>
        RefreshChannelPointRewards();

    private void ChannelPointRewardComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoadingOverlaySettings) return;
        savedChannelPointRewardId = ChannelPointRewardComboBox.SelectedValue as string ?? string.Empty;
        SaveOverlaySettings();
    }

    private void RefreshChannelPointRewards()
    {
        var selectedId = ChannelPointRewardComboBox.SelectedValue as string ?? savedChannelPointRewardId;
        ChannelPointRewards.Clear();
        ChannelPointRewards.Add(new ChannelPointRewardInfo(string.Empty, "使用しない", false));
        foreach (var reward in context.GetChannelPointRewards())
            ChannelPointRewards.Add(reward);

        ChannelPointRewardComboBox.SelectedValue = selectedId;
        if (ChannelPointRewardComboBox.SelectedIndex < 0)
            ChannelPointRewardComboBox.SelectedIndex = 0;
    }

    private void Context_ChannelPointRedeemed(ChannelPointRedemptionInfo redemption)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (ChannelPointRewardComboBox.SelectedItem is not ChannelPointRewardInfo reward ||
                string.IsNullOrWhiteSpace(reward.Id) ||
                !string.Equals(reward.Id, redemption.RewardId, StringComparison.Ordinal))
                return;

            var candidateText = (reward.IsUserInputRequired
                    ? redemption.UserInput
                    : redemption.UserName)
                .Trim();
            if (string.IsNullOrWhiteSpace(candidateText) ||
                Candidates.Any(candidate => string.Equals(
                    candidate.Text.Trim(), candidateText, StringComparison.CurrentCultureIgnoreCase)))
                return;

            Candidates.Add(new RouletteCandidate(candidateText, 1));
            RefreshCandidates();
            CandidatesListBox.ScrollIntoView(Candidates[^1]);
        });
    }

    private void SaveCandidateListButton_Click(object sender, RoutedEventArgs e)
    {
        var name = SavedCandidateListComboBox.Text.Trim();
        var entries = ReadEntries().ToArray();
        if (string.IsNullOrWhiteSpace(name) || name == UnsavedCandidateListName || entries.Length == 0) return;

        var savedList = new RouletteCandidateList(
            name,
            entries.Select(entry => entry.Text).ToArray(),
            entries.Select(entry => entry.Weight).ToArray());
        var existingIndex = SavedCandidateLists
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => string.Equals(
                pair.item.Name, name, StringComparison.CurrentCultureIgnoreCase))
            .index;
        var existing = SavedCandidateLists.FirstOrDefault(item => string.Equals(
            item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is null)
            SavedCandidateLists.Add(savedList);
        else
            SavedCandidateLists[existingIndex] = savedList;

        SavedCandidateListComboBox.SelectedItem = savedList;
        SaveCandidateLists();
    }

    private void LoadCandidateListButton_Click(object sender, RoutedEventArgs e)
    {
        var savedList = FindSelectedCandidateList();
        if (savedList is null) return;

        Candidates.Clear();
        for (var index = 0; index < savedList.Candidates.Length; index++)
        {
            var entry = savedList.Candidates[index];
            if (!string.IsNullOrWhiteSpace(entry))
                Candidates.Add(new RouletteCandidate(
                    entry,
                    savedList.Weights is { } weights && index < weights.Length
                        ? Math.Max(1, weights[index])
                        : 1));
        }
        RefreshCandidates();
    }

    private void DeleteCandidateListButton_Click(object sender, RoutedEventArgs e)
    {
        var savedList = FindSelectedCandidateList();
        if (savedList is null) return;

        SavedCandidateLists.Remove(savedList);
        SavedCandidateListComboBox.SelectedIndex = -1;
        SavedCandidateListComboBox.Text = string.Empty;
        SaveCandidateLists();
    }

    private RouletteCandidateList? FindSelectedCandidateList()
    {
        if (SavedCandidateListComboBox.SelectedItem is RouletteCandidateList selected)
            return selected.Name == UnsavedCandidateListName ? null : selected;

        var name = SavedCandidateListComboBox.Text.Trim();
        return SavedCandidateLists.FirstOrDefault(item => string.Equals(
            item.Name, name, StringComparison.CurrentCultureIgnoreCase));
    }

    private void LoadCandidateLists()
    {
        SavedCandidateLists.Add(new RouletteCandidateList(UnsavedCandidateListName, [], []));
        var path = IOPath.Combine(context.DataDirectory, "roulette-candidate-lists.json");
        if (!IOFile.Exists(path)) return;

        try
        {
            var lists = JsonSerializer.Deserialize<List<RouletteCandidateList>>(IOFile.ReadAllText(path)) ?? [];
            foreach (var list in lists.Where(item =>
                         !string.IsNullOrWhiteSpace(item.Name) && item.Candidates.Length > 0))
                SavedCandidateLists.Add(list);
        }
        catch (Exception ex)
        {
            context.LogError("ルーレットの候補リストを読み込めませんでした。", ex);
        }
    }

    private void SaveCandidateLists()
    {
        try
        {
            var path = IOPath.Combine(context.DataDirectory, "roulette-candidate-lists.json");
            IOFile.WriteAllText(path, JsonSerializer.Serialize(
                SavedCandidateLists.Where(item => item.Name != UnsavedCandidateListName)));
        }
        catch (Exception ex)
        {
            context.LogError("ルーレットの候補リストを保存できませんでした。", ex);
        }
    }

    private void RefreshCandidates()
    {
        RefreshCandidateCount();
        if (animationTimer is null || !animationTimer.IsEnabled)
        {
            wheelRotation = 0;
            DrawWheel(ReadEntries());
        }
    }

    private void ShowInExpansionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoadingOverlaySettings) return;
        PublishOverlay(ResultCaptionTextBlock?.Text ?? "待機中");
    }

    private void OverlayBoundsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isLoadingOverlaySettings) return;
        if (OverlayXTextBox is null || OverlayYTextBox is null || OverlayScaleTextBox is null)
            return;

        if (TryReadOverlayBounds(out _, out _, out _, out _))
        {
            SaveOverlaySettings();
            PublishOverlay(ResultCaptionTextBlock?.Text ?? "待機中");
        }
    }

    private void SpinTimingTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isLoadingOverlaySettings || SpinDurationSecondsTextBox is null)
            return;
        if (TryReadSpinTiming(out var cruise, out var slowdown))
        {
            SaveOverlaySettings();
            if (animationTimer?.IsEnabled != true)
                ElapsedSpinTimeTextBlock.Text = $"残り時間 {(cruise + slowdown).TotalSeconds:0.0} 秒";
        }
    }

    private void UpdateRemainingTimeFromSetting()
    {
        if (ElapsedSpinTimeTextBlock is null ||
            !TryReadSpinTiming(out var cruise, out var slowdown))
            return;
        ElapsedSpinTimeTextBlock.Text = $"残り時間 {(cruise + slowdown).TotalSeconds:0.0} 秒";
    }

    private bool TryReadSpinTiming(out TimeSpan cruiseDuration, out TimeSpan slowdownDuration)
    {
        var valid = double.TryParse(
            SpinDurationSecondsTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var totalSeconds) &&
                    totalSeconds is > 0 and <= 120;
        var normalizedSeconds = valid ? totalSeconds : 0;
        cruiseDuration = TimeSpan.FromSeconds(normalizedSeconds * 0.25);
        slowdownDuration = TimeSpan.FromSeconds(normalizedSeconds * 0.75);
        return valid;
    }

    private void LoadOverlaySettings()
    {
        var path = IOPath.Combine(context.DataDirectory, "roulette-settings.json");
        if (!IOFile.Exists(path)) return;

        try
        {
            var settings = JsonSerializer.Deserialize<RouletteOverlaySettings>(IOFile.ReadAllText(path));
            if (settings is null || settings.X < 0 || settings.Y < 0 ||
                settings.ScalePercent is < 25 or > 200 ||
                (settings.SpinSeconds ?? settings.CruiseSeconds + settings.SlowdownSeconds) is <= 0 or > 120)
                return;

            OverlayXTextBox.Text = settings.X.ToString();
            OverlayYTextBox.Text = settings.Y.ToString();
            OverlayScaleTextBox.Text = settings.ScalePercent.ToString();
            SpinDurationSecondsTextBox.Text = (settings.SpinSeconds ??
                settings.CruiseSeconds + settings.SlowdownSeconds).ToString(CultureInfo.CurrentCulture);
            savedChannelPointRewardId = settings.ChannelPointRewardId ?? string.Empty;
        }
        catch (Exception ex)
        {
            context.LogError("ルーレットの表示設定を読み込めませんでした。", ex);
        }
    }

    private void SaveOverlaySettings()
    {
        if (!int.TryParse(OverlayXTextBox.Text, out var x) ||
            !int.TryParse(OverlayYTextBox.Text, out var y) ||
            !int.TryParse(OverlayScaleTextBox.Text, out var scalePercent) ||
            !TryReadSpinTiming(out var cruiseDuration, out var slowdownDuration))
            return;

        try
        {
            var path = IOPath.Combine(context.DataDirectory, "roulette-settings.json");
            IOFile.WriteAllText(path, JsonSerializer.Serialize(new RouletteOverlaySettings(
                x, y, scalePercent, savedChannelPointRewardId,
                cruiseDuration.TotalSeconds, slowdownDuration.TotalSeconds,
                cruiseDuration.TotalSeconds + slowdownDuration.TotalSeconds)));
        }
        catch (Exception ex)
        {
            context.LogError("ルーレットの表示設定を保存できませんでした。", ex);
        }
    }

    private bool TryReadOverlayBounds(out int x, out int y, out int width, out int height)
    {
        const int baseWidth = 520;
        const int baseHeight = 720;
        var hasX = int.TryParse(OverlayXTextBox.Text, out x);
        var hasY = int.TryParse(OverlayYTextBox.Text, out y);
        var hasScale = int.TryParse(OverlayScaleTextBox.Text, out var scalePercent);
        width = (int)Math.Round(baseWidth * scalePercent / 100d);
        height = (int)Math.Round(baseHeight * scalePercent / 100d);
        return hasX && hasY && hasScale && x >= 0 && y >= 0 && scalePercent is >= 25 and <= 200;
    }

    private IReadOnlyList<RouletteEntry> ReadEntries() => Candidates
        .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
        .GroupBy(candidate => candidate.Text.Trim(), StringComparer.CurrentCulture)
        .Select(group => new RouletteEntry(group.Key, Math.Max(1, group.First().Weight)))
        .ToArray();

    private void RefreshCandidateCount()
    {
        var entries = ReadEntries();
        CandidateCountTextBlock.Text = $"候補: {entries.Count}件";
        var totalWeight = entries.Sum(entry => entry.Weight);
        foreach (var candidate in Candidates)
        {
            var entry = entries.FirstOrDefault(item => string.Equals(
                item.Text, candidate.Text.Trim(), StringComparison.CurrentCulture));
            var probability = entry is null || totalWeight == 0
                ? 0
                : entry.Weight * 100d / totalWeight;
            candidate.ProbabilityText = $"（{probability:0.0}%）";
        }
    }

    private async void DrawWheel(IReadOnlyList<RouletteEntry> entries)
    {
        if (WheelBrowser is null) return;

        try
        {
            await WheelBrowser.EnsureCoreWebView2Async();
            WheelBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WheelBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WheelBrowser.NavigateToString(CreateWheelHtml(entries));
        }
        catch (Exception ex)
        {
            context.LogError("アプリ内ルーレットのブラウザ表示を初期化できませんでした。", ex);
        }
    }

    private string CreateWheelHtml(IReadOnlyList<RouletteEntry> entries)
    {
        var sliceCount = Math.Max(1, entries.Count);
        var totalWeight = entries.Count == 0 ? 1 : entries.Sum(entry => entry.Weight);
        var gradientWeight = 0;
        var gradient = entries.Count == 0
            ? "rgb(70,70,70) 0deg 360deg"
            : string.Join(",", Enumerable.Range(0, sliceCount).Select(index =>
            {
                var color = SliceColors[index % SliceColors.Length];
                var start = gradientWeight * 360d / totalWeight;
                gradientWeight += entries[index].Weight;
                var end = gradientWeight * 360d / totalWeight;
                return $"rgb({color.R},{color.G},{color.B}) {start:0.###}deg {end:0.###}deg";
            }));
        var labelWeight = 0;
        var labels = string.Concat(entries.Select(entry =>
            {
                var angle = (labelWeight + entry.Weight / 2d) * 360 / totalWeight;
                labelWeight += entry.Weight;
                var label = FormatWheelLabel(entry.Text);
                var sliceAngle = entry.Weight * 360d / totalWeight;
                var labelFontSize = Math.Clamp(sliceAngle * 0.45, 8, 22);
                return $"<div style='position:absolute;inset:0;transform:rotate({angle:0.###}deg)'><span style='position:absolute;left:50%;top:44px;width:170px;margin-left:-85px;text-align:center;white-space:normal;overflow-wrap:anywhere;line-height:1.05;transform:rotate(-90deg);font-size:{labelFontSize:0.##}px;font-weight:800;color:#373737'>{label}</span></div>";
            }));
        var isSpinning = animationTimer?.IsEnabled == true;
        var elapsedMilliseconds = Math.Clamp(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - spinStartedUnixMilliseconds,
            0,
            (long)spinDuration.TotalMilliseconds);
        var animationName = $"wheel-{spinStartedUnixMilliseconds}";
        var wheelStyle = isSpinning
            ? $"animation:{animationName} {spinDuration.TotalMilliseconds:0}ms linear {-elapsedMilliseconds}ms forwards;"
            : $"transform:rotate({wheelRotation:0.###}deg);";
        var keyframes = isSpinning ? CreateCosineKeyframes(animationName, spinTotalRotation) : string.Empty;

        return $$"""
            <!doctype html><html><head><meta charset="utf-8"><style>
            *{box-sizing:border-box}html,body{width:100%;height:100%;margin:0;overflow:hidden;background:transparent}
            body{display:flex;align-items:flex-start;justify-content:center;font-family:'Segoe UI',sans-serif}
            #stage{position:relative;width:430px;height:438px;flex:0 0 430px;transform:scale(.7209302326);transform-origin:top center}
            #pointer{position:absolute;z-index:3;left:195px;top:0;width:0;height:0;border-left:20px solid transparent;border-right:20px solid transparent;border-top:42px solid #ffd166;filter:drop-shadow(0 3px 3px #000)}
            #wheel{position:absolute;left:15px;right:15px;top:23px;bottom:15px;border-radius:50%;background:conic-gradient(from 0deg,{{gradient}});{{wheelStyle}}}
            #hub{position:absolute;left:50%;top:50%;width:66px;height:66px;transform:translate(-50%,-50%);border-radius:50%;background:#555}
            {{keyframes}}
            </style></head><body><div id="stage"><div id="pointer"></div><div id="wheel">{{labels}}<div id="hub"></div></div></div></body></html>
            """;
    }

    private static string CreateCosineKeyframes(string animationName, double totalRotation) =>
        $"@keyframes {animationName}{{" +
        string.Concat(Enumerable.Range(0, 101).Select(percent =>
        {
            var progress = percent / 100d;
            const double switchTime = 0.7;
            const double switchProgress = 0.95;
            const double transitionSlope = 0.2;
            double cosineAxisProgress;
            if (progress <= switchTime)
            {
                var segmentProgress = progress / switchTime;
                cosineAxisProgress = CubicHermite(
                    0, switchProgress,
                    switchProgress,
                    transitionSlope * switchTime,
                    segmentProgress);
            }
            else
            {
                var segmentDuration = 1 - switchTime;
                var segmentProgress = (progress - switchTime) / segmentDuration;
                cosineAxisProgress = CubicHermite(
                    switchProgress, 1,
                    transitionSlope * segmentDuration,
                    0,
                    segmentProgress);
            }
            var cosineProgress = Math.Sin(cosineAxisProgress * Math.PI / 2);
            var rotation = totalRotation * cosineProgress;
            return FormattableString.Invariant($"{percent}%{{transform:rotate({rotation:0.###}deg)}}");
        })) + "}";

    private static double CubicHermite(
        double start, double end, double startTangent, double endTangent, double progress)
    {
        var progress2 = progress * progress;
        var progress3 = progress2 * progress;
        return (2 * progress3 - 3 * progress2 + 1) * start +
               (progress3 - 2 * progress2 + progress) * startTangent +
               (-2 * progress3 + 3 * progress2) * end +
               (progress3 - progress2) * endTangent;
    }

    private static int ChooseWeightedIndex(IReadOnlyList<RouletteEntry> entries)
    {
        var selectedWeight = Random.Shared.Next(entries.Sum(entry => entry.Weight));
        for (var index = 0; index < entries.Count; index++)
        {
            selectedWeight -= entries[index].Weight;
            if (selectedWeight < 0) return index;
        }
        return entries.Count - 1;
    }

    private static string FormatWheelLabel(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= 8)
            return WebUtility.HtmlEncode(trimmed);

        var center = trimmed.Length / 2;
        var splitCandidates = Enumerable.Range(1, trimmed.Length - 1)
            .Where(index =>
                char.IsWhiteSpace(trimmed[index]) ||
                char.IsWhiteSpace(trimmed[index - 1]) ||
                (char.IsLower(trimmed[index - 1]) && char.IsUpper(trimmed[index])))
            .ToList();
        var splitIndex = splitCandidates.Count > 0
            ? splitCandidates.MinBy(index => Math.Abs(index - center))
            : center;

        var firstLine = trimmed[..splitIndex].TrimEnd();
        var secondLine = trimmed[splitIndex..].TrimStart();
        return $"{WebUtility.HtmlEncode(firstLine)}<br>{WebUtility.HtmlEncode(secondLine)}";
    }

    private void SetResultColor(Color color)
    {
        resultColor = color;
        ResultTextBlock.Foreground = new SolidColorBrush(color);
    }

    private void PublishOverlay(string caption)
    {
        if (ShowInExpansionCheckBox?.IsChecked != true)
        {
            context.RemoveExpansionOverlay("roulette");
            return;
        }

        if (!TryReadOverlayBounds(out var overlayX, out var overlayY, out var overlayWidth, out var overlayHeight))
            return;

        const double baseWheelSize = 430;
        const double baseOverlayWidth = 520;
        const double baseOverlayHeight = 720;
        var displayScale = overlayWidth / baseOverlayWidth;
        var wheelSize = Math.Min(baseOverlayWidth - 48, baseOverlayHeight - 220);
        var wheelScale = wheelSize / baseWheelSize;

        var entries = animationTimer?.IsEnabled == true ? spinningEntries : ReadEntries();
        var sliceCount = Math.Max(1, entries.Count);
        var totalEntryWeight = entries.Count == 0 ? 1 : entries.Sum(entry => entry.Weight);
        var gradientWeight = 0;
        var gradient = string.Join(",", Enumerable.Range(0, sliceCount).Select(index =>
        {
            var color = SliceColors[index % SliceColors.Length];
            var start = gradientWeight * 360d / totalEntryWeight;
            gradientWeight += entries.Count == 0 ? 1 : entries[index].Weight;
            var end = gradientWeight * 360d / totalEntryWeight;
            return $"rgb({color.R},{color.G},{color.B}) {start:0.###}deg {end:0.###}deg";
        }));
        var isSpinning = animationTimer?.IsEnabled == true;
        var labelWeight = 0;
        var labels = string.Concat(entries.Select(entry =>
            {
                var angle = (labelWeight + entry.Weight / 2d) * 360 / totalEntryWeight;
                labelWeight += entry.Weight;
                var label = FormatWheelLabel(entry.Text);
                var sliceAngle = entry.Weight * 360d / totalEntryWeight;
                var labelFontSize = Math.Clamp(sliceAngle * 0.45, 8, 22);
                return $"<div style=\"position:absolute;inset:0;transform:rotate({angle}deg)\"><span style=\"position:absolute;left:50%;top:44px;width:170px;margin-left:-85px;text-align:center;white-space:normal;overflow-wrap:anywhere;line-height:1.05;writing-mode:horizontal-tb;transform:rotate(-90deg);font-size:{labelFontSize:0.##}px;font-weight:800;color:#373737;text-shadow:none\">{label}</span></div>";
            }));
        var wheelAnimationData = isSpinning
            ? $"data-jtsa-animation-start=\"{spinStartedUnixMilliseconds}\""
            : string.Empty;
        var animationName = $"jtsa-roulette-{spinStartedUnixMilliseconds}";
        var animationStyle = isSpinning
            ? $"animation:{animationName} {spinDuration.TotalMilliseconds:0}ms linear 0ms forwards;"
            : $"transform:rotate({wheelRotation:0.###}deg);";
        var animationDefinition = isSpinning
            ? $"<style>{CreateCosineKeyframes(animationName, spinTotalRotation)}</style>"
            : string.Empty;
        var encodedCaption = WebUtility.HtmlEncode(caption);
        var result = ResultTextBlock?.Text ?? "―";
        var encodedResult = WebUtility.HtmlEncode(result);
        var resultCssColor = $"#{resultColor.R:X2}{resultColor.G:X2}{resultColor.B:X2}";
        var resultFontSize = Math.Clamp(360d / Math.Max(1, result.Length), 20, 58);
        context.SetExpansionOverlay(new ExpansionOverlayContent(
            "roulette",
            $"{animationDefinition}<div style=\"box-sizing:border-box;width:{baseOverlayWidth:0}px;height:{baseOverlayHeight:0}px;transform:scale({displayScale:0.#####});transform-origin:top left;padding:24px;display:flex;flex-direction:column;align-items:center;justify-content:flex-start;gap:18px;color:white;font-family:'Segoe UI',sans-serif;text-shadow:0 3px 8px #000;overflow:hidden\"><div style=\"position:relative;width:{wheelSize:0.###}px;height:{wheelSize:0.###}px;flex:0 0 {wheelSize:0.###}px\"><div style=\"position:absolute;left:0;top:0;width:430px;height:430px;transform:scale({wheelScale:0.#####});transform-origin:top left\"><div style=\"position:absolute;z-index:3;left:195px;top:-8px;width:0;height:0;border-left:20px solid transparent;border-right:20px solid transparent;border-top:42px solid #ffd166;filter:drop-shadow(0 3px 3px #000)\"></div><div {wheelAnimationData} style=\"position:absolute;inset:15px;border-radius:50%;background:conic-gradient(from 0deg,{gradient});{animationStyle}\">{labels}<div style=\"position:absolute;left:50%;top:50%;width:66px;height:66px;transform:translate(-50%,-50%);border-radius:50%;background:#555\"></div></div></div></div><div style=\"box-sizing:border-box;width:430px;height:150px;flex:0 0 150px;padding:16px 26px;background:rgba(20,20,20,.86);border:2px solid rgba(255,255,255,.42);border-radius:20px;text-align:center;display:flex;flex-direction:column;align-items:center;justify-content:center\"><div style=\"font-size:24px;line-height:1.2;color:#ddd\">{encodedCaption}</div><div style=\"width:100%;font-size:{resultFontSize:0.##}px;line-height:1.1;font-weight:800;color:{resultCssColor};margin-top:8px;white-space:nowrap;overflow:hidden\">{encodedResult}</div></div></div>",
            overlayX, overlayY, overlayWidth, overlayHeight));
    }
}

public sealed class RouletteCandidate(string text, int weight) : INotifyPropertyChanged
{
    private int weight = Math.Max(1, weight);
    private string probabilityText = "（0.0%）";
    public string Text { get; set; } = text;
    public int Weight
    {
        get => weight;
        set
        {
            weight = Math.Max(1, value);
            Changed();
        }
    }
    public string ProbabilityText
    {
        get => probabilityText;
        set
        {
            probabilityText = value;
            Changed();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record RouletteEntry(string Text, int Weight);

public sealed record RouletteOverlaySettings(
    int X,
    int Y,
    int ScalePercent,
    string? ChannelPointRewardId = null,
    double CruiseSeconds = 1,
    double SlowdownSeconds = 3,
    double? SpinSeconds = null);

public sealed record RouletteCandidateList(
    string Name,
    string[] Candidates,
    int[]? Weights = null);
