using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace VuMeter;

public partial class MainWindow : Window
{
    private readonly AudioCapture _capture = new();
    private readonly DispatcherTimer _uiTimer;

    // VU ballistics
    private double _leftLevel, _rightLevel;
    private double _leftPeakLevel, _rightPeakLevel;
    private int _leftPeakHoldFrames, _rightPeakHoldFrames;

    private const double AttackCoeff  = 0.85;
    private const double DecayCoeff   = 0.92;
    private const double PeakHoldMs   = 1500;
    private const double RefreshHz    = 60;

    private int _peakHoldFrames;

    public MainWindow()
    {
        InitializeComponent();
        _peakHoldFrames = (int)(PeakHoldMs / (1000.0 / RefreshHz));

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / RefreshHz) };
        _uiTimer.Tick += OnTick;

        Loaded  += (_, _) => StartCapture();
        Closing += (_, _) => _capture.Stop();
    }

    private void StartCapture()
    {
        bool ok = _capture.Start();
        StatusDot.Fill  = ok ? new SolidColorBrush(Color.FromRgb(0, 180, 60))
                             : new SolidColorBrush(Color.FromRgb(180, 0, 0));
        StatusText.Text = ok ? "Loopback active" : "No loopback device";
        _uiTimer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _capture.GetLevels(out double rawL, out double rawR);

        // Attack / decay smoothing
        _leftLevel  = rawL > _leftLevel  ? Lerp(_leftLevel,  rawL, 1 - AttackCoeff)
                                         : Lerp(_leftLevel,  rawL, 1 - DecayCoeff);
        _rightLevel = rawR > _rightLevel ? Lerp(_rightLevel, rawR, 1 - AttackCoeff)
                                         : Lerp(_rightLevel, rawR, 1 - DecayCoeff);

        // Peak hold
        UpdatePeak(ref _leftPeakLevel,  ref _leftPeakHoldFrames,  _leftLevel);
        UpdatePeak(ref _rightPeakLevel, ref _rightPeakHoldFrames, _rightLevel);

        RenderBar(LeftBar,   LeftPeak,   _leftLevel,  _leftPeakLevel);
        RenderBar(RightBar,  RightPeak,  _rightLevel, _rightPeakLevel);
    }

    private void UpdatePeak(ref double peak, ref int holdFrames, double level)
    {
        if (level >= peak)
        {
            peak = level;
            holdFrames = _peakHoldFrames;
        }
        else if (holdFrames > 0)
        {
            holdFrames--;
        }
        else
        {
            peak = Math.Max(0, peak - 0.008);
        }
    }

    private static void RenderBar(System.Windows.Shapes.Rectangle bar,
                                  System.Windows.Shapes.Rectangle peak,
                                  double level, double peakLevel)
    {
        double totalH = bar.ActualHeight > 0 ? bar.ActualHeight
                      : ((System.Windows.Controls.Grid)bar.Parent).ActualHeight;
        if (totalH <= 0) return;

        bar.Height  = Math.Clamp(level * totalH, 0, totalH);
        double peakY = Math.Clamp(peakLevel * totalH, 3, totalH);
        peak.Margin = new Thickness(0, 0, 0, peakY - 3);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
