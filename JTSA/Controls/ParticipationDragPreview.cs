using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace JTSA.Controls;

/// <summary>A hit-test-transparent copy of the dragged row, positioned in WPF coordinates.</summary>
internal sealed class ParticipationDragPreview(UIElement surface, FrameworkElement row) : Adorner(surface)
{
    private Point position;
    private readonly VisualBrush brush = new(row) { Stretch = Stretch.Uniform };

    internal void FollowPointer()
    {
        if (!GetCursorPos(out var cursor)) return;
        position = AdornedElement.PointFromScreen(new Point(cursor.X, cursor.Y));
        Visibility = position.X >= 0 && position.Y >= 0 &&
            position.X <= AdornedElement.RenderSize.Width && position.Y <= AdornedElement.RenderSize.Height
            ? Visibility.Visible : Visibility.Hidden;
        position.Offset(14, 18);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = Math.Max(120, row.ActualWidth);
        var height = Math.Max(32, row.ActualHeight);
        drawingContext.PushOpacity(0.85);
        drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(48, 48, 48)),
            new Pen(Brushes.LightSeaGreen, 1), new Rect(position.X - 4, position.Y - 3, width + 8, height + 6), 4, 4);
        drawingContext.DrawRectangle(brush, null, new Rect(position, new Size(width, height)));
        drawingContext.Pop();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
