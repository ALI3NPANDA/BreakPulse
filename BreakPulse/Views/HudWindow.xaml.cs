using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BreakPulse.Models;
using BreakPulse.Services;

namespace BreakPulse.Views;

public partial class HudWindow : Window
{
    private readonly AppSettings _s;
    private readonly TimerService _timer;
    private readonly Action _onSettings;
    private const double Radius = 76; // arc radius (px, inside canvas 152×152)
    private const double CenterXY = 76; // cx = cy

    public HudWindow(AppSettings settings, TimerService timer, Action onSettings)
    {
        InitializeComponent();
        _s = settings;
        _timer = timer;
        _onSettings = onSettings;
        Topmost = settings.AlwaysOnTop;
        Opacity = settings.IdleOpacity;
        DrawTrackArc();
        DrawArc(ArcFill, 0);
        PlaceOnScreen();

        // Reduce opacity while user is typing in other windows
        Deactivated += (_, _) => Opacity = _s.IdleOpacity;
        Activated += (_, _) => Opacity = 1.0;
    }

    // ── Public update called from App on every tick ────────────────────────

    public void UpdateProgress(TimeSpan remaining, double progress)
    {
        TimeLabel.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
        if (_timer.IsOnBreak)
        {
            SubLabel.Text = _timer.IsLongBreak ? "long break" : "break remaining";
            ArcBrush.Color = Color.FromRgb(0, 229, 160); // green
        }
        else
        {
            SubLabel.Text = "until break";
            ArcBrush.Color = progress switch
            {
                < 0.7 => Color.FromRgb(108, 99, 255), // violet
                < 0.9 => Color.FromRgb(245, 158, 11), // amber
                _ => Color.FromRgb(239, 68, 68), // red
            };
        }
        DrawArc(ArcFill, progress);

        // Status dot color
        StatusDot.Fill = _timer.IsPaused
            ? new SolidColorBrush(Color.FromRgb(245, 158, 11))
            : new SolidColorBrush(Color.FromRgb(0, 229, 160));
    }

    // ── Arc drawing ────────────────────────────────────────────────────────

    private void DrawTrackArc()
    {
        ArcTrack.Data = BuildArcGeometry(0, 359.99); // full circle track
    }

    private void DrawArc(System.Windows.Shapes.Path path, double progress)
    {
        if (!_s.ShowProgressArc)
        {
            path.Visibility = Visibility.Hidden;
            return;
        }
        path.Visibility = Visibility.Visible;
        double sweep = Math.Max(0, Math.Min(progress * 360, 359.99));
        path.Data = sweep < 0.1 ? null : BuildArcGeometry(0, sweep);
    }

    private static Geometry BuildArcGeometry(double startDeg, double sweepDeg)
    {
        double startRad = (startDeg - 90) * Math.PI / 180;
        double endRad = (startDeg + sweepDeg - 90) * Math.PI / 180;
        var startPt = new Point(CenterXY + Radius * Math.Cos(startRad),
            CenterXY + Radius * Math.Sin(startRad));
        var endPt = new Point(CenterXY + Radius * Math.Cos(endRad),
            CenterXY + Radius * Math.Sin(endRad));
        bool isLarge = sweepDeg > 180;
        var figure = new PathFigure { StartPoint = startPt };
        figure.Segments.Add(new ArcSegment(endPt,
            new Size(Radius, Radius),
            0, isLarge,
            SweepDirection.Clockwise,
            true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        return geo;
    }

    // ── Window placement ───────────────────────────────────────────────────

    private void PlaceOnScreen()
    {
        var screen = SystemParameters.WorkArea;
        const double margin = 20;
        (Left, Top) = _s.Position switch
        {
            HudPosition.TopRight => (screen.Right - Width - margin, screen.Top + margin),
            HudPosition.BottomRight => (screen.Right - Width - margin, screen.Bottom - Height - margin),
            HudPosition.TopCenter => (screen.Left + (screen.Width - Width) / 2, screen.Top + margin),
            _ => (screen.Left + (screen.Width - Width) / 2,
                screen.Top + (screen.Height - Height) / 2),
        };
    }

    // ── Input ─────────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MenuPause_Click(object sender, RoutedEventArgs e)
    {
        _timer.TogglePause();
        MenuPause.Header = _timer.IsPaused ? "▶  Resume" : "⏸  Pause";
    }

    private void MenuBreak_Click(object sender, RoutedEventArgs e)
    {
        _timer.StartBreak();
        var overlay = new BreakOverlay(_s, _timer);
        overlay.ShowDialog();
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        _onSettings();
    }
}
