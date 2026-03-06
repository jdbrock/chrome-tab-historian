using System.Text.Json;

namespace TabHistorian.Common;

public class TabHistorianSettings
{
    public string DatabasePath { get; set; } = "tabhistorian.db";
    public string TabMachineDatabasePath { get; set; } = "TabMachine.db";
    public string BackupDirectory { get; set; } = "backups";
    public List<string> IgnoredProfiles { get; set; } = [];
    public Dictionary<string, string> ProfileDisplayNames { get; set; } = [];

    public required string SettingsDirectory { get; init; }
    public required string ResolvedDatabasePath { get; init; }
    public required string ResolvedTabMachineDatabasePath { get; init; }
    public required string ResolvedBackupDirectory { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static TabHistorianSettings Load()
    {
        var settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TabHistorian");
        Directory.CreateDirectory(settingsDir);

        var settingsPath = Path.Combine(settingsDir, "settings.json");

        string databasePath = "tabhistorian.db";
        string tabMachineDatabasePath = "TabMachine.db";
        string backupDirectory = "backups";
        var ignoredProfiles = new List<string>();
        var profileDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (doc.TryGetProperty("databasePath", out var dbProp) && dbProp.ValueKind == JsonValueKind.String)
                databasePath = dbProp.GetString()!;
            if (doc.TryGetProperty("tabMachineDatabasePath", out var tmProp) && tmProp.ValueKind == JsonValueKind.String)
                tabMachineDatabasePath = tmProp.GetString()!;
            if (doc.TryGetProperty("backupDirectory", out var backupProp) && backupProp.ValueKind == JsonValueKind.String)
                backupDirectory = backupProp.GetString()!;
            if (doc.TryGetProperty("ignoredProfiles", out var ipProp) && ipProp.ValueKind == JsonValueKind.Array)
                ignoredProfiles = ipProp.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
            if (doc.TryGetProperty("profileDisplayNames", out var pdnProp) && pdnProp.ValueKind == JsonValueKind.Object)
                foreach (var prop in pdnProp.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        profileDisplayNames[prop.Name] = prop.Value.GetString()!;
        }
        else
        {
            var defaults = new { databasePath, tabMachineDatabasePath, backupDirectory, ignoredProfiles, profileDisplayNames };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(defaults, JsonOptions));
        }

        return new TabHistorianSettings
        {
            DatabasePath = databasePath,
            TabMachineDatabasePath = tabMachineDatabasePath,
            BackupDirectory = backupDirectory,
            IgnoredProfiles = ignoredProfiles,
            ProfileDisplayNames = profileDisplayNames,
            SettingsDirectory = settingsDir,
            ResolvedDatabasePath = ResolvePath(settingsDir, databasePath),
            ResolvedTabMachineDatabasePath = ResolvePath(settingsDir, tabMachineDatabasePath),
            ResolvedBackupDirectory = ResolvePath(settingsDir, backupDirectory),
        };
    }

    private static string ResolvePath(string baseDir, string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
}
