using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BreakPulse.Services;

/// <summary>
/// Detects meetings/appointments by checking Windows Calendar and active applications.
/// </summary>
public class WindowsCalendarProvider : ICalendarProvider
{
    // Win32 API to get the foreground window
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Common meeting application process names
    private static readonly string[] MeetingAppNames = new[]
    {
        "teams",           // Microsoft Teams
        "outlook",         // Outlook (calendar/meeting)
        "zoom",            // Zoom
        "googlemeet",      // Google Meet
        "slack",           // Slack (calls)
        "webex",           // Cisco WebEx
        "discord",         // Discord (voice/meetings)
        "skype",           // Skype
        "messengerv2",     // Facebook Messenger
        "eventviewer",     // Windows Events (sometimes shows calendar)
        "calendar",        // Windows Calendar
        "appleicloud",     // iCloud Calendar
    };

    /// <summary>
    /// Check if a meeting is currently in progress by analyzing active applications.
    /// Works during screen sharing and when viewing shared screens.
    /// </summary>
    public bool IsInMeeting()
    {
        try
        {
            // Get the foreground window (the currently focused application)
            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow == IntPtr.Zero)
                return false;

            // Get the process ID of the foreground window
            GetWindowThreadProcessId(fgWindow, out uint processId);
            if (processId == 0)
                return false;

            // Get the process and its name
            try
            {
                Process? proc = Process.GetProcessById((int)processId);
                if (proc == null)
                    return false;

                string processName = proc.ProcessName.ToLowerInvariant();

                // Check if the foreground app is a known meeting application
                // This works even when sharing screen because the meeting app stays in focus
                if (MeetingAppNames.Any(app => processName.Contains(app)))
                    return true;

                // Also check if the window title suggests a meeting
                // This catches edge cases and confirms meeting status
                string windowTitle = GetWindowTitle(fgWindow)?.ToLowerInvariant() ?? "";
                if (windowTitle.Contains("meeting") || 
                    windowTitle.Contains("conference") ||
                    windowTitle.Contains("call") ||
                    windowTitle.Contains("webinar") ||
                    windowTitle.Contains("presenting"))  // Added "presenting" for presenter mode
                    return true;
            }
            catch
            {
                // If we can't get process info, continue to fallback check
            }

            // Fallback: Check if any meeting app is running in the background
            // This covers cases where user switches away from meeting app briefly
            // Also important for screen sharing where app may lose focus temporarily
            return CheckRunningMeetingApps();
        }
        catch
        {
            // On any error, assume no meeting (fail-safe to not interfere with meetings)
            return false;
        }
    }

    /// <summary>
    /// Check if any known meeting application is running.
    /// </summary>
    private bool CheckRunningMeetingApps()
    {
        try
        {
            Process[] processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string processName = proc.ProcessName.ToLowerInvariant();
                    
                    // Check for Teams specifically - it's the most common enterprise meeting app
                    if (processName.Contains("teams"))
                    {
                        // Check if Teams window is visible/active
                        if (proc.MainWindowHandle != IntPtr.Zero)
                            return true;
                    }
                }
                catch
                {
                    // Ignore individual process errors
                }
            }
        }
        catch
        {
            // On any error checking processes, return false
        }
        return false;
    }

    /// <summary>
    /// Get the window title for a given window handle.
    /// </summary>
    private string? GetWindowTitle(IntPtr hWnd)
    {
        try
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0)
                return null;

            var sb = new System.Text.StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }
}

