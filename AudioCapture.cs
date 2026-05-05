using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VuMeter;

/// <summary>
/// WASAPI loopback capture — taps whatever is playing through the default output device.
/// </summary>
internal sealed class AudioCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;

    // Ring buffer — 50 ms worth at 48 kHz stereo float = ~9600 samples
    private const int BufSize = 16384;
    private readonly float[] _buf = new float[BufSize];
    private int _writePos;
    private readonly object _lock = new();

    public bool Start()
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable   += OnData;
            _capture.RecordingStopped += (_, _) => { };
            _capture.StartRecording();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Stop()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        // WasapiLoopbackCapture always gives IEEE float 32-bit interleaved stereo
        int floatCount = e.BytesRecorded / 4;
        lock (_lock)
        {
            for (int i = 0; i < floatCount; i++)
            {
                _buf[_writePos] = BitConverter.ToSingle(e.Buffer, i * 4);
                _writePos = (_writePos + 1) % BufSize;
            }
        }
    }

    /// <summary>Returns normalised RMS [0..1] for L and R over the most recent window.</summary>
    public void GetLevels(out double left, out double right)
    {
        const int WindowSamples = 2048; // ~21 ms at 48 kHz
        double sumL = 0, sumR = 0;
        int count = 0;

        lock (_lock)
        {
            int start = (_writePos - WindowSamples * 2 + BufSize) % BufSize;
            for (int i = 0; i < WindowSamples; i++)
            {
                float l = _buf[(start + i * 2)     % BufSize];
                float r = _buf[(start + i * 2 + 1) % BufSize];
                sumL += l * l;
                sumR += r * r;
                count++;
            }
        }

        if (count == 0) { left = right = 0; return; }

        double rmsL = Math.Sqrt(sumL / count);
        double rmsR = Math.Sqrt(sumR / count);

        left  = DbFsToVuPosition(20 * Math.Log10(Math.Max(rmsL, 1e-6)));
        right = DbFsToVuPosition(20 * Math.Log10(Math.Max(rmsR, 1e-6)));
    }

    // 0 VU pegged at -16 dBFS — typical-content levels swing across most of the dial
    // without slamming the needle on every transient. Visible range -20..+3 VU.
    private const double ZeroVuDbFs = -16.0;
    private const double VuMin = -20.0;
    private const double VuMax =   3.0;

    private static double DbFsToVuPosition(double dbFs)
    {
        double vu = dbFs - ZeroVuDbFs;
        return Math.Clamp((vu - VuMin) / (VuMax - VuMin), 0.0, 1.0);
    }

    public void Dispose() => Stop();
}
