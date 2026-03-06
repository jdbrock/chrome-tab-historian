namespace TabHistorian.Models;

public class TabIdentity
{
    public long Id { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public string FirstUrl { get; set; } = string.Empty;
    public string FirstTitle { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public string LastUrl { get; set; } = string.Empty;
    public string LastTitle { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public string? LastActiveTime { get; set; }
}
