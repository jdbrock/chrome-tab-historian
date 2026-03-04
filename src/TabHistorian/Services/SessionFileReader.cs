using TabHistorian.Models;
using TabHistorian.Parsing;

namespace TabHistorian.Services;

/// <summary>
/// Reads and reconstructs Chrome session state from SNSS files.
/// Copies files to temp before reading (Chrome holds locks on originals).
/// ALL access to Chrome files is strictly READ-ONLY.
/// </summary>
public class SessionFileReader
{
    // Session file command IDs (from Chromium source: session_service_commands.cc)
    private const byte CmdSetTabWindow = 0;
    private const byte CmdSetTabIndexInWindow = 2;
    private const byte CmdUpdateTabNavigation = 6;
    private const byte CmdSetSelectedNavigationIndex = 7;
    private const byte CmdSetSelectedTabInIndex = 8;
    private const byte CmdSetPinnedState = 12;
    private const byte CmdTabClosed = 16;
    private const byte CmdWindowClosed = 17;

    private readonly ILogger<SessionFileReader> _logger;
    private readonly SnssParser _parser = new();

    public SessionFileReader(ILogger<SessionFileReader> logger)
    {
        _logger = logger;
    }

    public List<ChromeWindow> ReadProfile(ChromeProfile profile)
    {
        var sessionsDir = Path.Combine(profile.FullPath, "Sessions");
        if (!Directory.Exists(sessionsDir))
        {
            _logger.LogDebug("No Sessions directory for profile {Profile}", profile.DisplayName);
            return [];
        }

        // Get session files ordered newest first. When Chrome closes cleanly,
        // the newest file may be empty — fall back to the second newest.
        var sessionFiles = Directory.GetFiles(sessionsDir, "Session_*")
            .OrderByDescending(f => f)
            .ToList();

        if (sessionFiles.Count == 0)
        {
            _logger.LogDebug("No session files found for profile {Profile}", profile.DisplayName);
            return [];
        }

        foreach (var sessionFile in sessionFiles)
        {
            _logger.LogDebug("Trying session file: {File} ({Size} bytes)",
                sessionFile, new FileInfo(sessionFile).Length);

            string tempFile = Path.Combine(Path.GetTempPath(), $"tabhistorian_{Guid.NewGuid()}.snss");
            try
            {
                // Read-only copy: open source with read-only access, allow Chrome to keep writing
                using (var source = new FileStream(sessionFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dest = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    source.CopyTo(dest);
                }

                var windows = ParseSessionFile(tempFile, profile);
                if (windows.Count > 0)
                    return windows;

                _logger.LogDebug("No windows found in {File}, trying next file", sessionFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading session file {File}", sessionFile);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
            }
        }

        _logger.LogDebug("No usable session files for profile {Profile}", profile.DisplayName);
        return [];
    }

    private List<ChromeWindow> ParseSessionFile(string filePath, ChromeProfile profile)
    {
        List<SnssCommand> commands;
        using (var stream = File.OpenRead(filePath))
        {
            commands = _parser.Parse(stream);
        }

        _logger.LogDebug("Parsed {Count} commands from session file", commands.Count);

        // State dictionaries keyed by session IDs
        var windows = new Dictionary<int, WindowState>();
        var tabs = new Dictionary<int, TabState>();

        foreach (var cmd in commands)
        {
            try
            {
                ProcessCommand(cmd, windows, tabs);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Skipping command {Id}: {Error}", cmd.Id, ex.Message);
            }
        }

        _logger.LogDebug("Replayed into {Windows} windows, {Tabs} tabs",
            windows.Count(w => !w.Value.Closed), tabs.Count(t => !t.Value.Closed));

        // Build result: group tabs by window, filter out closed entities
        return AssembleWindows(windows, tabs, profile);
    }

    private void ProcessCommand(SnssCommand cmd, Dictionary<int, WindowState> windows, Dictionary<int, TabState> tabs)
    {
        switch (cmd.Id)
        {
            case CmdSetTabWindow:
            {
                // Payload is (windowId, tabId) — window ID comes first
                var (windowId, tabId) = ReadIdAndIndex(cmd.Payload);
                GetOrCreateTab(tabs, tabId).WindowId = windowId;
                GetOrCreateWindow(windows, windowId);
                break;
            }
            case CmdSetTabIndexInWindow:
            {
                var (tabId, index) = ReadIdAndIndex(cmd.Payload);
                GetOrCreateTab(tabs, tabId).TabIndex = index;
                break;
            }
            case CmdUpdateTabNavigation:
            {
                ParseNavigationEntry(cmd.Payload, tabs);
                break;
            }
            case CmdSetSelectedNavigationIndex:
            {
                var (tabId, index) = ReadIdAndIndex(cmd.Payload);
                GetOrCreateTab(tabs, tabId).SelectedNavIndex = index;
                break;
            }
            case CmdSetSelectedTabInIndex:
            {
                var (windowId, index) = ReadIdAndIndex(cmd.Payload);
                GetOrCreateWindow(windows, windowId).SelectedTabIndex = index;
                break;
            }
            case CmdSetPinnedState:
            {
                if (cmd.Payload.Length >= 5)
                {
                    int tabId = BitConverter.ToInt32(cmd.Payload, 0);
                    bool pinned = cmd.Payload[4] != 0;
                    GetOrCreateTab(tabs, tabId).Pinned = pinned;
                }
                break;
            }
            case CmdTabClosed:
            {
                if (cmd.Payload.Length >= 4)
                {
                    int tabId = BitConverter.ToInt32(cmd.Payload, 0);
                    if (tabs.ContainsKey(tabId))
                        tabs[tabId].Closed = true;
                }
                break;
            }
            case CmdWindowClosed:
            {
                if (cmd.Payload.Length >= 4)
                {
                    int windowId = BitConverter.ToInt32(cmd.Payload, 0);
                    if (windows.ContainsKey(windowId))
                        windows[windowId].Closed = true;
                }
                break;
            }
        }
    }

    private void ParseNavigationEntry(byte[] payload, Dictionary<int, TabState> tabs)
    {
        var pickle = new PickleReader(payload);

        int tabId = pickle.ReadInt32();
        int navIndex = pickle.ReadInt32();
        string url = pickle.ReadString();
        string title = pickle.ReadString16();

        // Skip encoded_page_state (can be large)
        pickle.ReadString();

        int transitionType = pickle.ReadInt32();

        var tab = GetOrCreateTab(tabs, tabId);

        // Ensure nav list is big enough
        while (tab.NavigationEntries.Count <= navIndex)
            tab.NavigationEntries.Add(new NavigationEntry());

        tab.NavigationEntries[navIndex] = new NavigationEntry
        {
            Url = url,
            Title = title,
            TransitionType = transitionType & 0xFF // lower 8 bits = core type
        };
    }

    private static (int id, int index) ReadIdAndIndex(byte[] payload)
    {
        if (payload.Length < 8)
            throw new InvalidDataException("IDAndIndex payload too short");

        return (BitConverter.ToInt32(payload, 0), BitConverter.ToInt32(payload, 4));
    }

    private List<ChromeWindow> AssembleWindows(
        Dictionary<int, WindowState> windows,
        Dictionary<int, TabState> tabs,
        ChromeProfile profile)
    {
        var result = new List<ChromeWindow>();
        int windowIndex = 0;

        foreach (var (windowId, windowState) in windows.Where(w => !w.Value.Closed))
        {
            var windowTabs = tabs
                .Where(t => !t.Value.Closed && t.Value.WindowId == windowId)
                .OrderBy(t => t.Value.TabIndex)
                .Select(t =>
                {
                    var tabState = t.Value;
                    int selectedIdx = tabState.SelectedNavIndex;
                    var navEntries = tabState.NavigationEntries
                        .Where(n => !string.IsNullOrEmpty(n.Url))
                        .ToList();

                    string currentUrl = "";
                    string currentTitle = "";
                    if (selectedIdx >= 0 && selectedIdx < tabState.NavigationEntries.Count)
                    {
                        currentUrl = tabState.NavigationEntries[selectedIdx].Url;
                        currentTitle = tabState.NavigationEntries[selectedIdx].Title;
                    }
                    else if (navEntries.Count > 0)
                    {
                        currentUrl = navEntries[^1].Url;
                        currentTitle = navEntries[^1].Title;
                    }

                    return new ChromeTab
                    {
                        TabIndex = tabState.TabIndex,
                        CurrentUrl = currentUrl,
                        Title = currentTitle,
                        Pinned = tabState.Pinned,
                        NavigationHistory = navEntries
                    };
                })
                .Where(t => !string.IsNullOrEmpty(t.CurrentUrl))
                .ToList();

            if (windowTabs.Count > 0)
            {
                result.Add(new ChromeWindow
                {
                    ProfileName = profile.DirectoryName,
                    ProfileDisplayName = profile.DisplayName,
                    WindowIndex = windowIndex++,
                    Tabs = windowTabs
                });
            }
        }

        return result;
    }

    private static WindowState GetOrCreateWindow(Dictionary<int, WindowState> windows, int id)
    {
        if (!windows.TryGetValue(id, out var state))
        {
            state = new WindowState();
            windows[id] = state;
        }
        return state;
    }

    private static TabState GetOrCreateTab(Dictionary<int, TabState> tabs, int id)
    {
        if (!tabs.TryGetValue(id, out var state))
        {
            state = new TabState();
            tabs[id] = state;
        }
        return state;
    }

    private class WindowState
    {
        public int SelectedTabIndex { get; set; }
        public bool Closed { get; set; }
    }

    private class TabState
    {
        public int WindowId { get; set; }
        public int TabIndex { get; set; }
        public int SelectedNavIndex { get; set; } = -1;
        public bool Pinned { get; set; }
        public bool Closed { get; set; }
        public List<NavigationEntry> NavigationEntries { get; set; } = [];
    }
}
