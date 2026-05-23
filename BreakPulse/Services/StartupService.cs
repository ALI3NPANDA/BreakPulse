using Microsoft.Win32;
using System.Diagnostics;

namespace BreakPulse.Services;

/// <summary>
/// Manages adding/removing the app from Windows startup via registry
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BreakPulse";

    /// <summary>
    /// Enable or disable launch on startup by modifying Windows registry
    /// </summary>
    public static void SetLaunchOnStartup(bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)!)
            {
                if (key == null)
                {
                    Debug.WriteLine("Failed to open registry key for startup");
                    return;
                }

                if (enabled)
                {
                    // Get the path to the current executable
                    string exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(AppName, exePath);
                }
                else
                {
                    // Remove the entry
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting startup registry: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if app is currently set to launch on startup
    /// </summary>
    public static bool IsLaunchOnStartupEnabled()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey)!)
            {
                return key?.GetValue(AppName) != null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading startup registry: {ex.Message}");
            return false;
        }
    }
}

