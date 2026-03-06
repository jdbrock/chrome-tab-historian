using TabHistorian.Models;

namespace TabHistorian.Services;

/// <summary>
/// Orchestrates Chrome tab reading and snapshot saving as separate operations.
/// </summary>
public class SnapshotService
{
    private readonly ChromeProfileDiscovery _profileDiscovery;
    private readonly SessionFileReader _sessionReader;
    private readonly SyncedSessionReader _syncedSessionReader;
    private readonly StorageService _storage;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(
        ChromeProfileDiscovery profileDiscovery,
        SessionFileReader sessionReader,
        SyncedSessionReader syncedSessionReader,
        StorageService storage,
        ILogger<SnapshotService> logger)
    {
        _profileDiscovery = profileDiscovery;
        _sessionReader = sessionReader;
        _syncedSessionReader = syncedSessionReader;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Reads current Chrome state into memory without writing to the database.
    /// </summary>
    public (List<ChromeWindow> Windows, DateTime Timestamp)? ReadCurrentState()
    {
        var profiles = _profileDiscovery.DiscoverProfiles();
        if (profiles.Count == 0)
        {
            _logger.LogWarning("No Chrome profiles found");
            return null;
        }

        _logger.LogInformation("Reading Chrome state across {Count} profiles", profiles.Count);

        _sessionReader.EnsureVssSnapshot(profiles[0].FullPath);

        var allWindows = new List<ChromeWindow>();
        try
        {
            foreach (var profile in profiles)
            {
                try
                {
                    var windows = _sessionReader.ReadProfile(profile);
                    allWindows.AddRange(windows);
                    _logger.LogDebug("Profile {Name}: {Count} windows", profile.DisplayName, windows.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read profile {Name}, continuing with others", profile.DisplayName);
                }
            }
        }
        finally
        {
            _sessionReader.ReleaseVssSnapshot();
        }

        try
        {
            var syncedWindows = _syncedSessionReader.ReadSyncedSessions();
            if (syncedWindows.Count > 0)
            {
                allWindows.AddRange(syncedWindows);
                int syncedTabs = syncedWindows.Sum(w => w.Tabs.Count);
                _logger.LogInformation("Added {Windows} synced windows with {Tabs} tabs from other devices",
                    syncedWindows.Count, syncedTabs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read synced sessions, continuing without them");
        }

        if (allWindows.Count == 0)
        {
            _logger.LogInformation("No open windows found (Chrome may not be running)");
            return null;
        }

        return (allWindows, DateTime.UtcNow);
    }

    /// <summary>
    /// Saves a full snapshot to FullSnapshots.db.
    /// </summary>
    public long SaveSnapshot(List<ChromeWindow> windows, DateTime timestamp)
    {
        var snapshot = new Snapshot
        {
            Timestamp = timestamp,
            Windows = windows
        };

        int totalTabs = windows.Sum(w => w.Tabs.Count);
        var snapshotId = _storage.SaveSnapshot(snapshot);

        _logger.LogInformation("Snapshot saved: {Windows} windows, {Tabs} tabs", windows.Count, totalTabs);
        return snapshotId;
    }

    /// <summary>
    /// Returns the timestamp of the latest snapshot, or null if none exist.
    /// </summary>
    public DateTime? GetLatestSnapshotTimestamp()
    {
        return _storage.GetLatestSnapshotTimestamp();
    }
}
