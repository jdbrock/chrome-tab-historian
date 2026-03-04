using TabHistorian.Models;

namespace TabHistorian.Services;

/// <summary>
/// Orchestrates a full backup: discovers profiles, reads sessions, saves snapshot.
/// </summary>
public class SnapshotService
{
    private readonly ChromeProfileDiscovery _profileDiscovery;
    private readonly SessionFileReader _sessionReader;
    private readonly StorageService _storage;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(
        ChromeProfileDiscovery profileDiscovery,
        SessionFileReader sessionReader,
        StorageService storage,
        ILogger<SnapshotService> logger)
    {
        _profileDiscovery = profileDiscovery;
        _sessionReader = sessionReader;
        _storage = storage;
        _logger = logger;
    }

    public void TakeSnapshot()
    {
        var profiles = _profileDiscovery.DiscoverProfiles();
        if (profiles.Count == 0)
        {
            _logger.LogWarning("No Chrome profiles found, skipping snapshot");
            return;
        }

        _logger.LogInformation("Taking snapshot across {Count} profiles", profiles.Count);

        var allWindows = new List<ChromeWindow>();
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

        if (allWindows.Count == 0)
        {
            _logger.LogInformation("No open windows found (Chrome may not be running), skipping snapshot");
            return;
        }

        var snapshot = new Snapshot
        {
            Timestamp = DateTime.UtcNow,
            Windows = allWindows
        };

        int totalTabs = allWindows.Sum(w => w.Tabs.Count);
        _storage.SaveSnapshot(snapshot);
        _logger.LogInformation("Snapshot complete: {Windows} windows, {Tabs} tabs across {Profiles} profiles",
            allWindows.Count, totalTabs, profiles.Count);
    }
}
