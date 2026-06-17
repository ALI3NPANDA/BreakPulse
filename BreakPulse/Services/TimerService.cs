using System.Runtime.InteropServices;
using System.Windows.Threading;
using BreakPulse.Models;

namespace BreakPulse.Services;

/// <summary>
/// Drives the work-session countdown and fires events for the HUD and overlay.
/// </summary>
public class TimerService
{
    // ── Win32 idle detection ──────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref info);
        return TimeSpan.FromMilliseconds(Environment.TickCount64 - info.dwTime);
    }

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<TimeSpan, double>? TickOccurred; // remaining, 0-1 progress
    /// <summary>
    /// Fires N minutes before a break is due ("pre-warning" for upcoming break).
    /// </summary>
    public event Action? PreWarning;
    public event Action? BreakDue;
    public event Action? BreakEnded;
    /// <summary>
    /// Fires whenever IsOnBreak or IsPaused changes (for UI status dot updates).
    /// </summary>
    public event Action? StatusChanged;

    // ── State ─────────────────────────────────────────────────────────────────
    private AppSettings _s;
    private DispatcherTimer _ticker;
    private ICalendarProvider? _calendarProvider;
    private TimeSpan _sessionLength;
    private TimeSpan _elapsed;
    private TimeSpan _breakLength;
    private bool _onBreak;
    private bool _paused;
    private bool _preWarnFired;
    private bool _breakDueFired; // prevents BreakDue from firing every tick
    private bool _wasIdle;       // tracks whether the user was idle on the previous tick
    private DateTime _idleStartedUtc; // when the idle period began
    private int _sessionCount;
    public bool IsPaused
    {
        get
        {
            return _paused;
        }
    }
    public bool IsOnBreak
    {
        get
        {
            return _onBreak;
        }
    }
    public bool IsLongBreak { get; private set; }
    /// <summary>The actual break duration in effect (regular or long break).</summary>
    public TimeSpan BreakDuration
    {
        get
        {
            return _breakLength;
        }
    }
    public TimeSpan Elapsed
    {
        get
        {
            return _elapsed;
        }
    }
    public int SessionCount
    {
        get
        {
            return _sessionCount;
        }
    }

    public TimerService(AppSettings settings, ICalendarProvider? calendarProvider = null)
    {
        _s = settings;
        _calendarProvider = calendarProvider ?? new WindowsCalendarProvider();
        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ticker.Tick += OnTick;
        ApplySettings(settings);
    }

    public void ApplySettings(AppSettings settings)
    {
        _s = settings;
        _sessionLength = TimeSpan.FromMinutes(settings.SessionMinutes);
        _breakLength = TimeSpan.FromMinutes(settings.BreakMinutes);
        // Don't reset elapsed — let current session continue with new limits
    }

    public void Start()
    {
        _ticker.Start();
    }

    public void Stop()
    {
        _ticker.Stop();
    }

    public void Pause()
    {
        _paused = true;
        StatusChanged?.Invoke();
    }

    public void Resume()
    {
        _paused = false;
        StatusChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (_paused) Resume();
        else Pause();
    }

    /// <summary>Called when user clicks "Take break now" or BreakDue fires in App.</summary>
    public void StartBreak()
    {
        // Increment session count first so IsLongBreakDue() sees the new count
        _sessionCount++;
        IsLongBreak = IsLongBreakDue();
        _breakLength = IsLongBreak
            ? TimeSpan.FromMinutes(_s.LongBreakMinutes)
            : TimeSpan.FromMinutes(_s.BreakMinutes);
        _onBreak = true;
        _elapsed = TimeSpan.Zero;
        _breakDueFired = false;
        StatusChanged?.Invoke();
    }

    public void EndBreak()
    {
        _onBreak = false;
        _elapsed = TimeSpan.Zero;
        _preWarnFired = false;
        _breakDueFired = false;
        IsLongBreak = false;
        BreakEnded?.Invoke();
        StatusChanged?.Invoke();
    }

    public void SnoozeBreak(int minutes = 5)
    {
        // End the current break session and push the work-session clock back
        // so the next break fires `minutes` later than now.
        _onBreak = false;
        _elapsed = _sessionLength - TimeSpan.FromMinutes(minutes);
        if (_elapsed < TimeSpan.Zero) _elapsed = TimeSpan.Zero;
        _preWarnFired = false;
        _breakDueFired = false;
        IsLongBreak = false;
        // Roll back session count since the break was snoozed (not completed)
        if (_sessionCount > 0) _sessionCount--;
        StatusChanged?.Invoke();
    }

    // ── Tick ──────────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_paused) return;

        // Idle detection: if user has been idle longer than threshold, pause the clock
        if (_s.DetectIdle && !_onBreak)
        {
            var idle = GetIdleTime();
            if (idle.TotalSeconds >= _s.IdleThresholdSecs)
            {
                if (!_wasIdle)
                {
                    _wasIdle = true;
                    _idleStartedUtc = DateTime.UtcNow;
                }
                return; // Don't advance timer while idle
            }

            // User is active — check if they just returned from a long idle period
            if (_wasIdle)
            {
                _wasIdle = false;
                var idleDuration = DateTime.UtcNow - _idleStartedUtc;
                // If idle exceeded the reset threshold, restart the session timer
                // so the user gets a full work session before the next break.
                if (_s.IdleResetMinutes > 0 && idleDuration.TotalMinutes >= _s.IdleResetMinutes)
                {
                    _elapsed = TimeSpan.Zero;
                    _preWarnFired = false;
                    _breakDueFired = false;
                }
            }
        }
        _elapsed += TimeSpan.FromSeconds(1);
        var limit = _onBreak ? _breakLength : _sessionLength;
        if (!_onBreak)
        {
            var remaining = limit - _elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            // Pre-warning
            if (!_preWarnFired
                && _s.PreWarningMinutes > 0
                && remaining <= TimeSpan.FromMinutes(_s.PreWarningMinutes))
            {
                _preWarnFired = true;
                PreWarning?.Invoke();
            }
            double progress = _elapsed / limit;
            TickOccurred?.Invoke(remaining, Math.Min(progress, 1.0));
            if (_elapsed >= limit && !_breakDueFired)
            {
                // Check if we should skip the break due to an active meeting
                if (_s.SkipDuringMeetings && _calendarProvider?.IsInMeeting() == true)
                {
                    // User is in a meeting — extend the session by snoozing instead of firing BreakDue
                    // Reset elapsed time to give them another 5 minutes before the next break prompt
                    _elapsed = _sessionLength - TimeSpan.FromMinutes(2);
                    _preWarnFired = false; // Reset pre-warning in case they extended into warning zone
                    _breakDueFired = false;
                    // Don't fire BreakDue — the break is skipped
                }
                else
                {
                    _breakDueFired = true;
                    BreakDue?.Invoke();
                }
            }
        }
        else
        {
            // Break countdown — tick the HUD with break time remaining
            var remaining = limit - _elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            double progress = _elapsed / limit;
            TickOccurred?.Invoke(remaining, Math.Min(progress, 1.0));
            if (_elapsed >= limit && _s.AutoResume)
                EndBreak();
        }
    }

    private bool IsLongBreakDue() // Long break is due if session count is a multiple of SessionsBeforeLong
    {
    return _s.SessionsBeforeLong > 0 && _sessionCount % _s.SessionsBeforeLong == 0;
    }
}
