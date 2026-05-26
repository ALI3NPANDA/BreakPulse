using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BreakPulse.Rendering;

/// <summary>
/// A lightweight animated background that uses minimal CPU/RAM.
/// Renders a smooth gradient with subtle color shifts using the same
/// background, particle accent, and wave colors — no per-pixel particle physics.
/// Targets ~10 fps with a simple radial gradient + slow color rotation.
/// </summary>
public sealed class LightweightBackgroundRenderer : IDisposable
{
    // ── Public color properties (same interface as ParticleWaveRenderer) ──────
    private readonly object _colorLock = new();
    private Color _accentColor = Color.FromRgb(108, 99, 255);
    private Color _waveColor = Color.FromRgb(0, 200, 180);
    private Color _bgColor = Color.FromRgb(13, 13, 17);

    public Color AccentColor
    {
        get { lock (_colorLock) return _accentColor; }
        set { lock (_colorLock) _accentColor = value; }
    }

    public Color WaveColor
    {
        get { lock (_colorLock) return _waveColor; }
        set { lock (_colorLock) _waveColor = value; }
    }

    public Color BackgroundColor
    {
        get { lock (_colorLock) return _bgColor; }
        set { lock (_colorLock) _bgColor = value; }
    }

    // ── Output ───────────────────────────────────────────────────────────────
    public WriteableBitmap Bitmap { get; }

    // ── Private state ────────────────────────────────────────────────────────
    private readonly int _w, _h;
    private readonly int _stride;
    private readonly byte[] _pixels;
    private readonly byte[] _backBuffer;
    private double _time;
    private volatile bool _running;
    private volatile bool _disposed;
    private Thread? _renderThread;
    private volatile bool _frameReady;
    private readonly object _bufferLock = new();
    private readonly Dispatcher _dispatcher;

    public LightweightBackgroundRenderer(int width, int height)
    {
        _w = width;
        _h = height;
        _stride = width * 4;
        _pixels = new byte[_stride * height];
        _backBuffer = new byte[_stride * height];
        Bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        CompositionTarget.Rendering += OnUiRender;
        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = "LightweightBgRenderer",
            Priority = ThreadPriority.BelowNormal,
        };
        _renderThread.Start();
    }

    public void Stop()
    {
        _running = false;
        CompositionTarget.Rendering -= OnUiRender;
        _renderThread?.Join(500);
        _renderThread = null;
    }

    public void Dispose()
    {
        Stop();
        _disposed = true;
    }

    // ── Background render loop (~10 fps — very light) ────────────────────────

    private void RenderLoop()
    {
        var lastFrame = DateTime.UtcNow;
        const double targetInterval = 1.0 / 10.0; // ~10 fps
        while (_running && !_disposed)
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min((now - lastFrame).TotalSeconds, 0.1);
            lastFrame = now;
            _time += dt * 0.3; // slow progression

            Rasterize();

            lock (_bufferLock)
            {
                Buffer.BlockCopy(_pixels, 0, _backBuffer, 0, _pixels.Length);
                _frameReady = true;
            }

            double elapsed = (DateTime.UtcNow - now).TotalSeconds;
            var sleepMs = (int)((targetInterval - elapsed) * 1000);
            if (sleepMs > 0) Thread.Sleep(sleepMs);
        }
    }

    private void OnUiRender(object? sender, EventArgs e)
    {
        if (_disposed || !_frameReady) return;
        lock (_bufferLock)
        {
            if (!_frameReady) return;
            _frameReady = false;
            Bitmap.Lock();
            try
            {
                Marshal.Copy(_backBuffer, 0, Bitmap.BackBuffer, _backBuffer.Length);
                Bitmap.AddDirtyRect(new Int32Rect(0, 0, _w, _h));
            }
            finally
            {
                Bitmap.Unlock();
            }
        }
    }

    // ── Rasterization: smooth animated gradient with wave bands ─────────────

    private void Rasterize()
    {
        Color accentSnap, waveSnap, bgSnap;
        lock (_colorLock)
        {
            accentSnap = _accentColor;
            waveSnap = _waveColor;
            bgSnap = _bgColor;
        }

        double t = _time;
        double cx = _w * 0.5;
        double cy = _h * 0.5;
        double maxR = Math.Sqrt(cx * cx + cy * cy);

        // Slowly oscillate intensities
        double blend1 = (Math.Sin(t * 0.7) + 1.0) * 0.5;
        double blend2 = (Math.Sin(t * 0.5 + 2.0) + 1.0) * 0.5;

        // Accent blob drifts from upper-left area
        double ox1 = Math.Sin(t * 0.4) * _w * 0.18;
        double oy1 = Math.Cos(t * 0.35) * _h * 0.18;

        // Wave blob drifts from lower-right area (opposite side for contrast)
        double ox2 = Math.Sin(t * 0.3 + 3.14) * _w * 0.22;
        double oy2 = Math.Cos(t * 0.25 + 2.5) * _h * 0.22;

        float bgR = bgSnap.R / 255f;
        float bgG = bgSnap.G / 255f;
        float bgB = bgSnap.B / 255f;
        float aR = accentSnap.R / 255f;
        float aG = accentSnap.G / 255f;
        float aB = accentSnap.B / 255f;
        float wR = waveSnap.R / 255f;
        float wG = waveSnap.G / 255f;
        float wB = waveSnap.B / 255f;

        // Precompute wave band parameters (cheap sinusoidal bands in wave color)
        double waveBandFreq = 0.025;
        double waveBandPhase = t * 0.6;

        for (int py = 0; py < _h; py++)
        {
            int rowOffset = py * _stride;
            for (int px = 0; px < _w; px++)
            {
                // ── Accent radial glow (centered around drifting point 1) ──
                double dx1 = px - (cx + ox1);
                double dy1 = py - (cy + oy1);
                double d1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1) / maxR;
                d1 = Math.Min(d1, 1.0);
                double g1 = (1.0 - d1 * d1) * 0.50 * (0.7 + blend1 * 0.3);

                // ── Wave radial glow (centered around drifting point 2) ────
                double dx2 = px - (cx + ox2);
                double dy2 = py - (cy + oy2);
                double d2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2) / maxR;
                d2 = Math.Min(d2, 1.0);
                double g2 = (1.0 - d2 * d2) * 0.45 * (0.7 + blend2 * 0.3);

                // ── Wave color bands (diagonal sinusoidal streaks) ──────────
                // This adds visible wave-colored bands across the background
                double band = Math.Sin((px + py * 0.7) * waveBandFreq + waveBandPhase)
                            * Math.Sin((px * 0.5 - py) * waveBandFreq * 0.8 + waveBandPhase * 0.7);
                band = Math.Max(0, band); // only positive contributions
                band *= 0.18 * (0.8 + blend2 * 0.2); // subtle intensity

                // Composite: background + accent glow + wave glow + wave bands
                float r = bgR + (float)(aR * g1 + wR * g2 + wR * band);
                float g = bgG + (float)(aG * g1 + wG * g2 + wG * band);
                float b = bgB + (float)(aB * g1 + wB * g2 + wB * band);

                // Clamp
                r = Math.Min(r, 1f);
                g = Math.Min(g, 1f);
                b = Math.Min(b, 1f);

                int idx = rowOffset + px * 4;
                _pixels[idx] = (byte)(b * 255);
                _pixels[idx + 1] = (byte)(g * 255);
                _pixels[idx + 2] = (byte)(r * 255);
                _pixels[idx + 3] = 255;
            }
        }
    }
}

