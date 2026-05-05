using System.Windows;
using System.Windows.Media;

namespace VuMeter;

/// <summary>
/// Draws horizontal gap lines over the meter bar to give a classic segmented LED look.
/// </summary>
public sealed class SegmentOverlay : System.Windows.FrameworkElement
{
    private static readonly Pen GapPen = new(new SolidColorBrush(Color.FromArgb(180, 26, 26, 26)), 2);

    static SegmentOverlay() => GapPen.Freeze();

    protected override void OnRender(DrawingContext dc)
    {
        double h = ActualHeight;
        double w = ActualWidth;
        if (h <= 0 || w <= 0) return;

        const int segments = 30;
        double step = h / segments;

        for (int i = 1; i < segments; i++)
        {
            double y = h - i * step;
            dc.DrawLine(GapPen, new Point(0, y), new Point(w, y));
        }
    }

    protected override Size MeasureOverride(Size _) => new(0, 0);
    protected override Size ArrangeOverride(Size s) { InvalidateVisual(); return s; }
}
