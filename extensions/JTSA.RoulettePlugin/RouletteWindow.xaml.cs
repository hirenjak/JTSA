using JTSA.Plugin.Abstractions;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace JTSA.RoulettePlugin;

public partial class RouletteWindow : Window
{
    private static readonly TimeSpan SpinDuration = TimeSpan.FromSeconds(4);
    private readonly IJtsaPluginContext context;
    private readonly DispatcherTimer animationTimer;
    private IReadOnlyList<string> spinningEntries = [];
    private DateTime spinEndsUtc;
    private int animationIndex;
    private int finalIndex;
    private double wheelRotation;
    private long spinStartedUnixMilliseconds;
    private double spinTotalRotation;
    private static readonly Color[] SliceColors =
    [
        Color.FromRgb(239, 71, 111), Color.FromRgb(255, 209, 102),
        Color.FromRgb(6, 214, 160), Color.FromRgb(17, 138, 178),
        Color.FromRgb(131, 56, 236), Color.FromRgb(255, 127, 80),
        Color.FromRgb(76, 201, 240), Color.FromRgb(247, 37, 133)
    ];

    public RouletteWindow(IJtsaPluginContext context)
    {
        this.context = context;
        InitializeComponent();
        animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        animationTimer.Tick += AnimationTimer_Tick;
        Closed += (_, _) =>
        {
            animationTimer.Stop();
            context.RemoveExpansionOverlay("roulette");
        };
        RefreshCandidateCount();
        DrawWheel(ReadEntries());
        PublishOverlay("待機中");
    }

    private void SpinButton_Click(object sender, RoutedEventArgs e)
    {
        if (animationTimer.IsEnabled) return;
        spinningEntries = ReadEntries();
        if (spinningEntries.Count < 2)
        {
            ResultCaptionTextBlock.Text = "候補を2件以上入力してください";
            ResultTextBlock.Text = "―";
            PublishOverlay("候補不足");
            return;
        }

        EntriesTextBox.IsEnabled = false;
        SpinButton.IsEnabled = false;
        ResultCaptionTextBlock.Text = string.Empty;
        ResultTextBlock.Text = string.Empty;
        spinEndsUtc = DateTime.UtcNow + SpinDuration;
        animationIndex = Random.Shared.Next(spinningEntries.Count);
        finalIndex = Random.Shared.Next(spinningEntries.Count);
        wheelRotation = 360 - ((finalIndex + 0.5) * 360 / spinningEntries.Count);
        spinTotalRotation = 360 * 7 + wheelRotation;
        spinStartedUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DrawWheel(spinningEntries);
        var rotation = new RotateTransform();
        WheelCanvas.RenderTransform = rotation;
        var rotationAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(SpinDuration),
            FillBehavior = FillBehavior.HoldEnd
        };
        rotationAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
            spinTotalRotation,
            KeyTime.FromTimeSpan(SpinDuration),
            new KeySpline(.22, 1, .36, 1)));
        rotation.BeginAnimation(RotateTransform.AngleProperty, rotationAnimation);
        animationTimer.Start();
        PublishOverlay(string.Empty);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow < spinEndsUtc)
        {
            return;
        }

        animationTimer.Stop();
        var result = spinningEntries[finalIndex];
        ResultCaptionTextBlock.Text = "抽選結果";
        ResultTextBlock.Text = result;
        animationIndex = finalIndex;
        DrawWheel(spinningEntries);
        EntriesTextBox.IsEnabled = true;
        SpinButton.IsEnabled = true;
        PublishOverlay("抽選結果");
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        animationTimer.Stop();
        WheelCanvas.RenderTransform = Transform.Identity;
        EntriesTextBox.IsEnabled = true;
        SpinButton.IsEnabled = true;
        ResultCaptionTextBlock.Text = "抽選結果";
        ResultTextBlock.Text = "―";
        wheelRotation = 0;
        DrawWheel(ReadEntries());
        PublishOverlay("待機中");
    }

    private void EntriesTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (CandidateCountTextBlock is not null)
        {
            RefreshCandidateCount();
            if (animationTimer is null || !animationTimer.IsEnabled)
            {
                wheelRotation = 0;
                WheelCanvas.RenderTransform = Transform.Identity;
                DrawWheel(ReadEntries());
            }
        }
    }

    private void ShowInExpansionCheckBox_Changed(object sender, RoutedEventArgs e) => PublishOverlay(ResultCaptionTextBlock?.Text ?? "待機中");

    private IReadOnlyList<string> ReadEntries() => EntriesTextBox.Text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.CurrentCulture)
        .ToArray();

    private void RefreshCandidateCount() => CandidateCountTextBlock.Text = $"候補: {ReadEntries().Count}件";

    private void DrawWheel(IReadOnlyList<string> entries)
    {
        if (WheelCanvas is null) return;
        WheelCanvas.Children.Clear();
        const double center = 155;
        const double radius = 142;

        if (entries.Count == 0)
        {
            WheelCanvas.Children.Add(new Ellipse
            {
                Width = radius * 2, Height = radius * 2,
                Fill = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Stroke = Brushes.White, StrokeThickness = 3
            });
            Canvas.SetLeft(WheelCanvas.Children[^1], center - radius);
            Canvas.SetTop(WheelCanvas.Children[^1], center - radius);
        }
        else
        {
            var sliceAngle = 360d / entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var start = -90 + i * sliceAngle;
                var end = start + sliceAngle;
                var path = CreateSlice(center, center, radius, start, end);
                path.Fill = new SolidColorBrush(SliceColors[i % SliceColors.Length]);
                path.Stroke = new SolidColorBrush(Color.FromArgb(170, 30, 30, 30));
                path.StrokeThickness = 2;
                WheelCanvas.Children.Add(path);

                if (entries.Count <= 16)
                {
                    var angle = (start + end) / 2 * Math.PI / 180;
                    var label = new TextBlock
                    {
                        Text = entries[i].Length > 9 ? entries[i][..9] + "…" : entries[i],
                        Foreground = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                        FontWeight = FontWeights.Bold,
                        FontSize = entries.Count > 10 ? 14 : 17,
                        TextAlignment = TextAlignment.Center,
                        Width = 90,
                        RenderTransformOrigin = new Point(.5, .5),
                        RenderTransform = new RotateTransform((start + end) / 2)
                    };
                    Canvas.SetLeft(label, center + Math.Cos(angle) * radius * .62 - 45);
                    Canvas.SetTop(label, center + Math.Sin(angle) * radius * .62 - 9);
                    WheelCanvas.Children.Add(label);
                }
            }
        }

        var hub = new Ellipse { Width = 32, Height = 32, Fill = Brushes.White, Stroke = Brushes.DimGray, StrokeThickness = 3 };
        Canvas.SetLeft(hub, center - 16); Canvas.SetTop(hub, center - 16); WheelCanvas.Children.Add(hub);
    }

    private static Path CreateSlice(double centerX, double centerY, double radius, double startDegrees, double endDegrees)
    {
        Point PointAt(double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return new Point(centerX + radius * Math.Cos(radians), centerY + radius * Math.Sin(radians));
        }

        var figure = new PathFigure { StartPoint = new Point(centerX, centerY), IsClosed = true };
        figure.Segments.Add(new LineSegment(PointAt(startDegrees), true));
        figure.Segments.Add(new ArcSegment(
            PointAt(endDegrees), new Size(radius, radius), 0,
            endDegrees - startDegrees > 180, SweepDirection.Clockwise, true));
        return new Path { Data = new PathGeometry([figure]) };
    }

    private void PublishOverlay(string caption)
    {
        if (ShowInExpansionCheckBox?.IsChecked != true)
        {
            context.RemoveExpansionOverlay("roulette");
            return;
        }

        var entries = animationTimer?.IsEnabled == true ? spinningEntries : ReadEntries();
        var sliceCount = Math.Max(1, entries.Count);
        var gradient = string.Join(",", Enumerable.Range(0, sliceCount).Select(index =>
        {
            var color = SliceColors[index % SliceColors.Length];
            var start = index * 360d / sliceCount;
            var end = (index + 1) * 360d / sliceCount;
            return $"rgb({color.R},{color.G},{color.B}) {start:0.###}deg {end:0.###}deg";
        }));
        var isSpinning = animationTimer?.IsEnabled == true;
        var labels = entries.Count <= 16
            ? string.Concat(entries.Select((entry, index) =>
            {
                var angle = (index + .5) * 360 / sliceCount;
                var label = WebUtility.HtmlEncode(entry.Length > 12 ? entry[..12] + "…" : entry);
                return $"<div style=\"position:absolute;inset:0;transform:rotate({angle}deg)\"><span style=\"position:absolute;left:50%;top:34px;width:170px;margin-left:-85px;text-align:center;white-space:nowrap;writing-mode:horizontal-tb;transform:rotate(-90deg);font-size:22px;font-weight:800;color:#373737;text-shadow:none\">{label}</span></div>";
            }))
            : string.Empty;
        var wheelAnimationData = isSpinning
            ? $"data-jtsa-spin-start=\"{spinStartedUnixMilliseconds}\" data-jtsa-spin-duration=\"{SpinDuration.TotalMilliseconds:0}\" data-jtsa-spin-angle=\"{spinTotalRotation:0.###}\""
            : string.Empty;
        var elapsedMilliseconds = Math.Clamp(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - spinStartedUnixMilliseconds,
            0,
            (long)SpinDuration.TotalMilliseconds);
        var animationName = $"jtsa-roulette-{spinStartedUnixMilliseconds}";
        var animationStyle = isSpinning
            ? $"animation:{animationName} {SpinDuration.TotalMilliseconds:0}ms cubic-bezier(.22,1,.36,1) {-elapsedMilliseconds}ms forwards;"
            : $"transform:rotate({wheelRotation:0.###}deg);";
        var animationDefinition = isSpinning
            ? $"<style>@keyframes {animationName}{{from{{transform:rotate(0deg)}}to{{transform:rotate({spinTotalRotation:0.###}deg)}}}}</style>"
            : string.Empty;
        var encodedCaption = WebUtility.HtmlEncode(caption);
        var encodedResult = WebUtility.HtmlEncode(ResultTextBlock?.Text ?? "―");
        context.SetExpansionOverlay(new ExpansionOverlayContent(
            "roulette",
            $"{animationDefinition}<div style=\"box-sizing:border-box;width:100%;height:100%;display:flex;align-items:center;justify-content:center;gap:42px;color:white;font-family:'Segoe UI',sans-serif;text-shadow:0 3px 8px #000\"><div style=\"position:relative;width:430px;height:430px\"><div style=\"position:absolute;z-index:3;left:195px;top:-8px;width:0;height:0;border-left:20px solid transparent;border-right:20px solid transparent;border-top:42px solid #ffd166;filter:drop-shadow(0 3px 3px #000)\"></div><div {wheelAnimationData} style=\"position:absolute;inset:15px;border-radius:50%;background:conic-gradient(from 0deg,{gradient});box-shadow:0 8px 30px #000;{animationStyle}\">{labels}<div style=\"position:absolute;left:50%;top:50%;width:66px;height:66px;transform:translate(-50%,-50%);border-radius:50%;background:#555\"></div></div></div><div style=\"box-sizing:border-box;width:430px;padding:26px;background:rgba(20,20,20,.86);border-radius:20px;text-align:center\"><div style=\"font-size:24px;color:#ddd\">{encodedCaption}</div><div style=\"font-size:58px;font-weight:800;color:#ffd166;margin-top:10px;overflow-wrap:anywhere\">{encodedResult}</div></div></div>",
            485, 40, 950, 430));
    }
}
