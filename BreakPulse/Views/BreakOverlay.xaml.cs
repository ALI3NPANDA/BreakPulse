using System.Windows;
using System.Windows.Input;
using BreakPulse.Models;
using BreakPulse.Services;

namespace BreakPulse.Views;

public partial class BreakOverlay : Window
{
    private readonly AppSettings  _s;
    private readonly TimerService _timer;

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
        _s     = settings;
        _timer = timer;

        // Set title based on break type
        if (timer.IsLongBreak)
        {
            TitleLabel.Text = "🌟 Long Break Time!";
            MessageLabel.Text = "You've earned a proper rest. Step away, recharge fully.";
        }
        else
        {
            TitleLabel.Text   = "Time to rest";
            MessageLabel.Text = settings.BreakMessage;
        }

        ExerciseLabel.Text = Exercises[Random.Shared.Next(Exercises.Length)];

        if (settings.RequireShortcut)
        {
            ShortcutHint.Visibility = Visibility.Visible;
            SnoozeBtn.IsEnabled     = false;
            Continue.IsEnabled      = false;
        }

        // Show initial countdown from the timer's actual break duration
        UpdateCountdownLabel(timer.BreakDuration);

        // Subscribe to timer events instead of maintaining a separate countdown
        _timer.TickOccurred  += OnTimerTick;
        _timer.BreakEnded    += OnBreakEndedExternally;

        KeyDown += OnKeyDown;
        Closed  += OnWindowClosed;
    }

    // ── Timer event handlers ───────────────────────────────────────────────

    private void OnTimerTick(TimeSpan remaining, double progress)
    {
        if (_timer.IsOnBreak)
            Dispatcher.Invoke(() => UpdateCountdownLabel(remaining));
    }

    private void OnBreakEndedExternally()
    {
        // Auto-resume fired from TimerService — close overlay if still open
        Dispatcher.Invoke(() =>
        {
            if (IsVisible) Close();
        });
    }

    private void UpdateCountdownLabel(TimeSpan remaining)
    {
        var breakType = _timer.IsLongBreak ? "Long break" : "Break";
        CountdownLabel.Text =
            $"{breakType} ends in {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    // ── Cleanup event subscriptions when window closes ─────────────────────

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _timer.TickOccurred  -= OnTimerTick;
        _timer.BreakEnded    -= OnBreakEndedExternally;
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        _timer.SnoozeBreak(5);
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        EndBreakAndClose();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_s.RequireShortcut) return;
        if (e.Key == Key.B
            && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            EndBreakAndClose();
        }
    }

    // Close/Skip button handler
    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        EndBreakAndClose();
    }

    private void EndBreakAndClose()
    {
        _timer.EndBreak();
        Close();
    }
}
