using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BreakPulse.Models;

public class AppSettings
{
    // ── Timer ────────────────────────────────────────────────────────────────
    public int    SessionMinutes      { get; set; } = 60;
    public int    BreakMinutes        { get; set; } = 5;
    public int    LongBreakMinutes    { get; set; } = 15;
    public int    SessionsBeforeLong  { get; set; } = 3;
    public int    PreWarningMinutes   { get; set; } = 2;

    // Smart scheduling
    public bool   SkipDuringMeetings  { get; set; } = true;
    public bool   DetectIdle          { get; set; } = true;
    public int    IdleThresholdSecs   { get; set; } = 120;
    public bool   AdaptiveBreaks      { get; set; } = false;
    public bool   AutoResume          { get; set; } = true;

    // ── Alerts ────────────────────────────────────────────────────────────────
    public AlertMode NotificationMode  { get; set; } = AlertMode.Overlay;
    public string    BreakMessage      { get; set; } = "🧘 You've been at it for a while. Stand up, stretch, grab some water.";
    public bool      PlaySound         { get; set; } = true;
    public bool      RequireShortcut   { get; set; } = false;
    public bool      BlockScreenOnBreak{ get; set; } = false;

    // ── Display ───────────────────────────────────────────────────────────────
    public HudPosition Position        { get; set; } = HudPosition.Center;
    public int         HudSize         { get; set; } = 160;
    public double      IdleOpacity     { get; set; } = 0.90;
    public string      AccentColor     { get; set; } = "#6C63FF";
    public bool        ShowProgressArc { get; set; } = true;
    public bool        CollapseToDot   { get; set; } = false;
    public bool        AlwaysOnTop     { get; set; } = true;

    // ── Team / Enterprise ─────────────────────────────────────────────────────
    public bool   TeamTelemetryEnabled { get; set; } = false;
    public string TeamApiEndpoint      { get; set; } = "https://your-api.example.com/api/breakpulse";
    public string TeamApiKey           { get; set; } = "";
    public string UserDisplayName      { get; set; } = Environment.UserName;
    public string TeamName             { get; set; } = "";
    public bool   AnonymizeTelemetry   { get; set; } = true;
    public bool   ManagerDashboardEnabled { get; set; } = false;
    public bool   PushToSlack          { get; set; } = false;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BreakPulse", "settings.json");

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOpts) ?? new AppSettings();
            }
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, _jsonOpts));
        }
        catch { /* swallow — non-critical */ }
    }
}

public enum AlertMode  { Overlay, HudPulse, SoundAndHud, GentleFade }
public enum HudPosition{ Center, TopRight, BottomRight, TopCenter }
