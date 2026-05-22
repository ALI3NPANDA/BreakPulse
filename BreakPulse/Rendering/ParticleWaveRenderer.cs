using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BreakPulse.Rendering;

/// <summary>
/// Procedural animated background inspired by flowing luminous particle waves.
/// Rendered entirely in CPU pixel math — no video, no shaders, no external libs.
/// Heavy rasterization runs on a background thread; only bitmap commit touches the UI.
/// </summary>
public sealed class ParticleWaveRenderer : IDisposable
{
    // ── Public tunables ───────────────────────────────────────────────────────
    private readonly object _colorLock = new();
    private Color _accentColor = Color.FromRgb(108, 99, 255);
    private Color _waveColor = Color.FromRgb(0, 200, 180);
    private Color _bgColor = Color.FromRgb(13, 13, 17);
    public Color AccentColor
    {
        get
        {
            lock (_colorLock)
            {
                return _accentColor;
            }
        }
        set
        {
            lock (_colorLock)
            {
                _accentColor = value;
            }
        }
    }
    public Color WaveColor
    {
        get
        {
            lock (_colorLock)
            {
                return _waveColor;
            }
        }
        set
        {
            lock (_colorLock)
            {
                _waveColor = value;
            }
        }
    }
    public Color BackgroundColor
    {
        get
        {
            lock (_colorLock)
            {
                return _bgColor;
            }
        }
        set
        {
            lock (_colorLock)
            {
                _bgColor = value;
            }
        }
    }
    public double Brightness { get; set; } = 5.00;
    public double Speed { get; set; } = 1.0;
    public int ParticleCount { get; set; } = 120;
    public double GlowRadius { get; set; } = 60.0;
    public double WaveAmplitude { get; set; } = 0.55;
    public double WaveFrequency { get; set; } = 0.018;
    public double WaveSpeed { get; set; } = 0.38;

    // ── Read-only output ──────────────────────────────────────────────────────
    public WriteableBitmap Bitmap { get; }

    // ── Private state ─────────────────────────────────────────────────────────
    private readonly int _w, _h;
    private readonly int _stride;
    private readonly byte[] _pixels;
    private readonly float[] _accumR, _accumG, _accumB;
    private readonly Particle[] _particles;
    private double _time;
    private volatile bool _running;
    private volatile bool _disposed;

    // Background rendering
    private Thread? _renderThread;
    private readonly byte[] _backBuffer; // background thread writes here
    private volatile bool _frameReady; // signals a new frame is available
    private readonly Dispatcher _dispatcher;
    private readonly object _bufferLock = new();

    private struct Particle
    {
        public double X, Y;
        public double Vx, Vy;
        public double Phase;
        public double Size;
        public double Brightness;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public ParticleWaveRenderer(int width, int height)
    {
        _w = width;
        _h = height;
        _stride = width * 4;
        _pixels = new byte[_stride * height];
        _backBuffer = new byte[_stride * height];
        _accumR = new float[width * height];
        _accumG = new float[width * height];
        _accumB = new float[width * height];
        Bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _dispatcher = Dispatcher.CurrentDispatcher;
        _particles = new Particle[500];
        InitParticles();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        if (_running) return;
        _running = true;
        CompositionTarget.Rendering += OnUiRender;
        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = "ParticleWaveRenderer",
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

    // ── Particle init ─────────────────────────────────────────────────────────

    private void InitParticles()
    {
        var rng = new Random(42);
        for (var i = 0; i < _particles.Length; i++)
        {
            _particles[i] = new Particle
            {
                X = rng.NextDouble() * _w,
                Y = rng.NextDouble() * _h,
                Vx = (rng.NextDouble() - 0.3) * 0.4,
                Vy = (rng.NextDouble() - 0.6) * 0.3,
                Phase = rng.NextDouble() * Math.PI * 2,
                Size = 0.5 + rng.NextDouble() * 1.0,
                Brightness = 0.5 + rng.NextDouble() * 0.5,
            };
        }
    }

    // ── Background render loop (~30 fps) ──────────────────────────────────────

    private void RenderLoop()
    {
        var lastFrame = DateTime.UtcNow;
        const double targetInterval = 1.0 / 30.0; // ~30 fps
        while (_running && !_disposed)
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min((now - lastFrame).TotalSeconds, 0.05);
            lastFrame = now;
            _time += dt * Speed;

            // Snapshot tunables (they may be set from UI thread)
            UpdateParticles(dt);
            Rasterize();

            // Copy result into back buffer under lock
            lock (_bufferLock)
            {
                Buffer.BlockCopy(_pixels, 0, _backBuffer, 0, _pixels.Length);
                _frameReady = true;
            }

            // Sleep to target ~30 fps
            double elapsed = (DateTime.UtcNow - now).TotalSeconds;
            var sleepMs = (int)((targetInterval - elapsed) * 1000);
            if (sleepMs > 0) Thread.Sleep(sleepMs);
        }
    }

    // ── UI thread: only commits bitmap when a frame is ready ──────────────────

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

    // ── Particle physics ──────────────────────────────────────────────────────

    private void UpdateParticles(double dt)
    {
        int count = Math.Clamp(ParticleCount, 1, _particles.Length);
        double spd = Speed;
        for (var i = 0; i < count; i++)
        {
            ref var p = ref _particles[i];
            double waveX = Math.Sin(_time * 0.7 + p.Phase) * 0.5;
            double waveY = Math.Cos(_time * 0.5 + p.Phase * 1.3) * 0.4;
            double waveX2 = Math.Sin(_time * 1.1 + p.Phase * 0.7) * 0.25;
            p.X += (p.Vx + waveX + waveX2) * dt * 60 * spd;
            p.Y += (p.Vy + waveY) * dt * 60 * spd;
            double margin = GlowRadius;
            if (p.X < -margin) p.X += _w + margin * 2;
            if (p.X > _w + margin) p.X -= _w + margin * 2;
            if (p.Y < -margin) p.Y += _h + margin * 2;
            if (p.Y > _h + margin) p.Y -= _h + margin * 2;
        }
    }

    // ── Rasterization ─────────────────────────────────────────────────────────

    private void Rasterize()
    {
        int count = Math.Clamp(ParticleCount, 1, _particles.Length);
        int pixels = _w * _h;
        Array.Clear(_accumR, 0, pixels);
        Array.Clear(_accumG, 0, pixels);
        Array.Clear(_accumB, 0, pixels);

        // Snapshot colors under lock for thread-safe access
        Color accentSnap, waveSnap, bgSnap;
        lock (_colorLock)
        {
            accentSnap = _accentColor;
            waveSnap = _waveColor;
            bgSnap = _bgColor;
        }
        float aR = accentSnap.R / 255f;
        float aG = accentSnap.G / 255f;
        float aB = accentSnap.B / 255f;
        float wR = waveSnap.R / 255f;
        float wG = waveSnap.G / 255f;
        float wB = waveSnap.B / 255f;
        double t = _time;
        double freq = WaveFrequency;
        double wAmp = WaveAmplitude;
        double wSpd = WaveSpeed;
        double gRad = GlowRadius;
        double bright = Brightness;

        // ── Particle glow splats ───────────────────────────────────────────
        for (var i = 0; i < count; i++)
        {
            ref var p = ref _particles[i];
            double radius = gRad * p.Size;
            double r2 = radius * radius;
            int x0 = Math.Max(0, (int)(p.X - radius));
            int x1 = Math.Min(_w - 1, (int)(p.X + radius));
            int y0 = Math.Max(0, (int)(p.Y - radius));
            int y1 = Math.Min(_h - 1, (int)(p.Y + radius));
            double pulse = 0.75 + 0.25 * Math.Sin(t * 1.8 + p.Phase);
            double pBright = p.Brightness * pulse * bright * 0.45;
            double blend = (Math.Sin(p.Phase * 3.7) + 1.0) * 0.5;
            var pR = (float)(aR * (1 - blend) + wR * blend);
            var pG = (float)(aG * (1 - blend) + wG * blend);
            var pB = (float)(aB * (1 - blend) + wB * blend);
            for (int py = y0; py <= y1; py++)
            {
                double dy = py - p.Y;
                double dy2 = dy * dy;
                int row = py * _w;
                for (int px = x0; px <= x1; px++)
                {
                    double dx = px - p.X;
                    double d2 = dx * dx + dy2;
                    if (d2 >= r2) continue;
                    double norm = d2 / r2;
                    double att = (1.0 - norm) * (1.0 - norm);
                    att *= pBright;
                    int idx = row + px;
                    _accumR[idx] += (float)(pR * att);
                    _accumG[idx] += (float)(pG * att);
                    _accumB[idx] += (float)(pB * att);
                }
            }
        }

        // ── Wave surface overlay ───────────────────────────────────────────
        for (var py = 0; py < _h; py++)
        {
            int row = py * _w;
            for (var px = 0; px < _w; px++)
            {
                double w1 = Math.Sin((px + py) * freq + t * wSpd) * 0.5 + 0.5;
                double w2 = Math.Sin((px - py) * freq * 1.3 + t * wSpd * 0.7 + 1.2) * 0.5 + 0.5;
                double w3 = Math.Sin(px * freq * 0.8 + t * wSpd * 1.4 + 2.4) * 0.5 + 0.5;
                double wave = w1 * w2 * w3;
                wave = Math.Pow(wave, 2.5);
                wave *= wAmp * bright;
                int idx = row + px;
                _accumR[idx] += (float)(aR * wave * 0.6 + wR * wave * 0.4);
                _accumG[idx] += (float)(aG * wave * 0.6 + wG * wave * 0.4);
                _accumB[idx] += (float)(aB * wave * 0.6 + wB * wave * 0.4);
            }
        }

        // ── Tone-map HDR → LDR + pack BGRA bytes ──────────────────────────
        float bgR = bgSnap.R / 255f;
        float bgG = bgSnap.G / 255f;
        float bgB = bgSnap.B / 255f;
        for (var i = 0; i < pixels; i++)
        {
            // Exponential tone-mapping with lower exposure to preserve color vibrancy
            float r = 1f - MathF.Exp(-_accumR[i] * 0.7f);
            float g = 1f - MathF.Exp(-_accumG[i] * 0.7f);
            float b = 1f - MathF.Exp(-_accumB[i] * 0.7f);

            // Slight gamma lift for glow softness
            r = MathF.Pow(r, 0.85f);
            g = MathF.Pow(g, 0.85f);
            b = MathF.Pow(b, 0.85f);

            // Composite: particle luminance covers background (screen-like blend)
            r = bgR * (1f - r) + r;
            g = bgG * (1f - g) + g;
            b = bgB * (1f - b) + b;
            r = Math.Min(r, 1f);
            g = Math.Min(g, 1f);
            b = Math.Min(b, 1f);
            int px4 = i * 4;
            _pixels[px4] = (byte)(b * 255);
            _pixels[px4 + 1] = (byte)(g * 255);
            _pixels[px4 + 2] = (byte)(r * 255);
            _pixels[px4 + 3] = 255;
        }
    }
}
