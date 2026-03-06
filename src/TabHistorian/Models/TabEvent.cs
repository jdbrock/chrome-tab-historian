namespace TabHistorian.Models;

public enum TabEventType
{
    Opened,
    Closed,
    Navigated,
    TitleChanged,
    Pinned,
    Unpinned
}

public class TabEvent
{
    public long Id { get; set; }
    public long TabIdentityId { get; set; }
    public TabEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public long? SnapshotId { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? OldUrl { get; set; }
    public string? OldTitle { get; set; }
    public string? ProfileName { get; set; }
    public int? WindowIndex { get; set; }
    public int? TabIndex { get; set; }
}
