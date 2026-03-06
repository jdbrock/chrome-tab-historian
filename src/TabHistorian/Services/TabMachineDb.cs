using Microsoft.Data.Sqlite;
using TabHistorian.Common;

namespace TabHistorian.Services;

public class TabMachineDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TabMachineDb(TabHistorianSettings settings, ILogger<TabMachineDb> logger)
    {
        var dbPath = settings.ResolvedTabMachineDatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        _connection = new SqliteConnection($"Data Source={dbPath};Default Timeout=30");
        _connection.Open();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL";
            cmd.ExecuteNonQuery();
        }
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA busy_timeout=30000";
            cmd.ExecuteNonQuery();
        }

        InitializeSchema();
        logger.LogInformation("TabMachine database ready at {Path}", dbPath);
    }

    internal SqliteConnection Connection => _connection;

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tab_identities (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_name TEXT NOT NULL,
                first_url TEXT NOT NULL,
                first_title TEXT NOT NULL DEFAULT '',
                first_seen TEXT NOT NULL,
                last_url TEXT NOT NULL,
                last_title TEXT NOT NULL DEFAULT '',
                last_seen TEXT NOT NULL,
                last_active_time TEXT
            );

            CREATE TABLE IF NOT EXISTS tab_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tab_identity_id INTEGER NOT NULL REFERENCES tab_identities(id),
                event_type TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                state_delta TEXT,
                url TEXT,
                title TEXT,
                profile_name TEXT
            );

            CREATE TABLE IF NOT EXISTS tab_current_state (
                tab_identity_id INTEGER PRIMARY KEY REFERENCES tab_identities(id),
                current_url TEXT NOT NULL,
                title TEXT NOT NULL DEFAULT '',
                pinned INTEGER DEFAULT 0,
                last_active_time TEXT,
                tab_index INTEGER NOT NULL DEFAULT 0,
                window_index INTEGER NOT NULL DEFAULT 0,
                window_type INTEGER DEFAULT 0,
                profile_name TEXT NOT NULL,
                profile_display_name TEXT,
                sync_tab_node_id TEXT,
                tab_group_token TEXT,
                extension_app_id TEXT,
                navigation_history TEXT,
                show_state INTEGER DEFAULT 0,
                is_active INTEGER DEFAULT 0,
                is_open INTEGER DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS idx_tm_identities_profile ON tab_identities(profile_name);
            CREATE INDEX IF NOT EXISTS idx_tm_events_identity ON tab_events(tab_identity_id);
            CREATE INDEX IF NOT EXISTS idx_tm_events_type ON tab_events(event_type);
            CREATE INDEX IF NOT EXISTS idx_tm_events_timestamp ON tab_events(timestamp);
            CREATE INDEX IF NOT EXISTS idx_tm_events_url ON tab_events(url);
            CREATE INDEX IF NOT EXISTS idx_tm_current_state_open ON tab_current_state(is_open);
            CREATE INDEX IF NOT EXISTS idx_tm_current_state_sync ON tab_current_state(sync_tab_node_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
