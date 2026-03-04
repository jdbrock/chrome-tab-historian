using System.Collections.ObjectModel;

namespace TabHistorian.Viewer.ViewModels;

public class SnapshotNode : ViewModelBase
{
    private bool _isExpanded;

    public string Timestamp { get; init; } = "";
    public int WindowCount { get; init; }
    public int TabCount { get; init; }
    public ObservableCollection<WindowNode> Windows { get; } = [];

    public string Display => $"\U0001F4F8 {Timestamp}  ({WindowCount} windows, {TabCount} tabs)";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
}

public class WindowNode : ViewModelBase
{
    private bool _isExpanded;

    public string ProfileDisplayName { get; init; } = "";
    public int WindowIndex { get; init; }
    public int TabCount { get; init; }
    public ObservableCollection<TabNode> Tabs { get; } = [];

    public string Display => $"{ProfileDisplayName} \u2014 Window {WindowIndex + 1}  ({TabCount} tabs)";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
}

public class TabNode : ViewModelBase
{
    private bool _isExpanded;

    public string Title { get; init; } = "";
    public string CurrentUrl { get; init; } = "";
    public bool Pinned { get; init; }
    public ObservableCollection<NavEntryNode> NavEntries { get; } = [];

    public string Display => (Pinned ? "\U0001F4CC " : "") +
        (string.IsNullOrEmpty(Title) ? CurrentUrl : $"{Title} \u2014 {CurrentUrl}");

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
}

public class NavEntryNode
{
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";

    public string Display => string.IsNullOrEmpty(Title)
        ? $"\u2192 {Url}"
        : $"\u2192 {Url} \u2014 \"{Title}\"";
}
