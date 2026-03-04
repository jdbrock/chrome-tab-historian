namespace TabHistorian.Models;

public class Snapshot
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public List<ChromeWindow> Windows { get; set; } = [];
}
