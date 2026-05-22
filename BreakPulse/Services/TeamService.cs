using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BreakPulse.Models;

namespace BreakPulse.Services;

/// <summary>
/// Pushes session heartbeats to your microservice and fetches team status.
/// The API contract is intentionally simple — a single REST endpoint that
/// accepts POST heartbeats and returns GET team status as JSON arrays.
///
/// Expected endpoints:
///   POST {ApiEndpoint}/heartbeat   — body: HeartbeatPayload
///   GET  {ApiEndpoint}/team        — returns TeamMember[]
/// </summary>
public class TeamService
{
    private readonly AppSettings _s;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public TeamService(AppSettings settings)
    {
        _s = settings;
        _http = new HttpClient();
        if (!string.IsNullOrWhiteSpace(settings.TeamApiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", settings.TeamApiKey);
    }

    /// <summary>
    /// Send a heartbeat once per minute so the server knows the user is active.
    /// Call this from App on each tick (filtered to once/min).
    /// </summary>
    public async Task SendHeartbeatAsync(TimeSpan sessionElapsed, bool onBreak)
    {
        if (!_s.TeamTelemetryEnabled || string.IsNullOrWhiteSpace(_s.TeamApiEndpoint))
            return;
        var payload = new HeartbeatPayload
        {
            UserId = _s.AnonymizeTelemetry ? AnonymousId() : Environment.UserName,
            DisplayName = _s.AnonymizeTelemetry ? "Anonymous" : _s.UserDisplayName,
            TeamName = _s.TeamName,
            SessionSeconds = (int)sessionElapsed.TotalSeconds,
            OnBreak = onBreak,
            Timestamp = DateTime.UtcNow,
        };
        try
        {
            await _http.PostAsJsonAsync($"{_s.TeamApiEndpoint}/heartbeat", payload, _json);
        }
        catch
        { /* Non-critical — swallow network errors silently */
        }
    }

    /// <summary>Fetch all team members' current status from the server.</summary>
    public async Task<List<TeamMember>> GetTeamStatusAsync()
    {
        if (!_s.TeamTelemetryEnabled || string.IsNullOrWhiteSpace(_s.TeamApiEndpoint))
            return SampleTeamData(); // Return demo data when not configured
        try
        {
            List<TeamMember>? members = await _http.GetFromJsonAsync<List<TeamMember>>(
                $"{_s.TeamApiEndpoint}/team", _json);
            return members ?? [];
        }
        catch
        {
            return SampleTeamData();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string AnonymousId()
    {
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(Environment.MachineName)
            )[..8]);
    }

    /// <summary>Demo data so the Team tab looks useful before API is wired up.</summary>
    private static List<TeamMember> SampleTeamData()
    {
        return
        [
            new TeamMember { DisplayName = "You", Initials = "JD", SessionTime = TimeSpan.FromMinutes(35), Status = MemberStatus.Active, BreaksTaken = 2 },
            new TeamMember { DisplayName = "Priya R.", Initials = "PR", SessionTime = TimeSpan.FromMinutes(74), Status = MemberStatus.Overdue, BreaksTaken = 1 },
            new TeamMember { DisplayName = "Aditya K.", Initials = "AK", SessionTime = TimeSpan.FromMinutes(48), Status = MemberStatus.DueSoon, BreaksTaken = 2 },
            new TeamMember { DisplayName = "Sneha M.", Initials = "SM", SessionTime = TimeSpan.FromMinutes(22), Status = MemberStatus.Active, BreaksTaken = 3 },
            new TeamMember { DisplayName = "Rahul T.", Initials = "RT", SessionTime = TimeSpan.Zero, Status = MemberStatus.OnBreak, BreaksTaken = 4 },
        ];
    }
}

public record HeartbeatPayload
{
    public string UserId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string TeamName { get; init; } = "";
    public int SessionSeconds { get; init; }
    public bool OnBreak { get; init; }
    public DateTime Timestamp { get; init; }
}
