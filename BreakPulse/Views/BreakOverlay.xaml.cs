using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BreakPulse.Models;
using BreakPulse.Services;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace BreakPulse.Views;

public partial class BreakOverlay : Window
{
    private readonly AppSettings  _s;
    private readonly TimerService _timer;

    // ── Arc constants (canvas 460×460, large ring near circle edge) ──────────
    private const double BreakArcRadius   = 222.0;
    private const double BreakArcCenterXY = 230.0; // center of 460×460 canvas

    // Windows accent color, read once at construction
    private readonly Color _accentColor;

    // WebView2 instances created in code to avoid XAML assembly-resolution issues
    private readonly WebView2 _iconView     = new();
    private readonly WebView2 _titleView    = new();
    private readonly WebView2 _exerciseView = new();

    private string _titleHtml    = "";
    private string _exerciseHtml = "";

    private static readonly string[] Exercises =
    [
        "👀 Look at something 20 feet away for 20 seconds — rest your eyes.",
        "🧍‍♂️ Roll your shoulders back 5 times, then forward 5 times.",
        "💧 Grab a glass of water — hydration helps focus.",
        "🚶 Take a short walk — even 2 minutes around the room helps.",
        "🙆‍♂️ Reach both arms up, hold for 5 seconds, release and breathe.",
        "😌 Close your eyes, take 5 slow deep breaths.",
        "🦵 Stand up, do 10 calf raises.",
        "🤲 Shake out your hands — relieve typing tension.",
    ];

    public BreakOverlay(AppSettings settings, TimerService timer)
    {
        InitializeComponent();
        _s           = settings;
        _timer       = timer;
        _accentColor = GetWindowsAccentColor();

        // Inject the WebView2 controls into their placeholder slots
        IconViewHost.Content     = _iconView;
        TitleViewHost.Content    = _titleView;
        ExerciseViewHost.Content = _exerciseView;

        // Build HTML content
        if (timer.IsLongBreak)
        {
            _titleHtml        = EmojiHtml("🌟 Long Break Time!", "#F0EFF5", 22, "600");
            MessageLabel.Text = "You've earned a proper rest. Step away, recharge fully.";
        }
        else
        {
            _titleHtml        = EmojiHtml("Time to rest", "#F0EFF5", 22, "600");
            MessageLabel.Text = settings.BreakMessage;
        }

        _exerciseHtml = EmojiHtml(Exercises[Random.Shared.Next(Exercises.Length)], "#9997AA", 13);

        if (settings.RequireShortcut)
        {
            ShortcutHint.Visibility = Visibility.Visible;
            SnoozeBtn.IsEnabled     = false;
            Continue.IsEnabled      = false;
        }

        UpdateCountdownLabel(timer.BreakDuration);

        _timer.TickOccurred += OnTimerTick;
        _timer.BreakEnded   += OnBreakEndedExternally;

        KeyDown += OnKeyDown;
        Loaded  += OnLoaded;
        Closed  += OnWindowClosed;
    }

    // ── Windows accent color ──────────────────────────────────────────────────

    private static Color GetWindowsAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int argb)
            {
                // Windows stores it as little-endian ABGR
                byte r = (byte)( argb        & 0xFF);
                byte g = (byte)((argb >>  8) & 0xFF);
                byte b = (byte)((argb >> 16) & 0xFF);
                return Color.FromRgb(r, g, b);
            }
        }
        catch { /* ignore */ }
        return Color.FromRgb(108, 99, 255); // fallback violet
    }

    // ── Arc drawing ───────────────────────────────────────────────────────────

    private void DrawTrackArc()
    {
        ArcTrack.Data = BuildArcGeometry(0, 359.99); // full circle track
    }

    // progress: 0 = break just started (full ring), 1 = break over (empty ring).
    // The gap at 12 o'clock grows clockwise so the arc drains in a clockwise direction.
    private void DrawBreakArc(double progress)
    {
        var remaining = 1.0 - progress;
        var sweep     = Math.Max(0, Math.Min(remaining * 360, 359.99));
        if (sweep < 0.1) { ArcFill.Data = null; return; }
        // Start point advances clockwise from 12 o'clock as the break elapses.
        var startDeg = Math.Min(progress * 360, 359.99);
        ArcFill.Data = BuildArcGeometry(startDeg, sweep);
    }

    private static Geometry BuildArcGeometry(double startDeg, double sweepDeg)
    {
        var startRad = (startDeg - 90) * Math.PI / 180;
        var endRad   = (startDeg + sweepDeg - 90) * Math.PI / 180;

        var startPt = new Point(
            BreakArcCenterXY + BreakArcRadius * Math.Cos(startRad),
            BreakArcCenterXY + BreakArcRadius * Math.Sin(startRad));
        var endPt = new Point(
            BreakArcCenterXY + BreakArcRadius * Math.Cos(endRad),
            BreakArcCenterXY + BreakArcRadius * Math.Sin(endRad));

        var isLarge = sweepDeg > 180;
        var figure  = new PathFigure { StartPoint = startPt };
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

    // ── WebView2 initialisation ───────────────────────────────────────────────

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // ── Fade-in animation (starts immediately) ───────────────────────────
        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(600)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        // ── Apply colors ─────────────────────────────────────────────────────
        // White arc is always visible against the violet→teal gradient background.
        ArcBrush.Color            = Colors.White;
        // Keep CountdownLabel white regardless of Windows accent color.
        CountdownLabel.Foreground = new SolidColorBrush(Colors.White);
        // Do NOT override BgCircleBrush — let the XAML violet→teal gradient show as designed.

        // ── Progress ring ────────────────────────────────────────────────────
        DrawTrackArc();
        DrawBreakArc(0); // progress=0 → full ring at start

        // ── WebView2 setup ───────────────────────────────────────────────────
        _iconView.DefaultBackgroundColor     = System.Drawing.Color.Transparent;
        _titleView.DefaultBackgroundColor    = System.Drawing.Color.Transparent;
        _exerciseView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

        await Task.WhenAll(
            _iconView.EnsureCoreWebView2Async(),
            _titleView.EnsureCoreWebView2Async(),
            _exerciseView.EnsureCoreWebView2Async());

        foreach (var core in new[] { _iconView.CoreWebView2, _titleView.CoreWebView2, _exerciseView.CoreWebView2 })
        {
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled            = false;
            core.Settings.IsStatusBarEnabled            = false;
        }

        _iconView.NavigateToString(EmojiHtml("🧘", "#F0EFF5", 48));
        _titleView.NavigateToString(_titleHtml);
        _exerciseView.NavigateToString(_exerciseHtml);
    }

    // Builds a transparent-background HTML page that renders color emoji via Chromium.
    private static string EmojiHtml(string text, string color, int fontSize, string fontWeight = "normal")
    {
        var encoded = WebUtility.HtmlEncode(text);
        return $$"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"><style>
              * { margin:0; padding:0; box-sizing:border-box; }
              html, body {
                height: 100%;
                background: transparent;
                overflow: hidden;
                font-family: 'Segoe UI Emoji','Segoe UI',sans-serif;
                font-size: {{fontSize}}px;
                font-weight: {{fontWeight}};
                color: {{color}};
                text-align: center;
                line-height: 1.45;
              }
              body { display:flex; align-items:center; justify-content:center; }
            </style></head>
            <body>{{encoded}}</body></html>
            """;
    }

    // ── Timer event handlers ──────────────────────────────────────────────────

    private void OnTimerTick(TimeSpan remaining, double progress)
    {
        if (_timer.IsOnBreak)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateCountdownLabel(remaining);
                DrawBreakArc(progress); // start advances clockwise; arc drains clockwise

                // Color: white → amber → red as break time runs out
                // (white always contrasts against the violet/teal gradient background)
                var arcColor = progress switch
                {
                    < 0.7 => Colors.White,                          // white — plenty of time
                    < 0.9 => Color.FromRgb(245, 158,  11),          // amber — almost done
                    _     => Color.FromRgb(239,  68,  68)           // red   — last 10 %
                };
                ArcBrush.Color            = arcColor;
                CountdownLabel.Foreground = new SolidColorBrush(arcColor);
            });
        }
    }

    private void OnBreakEndedExternally()
    {
        Dispatcher.Invoke(() => { if (IsVisible) Close(); });
    }

    private void UpdateCountdownLabel(TimeSpan remaining)
    {
        CountdownLabel.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _timer.TickOccurred -= OnTimerTick;
        _timer.BreakEnded   -= OnBreakEndedExternally;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
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
        _timer.SnoozeBreak(5);
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => EndBreakAndClose();
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => EndBreakAndClose();

    private void MenuSnooze_Click(object sender, RoutedEventArgs e)
    {
        _timer.SnoozeBreak(5);
        Close();
    }

    private void MenuEndBreak_Click(object sender, RoutedEventArgs e) => EndBreakAndClose();

    private void EndBreakAndClose()
    {
        _timer.EndBreak();
        Close();
    }
}
