using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace VuMeter;

public sealed class AnalogVuMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(AnalogVuMeter),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(AnalogVuMeter),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // Standard VU scale labels
    private static readonly (double Db, string Text, bool IsRed)[] Marks =
    {
        (-20.0, "20", false),
        (-10.0, "10", false),
        ( -7.0, "7",  false),
        ( -5.0, "5",  false),
        ( -3.0, "3",  false),
        ( -1.0, "1",  false),
        (  0.0, "0",  false),
        (  1.0, "+1", true),
        (  3.0, "+3", true),
    };

    private const double SweepDeg = 96;   // total needle sweep
    private const double DbMin    = -20;
    private const double DbMax    =  3;

    private static readonly Brush FrameBrush;
    private static readonly Brush FaceBrush;
    private static readonly Brush GlassBrush;
    private static readonly Pen   FramePen      = new(new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)), 1.5);
    private static readonly Pen   FacePen       = new(new SolidColorBrush(Color.FromRgb(0x6a, 0x55, 0x2a)), 1.0);
    private static readonly Pen   ArcPen        = new(new SolidColorBrush(Color.FromRgb(0x22, 0x1a, 0x10)), 1.6);
    private static readonly Pen   RedArcPen     = new(new SolidColorBrush(Color.FromRgb(0xc4, 0x14, 0x14)), 3.0);
    private static readonly Pen   MajorTickPen  = new(new SolidColorBrush(Color.FromRgb(0x16, 0x10, 0x08)), 1.8);
    private static readonly Pen   MinorTickPen  = new(new SolidColorBrush(Color.FromRgb(0x16, 0x10, 0x08)), 0.9);
    private static readonly Pen   RedTickPen    = new(new SolidColorBrush(Color.FromRgb(0xc4, 0x14, 0x14)), 1.8);
    private static readonly Pen   NeedlePen     = new(new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)), 2.4);
    private static readonly Brush BlackBrush    = new SolidColorBrush(Color.FromRgb(0x14, 0x0e, 0x06));
    private static readonly Brush RedBrush      = new SolidColorBrush(Color.FromRgb(0xb8, 0x10, 0x10));
    private static readonly Brush ScrewOuter    = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
    private static readonly Brush ScrewInner    = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly Brush ChannelBrush  = new SolidColorBrush(Color.FromRgb(0x4a, 0x32, 0x12));

    private static readonly Typeface TickFont    = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold,   FontStretches.Normal);
    private static readonly Typeface VuFont      = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold,   FontStretches.Normal);
    private static readonly Typeface ChannelFont = new(new FontFamily("Georgia"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

    static AnalogVuMeter()
    {
        var frame = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1),
        };
        frame.GradientStops.Add(new GradientStop(Color.FromRgb(0x33, 0x2a, 0x1a), 0));
        frame.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x06, 0x02), 1));
        frame.Freeze();
        FrameBrush = frame;

        var face = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1),
        };
        face.GradientStops.Add(new GradientStop(Color.FromRgb(0xfa, 0xf1, 0xd2), 0));
        face.GradientStops.Add(new GradientStop(Color.FromRgb(0xeb, 0xdc, 0xa5), 0.55));
        face.GradientStops.Add(new GradientStop(Color.FromRgb(0xd2, 0xbe, 0x82), 1));
        face.Freeze();
        FaceBrush = face;

        var glass = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1),
        };
        glass.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, 0xff, 0xff, 0xff), 0));
        glass.GradientStops.Add(new GradientStop(Color.FromArgb(0x10, 0xff, 0xff, 0xff), 0.45));
        glass.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xff, 0xff, 0xff), 0.55));
        glass.Freeze();
        GlassBrush = glass;

        FramePen.Freeze();
        FacePen.Freeze();
        ArcPen.Freeze();
        RedArcPen.Freeze();
        MajorTickPen.Freeze();
        MinorTickPen.Freeze();
        RedTickPen.Freeze();
        NeedlePen.Freeze();
        ((SolidColorBrush)BlackBrush).Freeze();
        ((SolidColorBrush)RedBrush).Freeze();
        ((SolidColorBrush)ScrewOuter).Freeze();
        ((SolidColorBrush)ScrewInner).Freeze();
        ((SolidColorBrush)ChannelBrush).Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Outer enclosure
        var outer = new Rect(0, 0, w, h);
        dc.DrawRoundedRectangle(FrameBrush, FramePen, outer, 10, 10);

        // Face inset
        double pad = 12;
        var face = new Rect(pad, pad, w - 2 * pad, h - 2 * pad);
        dc.DrawRoundedRectangle(FaceBrush, FacePen, face, 5, 5);

        // Geometry: pivot just inside the bottom edge, radius sized so the
        // arc fits within the face on both axes (whichever is the tighter bound).
        double startDeg = -SweepDeg / 2;
        double endDeg   =  SweepDeg / 2;
        double sweepHalfRad = (SweepDeg / 2) * Math.PI / 180.0;
        double sideMargin = Math.Max(28, face.Width * 0.06);
        double topMargin  = Math.Max(36, face.Height * 0.12);

        double radiusFromWidth  = (face.Width / 2 - sideMargin) / Math.Sin(sweepHalfRad);
        double radiusFromHeight = face.Height - topMargin - 6; // leaving small space above the screw
        double radius = Math.Min(radiusFromWidth, radiusFromHeight);

        Point pivot = new(
            face.Left + face.Width / 2,
            face.Top + topMargin + radius);
        double DbToAngle(double db) =>
            startDeg + (db - DbMin) / (DbMax - DbMin) * (endDeg - startDeg);

        // Black arc for the negative range
        DrawArc(dc, ArcPen, pivot, radius, startDeg, DbToAngle(0));
        // Red arc for the overload range
        DrawArc(dc, RedArcPen, pivot, radius, DbToAngle(0), endDeg);

        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double tickFontSize    = Math.Max(11, radius * 0.085);
        double vuFontSize      = Math.Max(18, radius * 0.20);
        double channelFontSize = Math.Max(11, radius * 0.075);

        double minorTickLen = Math.Max(5, radius * 0.025);
        double majorTickLen = Math.Max(10, radius * 0.055);
        double labelInset   = majorTickLen + tickFontSize * 0.85;

        // Minor ticks every 1 dB
        for (double db = DbMin; db <= DbMax; db += 1)
        {
            if (Marks.Any(m => Math.Abs(m.Db - db) < 0.01)) continue;
            double a = DbToAngle(db);
            DrawTick(dc, db >= 0 ? RedTickPen : MinorTickPen, pivot, radius, a, minorTickLen);
        }

        // Major ticks + numbers
        foreach (var m in Marks)
        {
            double a = DbToAngle(m.Db);
            DrawTick(dc, m.IsRed ? RedTickPen : MajorTickPen, pivot, radius, a, majorTickLen);

            double rad = (a - 90) * Math.PI / 180.0;
            double textRadius = radius - labelInset;
            Point  textCenter = new(
                pivot.X + Math.Cos(rad) * textRadius,
                pivot.Y + Math.Sin(rad) * textRadius);

            var ft = new FormattedText(
                m.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                TickFont, tickFontSize, m.IsRed ? RedBrush : BlackBrush, pixelsPerDip);
            dc.DrawText(ft, new Point(textCenter.X - ft.Width / 2, textCenter.Y - ft.Height / 2));
        }

        // "VU" centered between arc bottom and pivot
        var vu = new FormattedText("VU",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            VuFont, vuFontSize, BlackBrush, pixelsPerDip);
        double vuCenterY = pivot.Y - radius * 0.32;
        dc.DrawText(vu, new Point(pivot.X - vu.Width / 2, vuCenterY - vu.Height / 2));

        // Channel label, lower-right of the face
        if (!string.IsNullOrEmpty(Label))
        {
            var ch = new FormattedText(Label,
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                ChannelFont, channelFontSize * 1.4, ChannelBrush, pixelsPerDip);
            dc.DrawText(ch, new Point(face.Right - ch.Width - 14, face.Bottom - ch.Height - 8));
        }

        // Manufacturer-style flourish text, lower-left
        var brand = new FormattedText("STANDARD VU",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            ChannelFont, channelFontSize * 0.85, ChannelBrush, pixelsPerDip);
        dc.DrawText(brand, new Point(face.Left + 14, face.Bottom - brand.Height - 10));

        // Needle
        double levelDb = DbMin + Math.Clamp(Level, 0, 1) * (DbMax - DbMin);
        double needleDeg = DbToAngle(levelDb);
        double needleRad = (needleDeg - 90) * Math.PI / 180.0;

        Point needleTip = new(
            pivot.X + Math.Cos(needleRad) * radius * 0.95,
            pivot.Y + Math.Sin(needleRad) * radius * 0.95);
        Point needleBase = new(
            pivot.X + Math.Cos(needleRad) * 6,
            pivot.Y + Math.Sin(needleRad) * 6);
        dc.DrawLine(NeedlePen, needleBase, needleTip);

        // Pivot screw
        double screwR = Math.Max(7, face.Height * 0.04);
        dc.DrawEllipse(ScrewOuter, null, pivot, screwR,        screwR);
        dc.DrawEllipse(ScrewInner, null, pivot, screwR * 0.55, screwR * 0.55);

        // Subtle glass highlight across the top
        var glassRect = new Rect(face.Left, face.Top, face.Width, face.Height * 0.55);
        dc.DrawRoundedRectangle(GlassBrush, null, glassRect, 5, 5);
    }

    private static void DrawTick(DrawingContext dc, Pen pen, Point center, double radius, double angleDeg, double length)
    {
        double rad = (angleDeg - 90) * Math.PI / 180.0;
        Point inner = new(center.X + Math.Cos(rad) * (radius - length), center.Y + Math.Sin(rad) * (radius - length));
        Point outer = new(center.X + Math.Cos(rad) * radius,            center.Y + Math.Sin(rad) * radius);
        dc.DrawLine(pen, inner, outer);
    }

    private static void DrawArc(DrawingContext dc, Pen pen, Point center, double radius, double startDeg, double endDeg)
    {
        double startRad = (startDeg - 90) * Math.PI / 180.0;
        double endRad   = (endDeg   - 90) * Math.PI / 180.0;
        Point p1 = new(center.X + Math.Cos(startRad) * radius, center.Y + Math.Sin(startRad) * radius);
        Point p2 = new(center.X + Math.Cos(endRad)   * radius, center.Y + Math.Sin(endRad)   * radius);

        var fig = new PathFigure { StartPoint = p1, IsClosed = false };
        fig.Segments.Add(new ArcSegment(p2, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}
