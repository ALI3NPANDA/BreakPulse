namespace BreakPulse.Services;

/// <summary>
/// Provides calendar/meeting detection for skipping breaks during meetings.
/// </summary>
public interface ICalendarProvider
{
    /// <summary>
    /// Check if a meeting/appointment is currently in progress.
    /// </summary>
    /// <returns>True if a meeting is happening now, false otherwise.</returns>
    bool IsInMeeting();
}

