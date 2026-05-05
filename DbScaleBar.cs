using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VuMeter;

/// <summary>
/// Draws dB tick marks aligned to match the meter bar above it.
/// </summary>
public sealed class DbScaleBar : System.Windows.FrameworkElement
{
    private static readonly double[] Ticks = { 0, -3, -6, -10, -20, -40, -60 };
    private static readonly Typeface Font = new("Consolas");
    private static readonly Brush   TextBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
    private static readonly Pen     TickPen   = new(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1);

    static DbScaleBar()
    {
        TextBrush.Freeze();
        TickPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double h = ActualHeight;
        double w = ActualWidth;
        if (h <= 0) return;

        foreach (double db in Ticks)
        {
            double norm = (db + 60.0) / 60.0;          // same formula as AudioCapture
            double y    = h - norm * h;

            dc.DrawLine(TickPen, new Point(0, y), new Point(4, y));

            var text = new FormattedText(
                db == 0 ? "0" : db.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Font, 8, TextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(text, new Point(6, y - text.Height / 2));
        }
    }

    protected override Size MeasureOverride(Size _) => new(36, 0);
    protected override Size ArrangeOverride(Size s) { InvalidateVisual(); return s; }
}
