namespace BreakPulse.Services;

/// <summary>
/// Detects meetings/appointments by checking if meeting apps are actively using mic/camera.
/// </summary>
public class WindowsCalendarProvider : ICalendarProvider
{
    // Common meeting application process names and registry identifiers
    private static readonly string[] MeetingAppNames = new[]
    {
        "teams",           // Microsoft Teams (classic and new)
        "msteams",         // New Microsoft Teams (packaged app registry name)
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
    /// Uses mic/camera usage as the primary reliable indicator.
    /// </summary>
    public bool IsInMeeting()
    {
        try
        {
            // Check if a meeting app is actively using the microphone or camera.
            // This is the most reliable indicator regardless of window title.
            // When the meeting ends, Windows updates LastUsedTimeStop to non-zero.
            return IsMeetingAppUsingMediaDevice();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a meeting app is actively using the microphone or camera,
    /// which reliably indicates an active meeting regardless of window title.
    /// </summary>
    private bool IsMeetingAppUsingMediaDevice()
    {
        try
        {
            string[] registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"
            };

            foreach (var basePath in registryPaths)
            {
                if (CheckMediaRegistryKey(Microsoft.Win32.Registry.CurrentUser, basePath))
                    return true;
                if (CheckMediaRegistryKey(Microsoft.Win32.Registry.LocalMachine, basePath))
                    return true;
            }
        }
        catch
        {
            // Registry access failed, fall through
        }
        return false;
    }

    private bool CheckMediaRegistryKey(Microsoft.Win32.RegistryKey root, string basePath)
    {
        using var key = root.OpenSubKey(basePath);
        if (key == null) return false;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            if (subKeyName == "NonPackaged")
            {
                using var nonPackagedKey = key.OpenSubKey("NonPackaged");
                if (nonPackagedKey == null) continue;

                foreach (var appKey in nonPackagedKey.GetSubKeyNames())
                {
                    string appLower = appKey.ToLowerInvariant();
                    if (MeetingAppNames.Any(app => appLower.Contains(app)))
                    {
                        if (IsDeviceCurrentlyInUse(nonPackagedKey, appKey))
                            return true;
                    }
                }
            }
            else
            {
                string subLower = subKeyName.ToLowerInvariant();
                if (MeetingAppNames.Any(app => subLower.Contains(app)))
                {
                    if (IsDeviceCurrentlyInUse(key, subKeyName))
                        return true;
                }
            }
        }
        return false;
    }

    private static bool IsDeviceCurrentlyInUse(Microsoft.Win32.RegistryKey parentKey, string subKeyName)
    {
        using var appSubKey = parentKey.OpenSubKey(subKeyName);
        var lastUsedStop = appSubKey?.GetValue("LastUsedTimeStop");
        // LastUsedTimeStop == 0 means the device is currently in use
        if (lastUsedStop is long stopLong && stopLong == 0)
            return true;
        if (lastUsedStop is int stopInt && stopInt == 0)
            return true;
        return false;
    }

    }

