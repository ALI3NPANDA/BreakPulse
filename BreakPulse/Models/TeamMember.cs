namespace BreakPulse.Models;

public class TeamMember
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Initials { get; set; } = "";
    public TimeSpan SessionTime { get; set; }
    public MemberStatus Status { get; set; }
    public DateTime LastSeen { get; set; }
    public int BreaksTaken { get; set; }
}

public enum MemberStatus
{
    Active,
    DueSoon,
    Overdue,
    OnBreak,
    Offline,
}
