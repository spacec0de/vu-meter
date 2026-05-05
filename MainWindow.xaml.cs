using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace VuMeter;

public partial class MainWindow : Window
{
    private readonly AudioCapture _capture = new();
    private readonly DispatcherTimer _uiTimer;

    private double _leftLevel, _rightLevel;

    // Approximating ANSI VU ballistics (~300 ms integration) at 60 Hz
    private const double AttackCoeff = 0.88;
    private const double DecayCoeff  = 0.94;
    private const double RefreshHz   = 60;

    public MainWindow()
    {
        InitializeComponent();

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

        _leftLevel  = rawL > _leftLevel  ? Lerp(_leftLevel,  rawL, 1 - AttackCoeff)
                                         : Lerp(_leftLevel,  rawL, 1 - DecayCoeff);
        _rightLevel = rawR > _rightLevel ? Lerp(_rightLevel, rawR, 1 - AttackCoeff)
                                         : Lerp(_rightLevel, rawR, 1 - DecayCoeff);

        LeftMeter.Level  = _leftLevel;
        RightMeter.Level = _rightLevel;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
