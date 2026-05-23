using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BreakPulse.Models;

public class AppSettings
{
    // ── Timer ────────────────────────────────────────────────────────────────
    public int SessionMinutes { get; set; } = 60;
    public int BreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsBeforeLong { get; set; } = 3;
    public int PreWarningMinutes { get; set; } = 2;

    // Smart scheduling
    public bool SkipDuringMeetings { get; set; } = true;
    public bool DetectIdle { get; set; } = true;
    public int IdleThresholdSecs { get; set; } = 120;
    public bool AutoResume { get; set; } = true;

    // ── Alerts ────────────────────────────────────────────────────────────────
    public AlertMode NotificationMode { get; set; } = AlertMode.Overlay;
    public bool PlaySound { get; set; } = true;

    // Exercise messages shown randomly during breaks (10 fixed slots; empty = skipped)
    public List<string> Exercises { get; set; } = new()
    {
        "👀 Look at something 20 feet away for 20 seconds — rest your eyes.",
        "🧍‍♂️ Roll your shoulders back 5 times, then forward 5 times.",
        "💧 Grab a glass of water — hydration helps focus.",
        "🚶 Take a short walk — even 2 minutes around the room helps.",
        "🙆‍♂️ Reach both arms up, hold for 5 seconds, release and breathe.",
        "😌 Close your eyes, take 5 slow deep breaths.",
        "🦵 Stand up, do 10 calf raises.",
        "🤲 Shake out your hands — relieve typing tension.",
        "",
        "",
    };
    public bool RequireShortcut { get; set; } = false;
    public bool BlockScreenOnBreak { get; set; } = false;

    // ── Startup ───────────────────────────────────────────────────────────────
    public bool LaunchOnStartup { get; set; } = false;

    // ── Display ───────────────────────────────────────────────────────────────
    public HudPosition Position { get; set; } = HudPosition.Center;
    public int HudSize { get; set; } = 160;
    public double IdleOpacity { get; set; } = 0.90;
    public string AccentColor { get; set; } = "#6C63FF";
    public bool ShowProgressArc { get; set; } = true;
    public bool CollapseToDot { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = true;

    // ── Appearance ────────────────────────────────────────────────────────────
    public string OverlayBackgroundColor { get; set; } = "#0D0D11";
    public string ParticleAccentColor { get; set; } = "#6C63FF";
    public string ParticleWaveColor { get; set; } = "#00C8B4";
    public string BlockScreenColor { get; set; } = "#404040";
    public double BlockScreenOpacity { get; set; } = 0.85;
    public double ArcThickness { get; set; } = 4.0;
    public bool ShowCountdown { get; set; } = true;
    public bool ShowExercise { get; set; } = true;

    // ── Team / Enterprise ─────────────────────────────────────────────────────
    public bool TeamTelemetryEnabled { get; set; } = false;
    public string TeamApiEndpoint { get; set; } = "https://your-api.example.com/api/breakpulse";
    public string TeamApiKey { get; set; } = "";
    public string UserDisplayName { get; set; } = Environment.UserName;
    public string TeamName { get; set; } = "";
    public bool AnonymizeTelemetry { get; set; } = true;
    public bool ManagerDashboardEnabled { get; set; } = false;
    public bool PushToSlack { get; set; } = false;

    // ── Persistence ───────────────────────────────────────────────────────────
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BreakPulse", "settings.json");
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOpts) ?? new AppSettings();
            }
        }
        catch
        { /* fall through to defaults */
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, _jsonOpts), System.Text.Encoding.UTF8);
        }
        catch
        { /* swallow — non-critical */
        }
    }
}

public enum AlertMode
{
    Overlay,
    HudPulse,
    SoundAndHud,
    GentleFade,
}

public enum HudPosition
{
    Center,
    TopRight,
    BottomRight,
    TopCenter,
}
