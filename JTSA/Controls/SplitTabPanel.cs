using System.Windows;
using System.Windows.Controls;

namespace JTSA.Controls;

/// <summary>通常のタブを左から、指定されたタブを右から配置します。</summary>
public class SplitTabPanel : Panel
{
    public static readonly DependencyProperty IsRightAlignedProperty =
        DependencyProperty.RegisterAttached(
            "IsRightAligned",
            typeof(bool),
            typeof(SplitTabPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static bool GetIsRightAligned(DependencyObject element) =>
        (bool)element.GetValue(IsRightAlignedProperty);

    public static void SetIsRightAligned(DependencyObject element, bool value) =>
        element.SetValue(IsRightAlignedProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var left = 0.0;
        var right = finalSize.Width;

        foreach (UIElement child in InternalChildren)
        {
            if (GetIsRightAligned(child))
                continue;

            var width = child.DesiredSize.Width;
            child.Arrange(new Rect(left, 0, width, finalSize.Height));
            left += width;
        }

        for (var index = InternalChildren.Count - 1; index >= 0; index--)
        {
            var child = InternalChildren[index];
            if (!GetIsRightAligned(child))
                continue;

            var width = child.DesiredSize.Width;
            right -= width;
            child.Arrange(new Rect(Math.Max(left, right), 0, width, finalSize.Height));
        }

        return finalSize;
    }
}
