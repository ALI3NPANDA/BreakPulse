using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;
using BreakPulse.Models;
using BreakPulse.Rendering;
using BreakPulse.Services;

namespace BreakPulse.Views;

public partial class BreakOverlay : Window
{
    private readonly AppSettings _s;
    private readonly TimerService _timer;
    
    // ── Icon source property ──────────────────────────────────────────────
    public BitmapImage? IconSource { get; private set; }

    // ── Arc constants (canvas 460×460, large ring near circle edge) ──────────
    private const double BreakArcRadius = 222.0;
    private const double BreakArcCenterXY = 230.0; // center of 460×460 canvas

    // Background renderers (only one is active based on settings)
    private readonly ParticleWaveRenderer? _particleRenderer;
    private readonly LightweightBackgroundRenderer? _lightRenderer;


    public BreakOverlay(AppSettings settings, TimerService timer)
    {
        InitializeComponent();
        DataContext = this;
        _s = settings;
        _timer = timer;

         // Initialize background renderer (460×460 to match the circle)
         // DISABLED: High quality animation removed (causes exercise text to be unreadable)
         // if (_s.UseHighQualityAnimation)
         // {
         //     _particleRenderer = new ParticleWaveRenderer(460, 460);
         //     ApplyColorsToRenderer(_particleRenderer);
         //     ParticleBgImage.Source = _particleRenderer.Bitmap;
         // }
         // else
         // {
             _lightRenderer = new LightweightBackgroundRenderer(460, 460);
             ApplyColorsToLightRenderer(_lightRenderer);
             ParticleBgImage.Source = _lightRenderer.Bitmap;
         // }

        // ── Fullscreen blocking mode ──────────────────────────────────────────
        if (_s.BlockScreenOnBreak)
        {
            ApplyFullscreenBlocking();
        }

     // Set the exercise text in TitleViewHost
         TitleViewHost.Text = PickRandomExercise(settings.Exercises);
         
         // Set random icon from PNG files
         IconSource = PickRandomIconImage();
         IconViewHost.Source = IconSource;
         
         if (settings.RequireShortcut)
        {
            ShortcutHint.Visibility = Visibility.Visible;
            SnoozeBtn.IsEnabled = false;
            Continue.IsEnabled = false;
        }
        UpdateCountdownLabel(timer.BreakDuration);
        _timer.TickOccurred += OnTimerTick;
        _timer.BreakEnded += OnBreakEndedExternally;
        KeyDown += OnKeyDown;
        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    // ── Fullscreen blocking ─────────────────────────────────────────────────

    private void ApplyFullscreenBlocking()
    {
        // Span window across all monitors using virtual screen bounds
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        // Show the blocking background
        BlockingBackground.Visibility = Visibility.Visible;
        BlockingBackground.Opacity = _s.BlockScreenOpacity;
        try
        {
            var blockColor = (Color)ColorConverter.ConvertFromString(_s.BlockScreenColor);
            BlockingBackground.Background = new SolidColorBrush(blockColor);
        }
        catch { BlockingBackground.Background = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)); }
        Topmost = true;

        // Prevent Alt+F4
        Closing += (_, args) =>
        {
            // Only allow closing via our EndBreakAndClose method
            if (_timer.IsOnBreak && !_closingIntentionally)
                args.Cancel = true;
        };
    }

    private bool _closingIntentionally;

    // ── Arc drawing ───────────────────────────────────────────────────────────

    private void DrawTrackArc()
    {
        ArcTrack.Data = BuildArcGeometry(0, 359.99); // full circle track
    }

    // progress: 0 = break just started (full ring), 1 = break over (empty ring).
    // The gap at 12 o'clock grows clockwise so the arc drains in a clockwise direction.
    private void DrawBreakArc(double progress)
    {
        double remaining = 1.0 - progress;
        double sweep = Math.Max(0, Math.Min(remaining * 360, 359.99));
        if (sweep < 0.1)
        {
            ArcFill.Data = null;
            ArcGlow.Data = null;
            return;
        }
        double startDeg = Math.Min(progress * 360, 359.99);
        var geo = BuildArcGeometry(startDeg, sweep);
        ArcFill.Data = geo;
        ArcGlow.Data = geo;
    }

    private static Geometry BuildArcGeometry(double startDeg, double sweepDeg)
    {
        double startRad = (startDeg - 90) * Math.PI / 180;
        double endRad = (startDeg + sweepDeg - 90) * Math.PI / 180;
        var startPt = new Point(
            BreakArcCenterXY + BreakArcRadius * Math.Cos(startRad),
            BreakArcCenterXY + BreakArcRadius * Math.Sin(startRad));
        var endPt = new Point(
            BreakArcCenterXY + BreakArcRadius * Math.Cos(endRad),
            BreakArcCenterXY + BreakArcRadius * Math.Sin(endRad));
        bool isLarge = sweepDeg > 180;
        var figure = new PathFigure { StartPoint = startPt };
        figure.Segments.Add(new ArcSegment(
            endPt,
            new Size(BreakArcRadius, BreakArcRadius),
            0, isLarge,
            SweepDirection.Clockwise,
            true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        return geo;
    }

    // ── Window initialization ─────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // ── Fade-in animation (starts immediately) ───────────────────────────
        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(700)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(OpacityProperty, fadeIn);

        // ── Start ambient pulse glow ─────────────────────────────────────────
        var pulse = (Storyboard)FindResource("PulseGlow");
        pulse.Begin();

        // ── Apply colors ─────────────────────────────────────────────────────
        // Apply accent color to outer glow
        try
        {
            var accentColor = (Color)ColorConverter.ConvertFromString(_s.ParticleAccentColor);
            OuterGlowBrush.GradientStops[0].Color = accentColor;
        }
        catch { /* keep defaults */ }

         // Start the active background renderer
         // DISABLED: High quality animation removed
         // if (_particleRenderer != null)
         // {
         //     ApplyColorsToRenderer(_particleRenderer);
         //     _particleRenderer.Start();
         // }
         // else 
         if (_lightRenderer != null)
         {
             ApplyColorsToLightRenderer(_lightRenderer);
             _lightRenderer.Start();
         }

        // White arc always contrasts against the dark gradient
        ArcBrush.Color = Colors.White;
        ArcGlowBrush.Color = Colors.White;
        CountdownLabel.Foreground = new SolidColorBrush(Colors.White);

        // Apply visibility toggles
        var countdownVis = _s.ShowCountdown ? Visibility.Visible : Visibility.Collapsed;
        CountdownLabel.Visibility = countdownVis;
        RemainingLabel.Visibility = countdownVis;
        Topmost = _s.AlwaysOnTop;

        // ── Progress ring ────────────────────────────────────────────────────
        DrawTrackArc();
        DrawBreakArc(0); // progress=0 → full ring at start
        ArcGlow.Data = ArcFill.Data; // sync glow with fill
    }

     // Picks a random non-empty exercise; falls back to a default if all are empty.
     private static string PickRandomExercise(IList<string> exercises)
     {
         List<string> active = exercises.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
         return active.Count > 0
             ? active[Random.Shared.Next(active.Count)]
             : "Take a moment to breathe and stretch.";
     }

      // Picks a random PNG image from the Assets folder.
      private BitmapImage? PickRandomIconImage()
      {
          try
          {
              // Get the assets folder path
              var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/png");
              if (!Directory.Exists(assetsPath))
                  return null;

              // Get all PNG files
              var pngFiles = Directory.GetFiles(assetsPath, "*.png");
              if (pngFiles.Length == 0)
                  return null;

              // Pick a random PNG file
              var randomPng = pngFiles[Random.Shared.Next(pngFiles.Length)];
              var bitmap = new BitmapImage();
              bitmap.BeginInit();
              bitmap.UriSource = new Uri(randomPng);
              bitmap.CacheOption = BitmapCacheOption.OnLoad;
              bitmap.EndInit();
              bitmap.Freeze();
              return bitmap;
          }
          catch
          {
              // If anything goes wrong, return null (XAML will use fallback)
              return null;
          }
      }

    private void OnTimerTick(TimeSpan remaining, double progress)
    {
        if (_timer.IsOnBreak)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateCountdownLabel(remaining);
                DrawBreakArc(progress); // start advances clockwise; arc drains clockwise

                // Color: white → amber → red as break time runs out
                var arcColor = progress switch
                {
                    < 0.7 => Colors.White, // white — plenty of time
                    < 0.9 => Color.FromRgb(245, 158, 11), // amber — almost done
                    _ => Color.FromRgb(239, 68, 68), // red   — last 10 %
                };
                ArcBrush.Color = arcColor;
                ArcGlowBrush.Color = arcColor;
                CountdownLabel.Foreground = new SolidColorBrush(arcColor);
            });
        }
    }

    private void OnBreakEndedExternally()
    {
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                _closingIntentionally = true;
                Close();
            }
        });
    }

    private void UpdateCountdownLabel(TimeSpan remaining)
    {
        CountdownLabel.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    // ── Color helpers ────────────────────────────────────────────────────────

    private void ApplyColorsToRenderer(ParticleWaveRenderer renderer)
    {
        try { renderer.AccentColor = (Color)ColorConverter.ConvertFromString(_s.ParticleAccentColor); } catch { }
        try { renderer.WaveColor = (Color)ColorConverter.ConvertFromString(_s.ParticleWaveColor); } catch { }
        try { renderer.BackgroundColor = (Color)ColorConverter.ConvertFromString(_s.OverlayBackgroundColor); } catch { }
    }

    private void ApplyColorsToLightRenderer(LightweightBackgroundRenderer renderer)
    {
        try { renderer.AccentColor = (Color)ColorConverter.ConvertFromString(_s.ParticleAccentColor); } catch { }
        try { renderer.WaveColor = (Color)ColorConverter.ConvertFromString(_s.ParticleWaveColor); } catch { }
        try { renderer.BackgroundColor = (Color)ColorConverter.ConvertFromString(_s.OverlayBackgroundColor); } catch { }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _timer.TickOccurred -= OnTimerTick;
        _timer.BreakEnded -= OnBreakEndedExternally;
        _particleRenderer?.Dispose();
        _lightRenderer?.Dispose();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !_s.BlockScreenOnBreak)
            DragMove();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_s.RequireShortcut) return;
        if (e.Key == Key.B && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            EndBreakAndClose();
    }

    // ── Button / menu handlers ────────────────────────────────────────────────

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        _closingIntentionally = true;
        _timer.SnoozeBreak(5);
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        EndBreakAndClose();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        EndBreakAndClose();
    }

    private void MenuSnooze_Click(object sender, RoutedEventArgs e)
    {
        _closingIntentionally = true;
        _timer.SnoozeBreak(5);
        Close();
    }

    private void MenuEndBreak_Click(object sender, RoutedEventArgs e)
    {
        EndBreakAndClose();
    }

    private void EndBreakAndClose()
    {
        _closingIntentionally = true;
        _timer.EndBreak();
        Close();
    }
}
