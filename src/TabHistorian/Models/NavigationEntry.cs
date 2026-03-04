namespace TabHistorian.Models;

public class NavigationEntry
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int TransitionType { get; set; }
}
