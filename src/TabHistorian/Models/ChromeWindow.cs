namespace TabHistorian.Models;

public class ChromeWindow
{
    public string ProfileName { get; set; } = string.Empty;
    public string ProfileDisplayName { get; set; } = string.Empty;
    public int WindowIndex { get; set; }
    public List<ChromeTab> Tabs { get; set; } = [];
}
