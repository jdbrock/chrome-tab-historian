using System.Text.Json;

namespace TabHistorian.Services;

public record ChromeProfile(string DirectoryName, string DisplayName, string FullPath);

public class ChromeProfileDiscovery
{
    private static readonly string ChromeUserDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google", "Chrome", "User Data");

    private readonly ILogger<ChromeProfileDiscovery> _logger;

    public ChromeProfileDiscovery(ILogger<ChromeProfileDiscovery> logger)
    {
        _logger = logger;
    }

    public List<ChromeProfile> DiscoverProfiles()
    {
        var profiles = new List<ChromeProfile>();
        var localStatePath = Path.Combine(ChromeUserDataPath, "Local State");

        if (!File.Exists(localStatePath))
        {
            _logger.LogWarning("Chrome Local State file not found at {Path}", localStatePath);
            return profiles;
        }

        try
        {
            // Read-only access to Chrome's Local State file
            using var stream = new FileStream(localStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("profile", out var profileElement) ||
                !profileElement.TryGetProperty("info_cache", out var infoCache))
            {
                _logger.LogWarning("Could not find profile.info_cache in Local State");
                return profiles;
            }

            foreach (var entry in infoCache.EnumerateObject())
            {
                string dirName = entry.Name;
                string displayName = entry.Value.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? dirName
                    : dirName;

                string fullPath = Path.Combine(ChromeUserDataPath, dirName);
                if (Directory.Exists(fullPath))
                {
                    profiles.Add(new ChromeProfile(dirName, displayName, fullPath));
                    _logger.LogDebug("Found profile: {Dir} ({Name})", dirName, displayName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Chrome Local State");
        }

        return profiles;
    }
}
