using System.Text.Json;
using Microsoft.Data.Sqlite;
using TabHistorian.Models;
using TabHistorian.Common;

namespace TabHistorian.Services;

public class StorageService : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<StorageService> _logger;
    private readonly TabHistorianSettings _settings;

    public StorageService(ILogger<StorageService> logger, TabHistorianSettings settings)
    {
        _logger = logger;
        _settings = settings;

        Directory.CreateDirectory(Path.GetDirectoryName(settings.ResolvedDatabasePath)!);

        _connection = new SqliteConnection($"Data Source={settings.ResolvedDatabasePath};Default Timeout=30");
        _connection.Open();

        // WAL mode allows concurrent reads (Web/Viewer) without blocking writes
        using (var walCmd = _connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL";
            walCmd.ExecuteNonQuery();
        }

        // Explicit busy timeout — Default Timeout in connection string may not map to SQLite's busy_timeout
        using (var busyCmd = _connection.CreateCommand())
        {
            busyCmd.CommandText = "PRAGMA busy_timeout=30000";
            busyCmd.ExecuteNonQuery();
        }

        InitializeDatabase();
        RunMigrations();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS windows (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                snapshot_id INTEGER NOT NULL REFERENCES snapshots(id),
                profile_name TEXT NOT NULL,
                profile_display_name TEXT,
                window_index INTEGER NOT NULL,
                window_type INTEGER DEFAULT 0,
                x INTEGER, y INTEGER, width INTEGER, height INTEGER,
                show_state INTEGER DEFAULT 0,
                is_active INTEGER DEFAULT 0,
                selected_tab_index INTEGER DEFAULT 0,
                workspace TEXT,
                app_name TEXT,
                user_title TEXT
            );

            CREATE TABLE IF NOT EXISTS tabs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                window_id INTEGER NOT NULL REFERENCES windows(id),
                tab_index INTEGER NOT NULL,
                current_url TEXT NOT NULL,
                title TEXT,
                pinned INTEGER DEFAULT 0,
                last_active_time TEXT,
                tab_group_token TEXT,
                extension_app_id TEXT,
                navigation_history TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_snapshots_timestamp ON snapshots(timestamp);
            CREATE INDEX IF NOT EXISTS idx_windows_snapshot ON windows(snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_tabs_window ON tabs(window_id);
            CREATE INDEX IF NOT EXISTS idx_tabs_current_url ON tabs(current_url);
            CREATE INDEX IF NOT EXISTS idx_windows_profile ON windows(profile_name);
            CREATE INDEX IF NOT EXISTS idx_tabs_last_active ON tabs(last_active_time);
            """;
        cmd.ExecuteNonQuery();
    }

    private void RunMigrations()
    {
        // Create schema_version table if it doesn't exist
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL)";
            cmd.ExecuteNonQuery();
        }

        int currentVersion;
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version";
            currentVersion = Convert.ToInt32(cmd.ExecuteScalar());
        }

        if (currentVersion < 1)
        {
            _logger.LogInformation("Running migration v1: tab_identities and tab_events tables");
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
                    snapshot_id INTEGER,
                    url TEXT,
                    title TEXT,
                    old_url TEXT,
                    old_title TEXT,
                    profile_name TEXT,
                    window_index INTEGER,
                    tab_index INTEGER
                );

                CREATE TABLE IF NOT EXISTS tab_identity_map (
                    tab_id INTEGER PRIMARY KEY REFERENCES tabs(id),
                    tab_identity_id INTEGER NOT NULL REFERENCES tab_identities(id)
                );

                CREATE INDEX IF NOT EXISTS idx_tab_identities_profile ON tab_identities(profile_name);
                CREATE INDEX IF NOT EXISTS idx_tab_events_identity ON tab_events(tab_identity_id);
                CREATE INDEX IF NOT EXISTS idx_tab_events_type ON tab_events(event_type);
                CREATE INDEX IF NOT EXISTS idx_tab_events_timestamp ON tab_events(timestamp);
                CREATE INDEX IF NOT EXISTS idx_tab_events_snapshot ON tab_events(snapshot_id);
                CREATE INDEX IF NOT EXISTS idx_tab_identity_map_identity ON tab_identity_map(tab_identity_id);

                INSERT INTO schema_version (version) VALUES (1);
                """;
            cmd.ExecuteNonQuery();
        }

        if (currentVersion < 2)
        {
            _logger.LogInformation("Running migration v2: sync_tab_node_id column");
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                ALTER TABLE tabs ADD COLUMN sync_tab_node_id TEXT;
                CREATE INDEX IF NOT EXISTS idx_tabs_sync_node ON tabs(sync_tab_node_id);
                INSERT INTO schema_version (version) VALUES (2);
                """;
            cmd.ExecuteNonQuery();
        }

        if (currentVersion < 3)
        {
            _logger.LogInformation("Running migration v3: remove tab tracking tables (moved to TabMachine.db)");
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                DROP TABLE IF EXISTS tab_identity_map;
                DROP TABLE IF EXISTS tab_events;
                DROP TABLE IF EXISTS tab_identities;
                INSERT INTO schema_version (version) VALUES (3);
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public long SaveSnapshot(Snapshot snapshot)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            using var snapshotCmd = _connection.CreateCommand();
            snapshotCmd.CommandText = "INSERT INTO snapshots (timestamp) VALUES (@ts) RETURNING id";
            snapshotCmd.Parameters.AddWithValue("@ts", snapshot.Timestamp.ToString("O"));
            long snapshotId = (long)snapshotCmd.ExecuteScalar()!;

            foreach (var window in snapshot.Windows)
            {
                using var windowCmd = _connection.CreateCommand();
                windowCmd.CommandText = """
                    INSERT INTO windows (snapshot_id, profile_name, profile_display_name, window_index,
                        window_type, x, y, width, height, show_state, is_active, selected_tab_index,
                        workspace, app_name, user_title)
                    VALUES (@sid, @pn, @pdn, @wi, @wt, @x, @y, @w, @h, @ss, @ia, @sti,
                        @ws, @an, @ut) RETURNING id
                    """;
                windowCmd.Parameters.AddWithValue("@sid", snapshotId);
                windowCmd.Parameters.AddWithValue("@pn", window.ProfileName);
                windowCmd.Parameters.AddWithValue("@pdn", window.ProfileDisplayName);
                windowCmd.Parameters.AddWithValue("@wi", window.WindowIndex);
                windowCmd.Parameters.AddWithValue("@wt", window.WindowType);
                windowCmd.Parameters.AddWithValue("@x", window.X);
                windowCmd.Parameters.AddWithValue("@y", window.Y);
                windowCmd.Parameters.AddWithValue("@w", window.Width);
                windowCmd.Parameters.AddWithValue("@h", window.Height);
                windowCmd.Parameters.AddWithValue("@ss", window.ShowState);
                windowCmd.Parameters.AddWithValue("@ia", window.IsActive ? 1 : 0);
                windowCmd.Parameters.AddWithValue("@sti", window.SelectedTabIndex);
                windowCmd.Parameters.AddWithValue("@ws", (object?)window.Workspace ?? DBNull.Value);
                windowCmd.Parameters.AddWithValue("@an", (object?)window.AppName ?? DBNull.Value);
                windowCmd.Parameters.AddWithValue("@ut", (object?)window.UserTitle ?? DBNull.Value);
                long windowId = (long)windowCmd.ExecuteScalar()!;

                foreach (var tab in window.Tabs)
                {
                    using var tabCmd = _connection.CreateCommand();
                    tabCmd.CommandText = """
                        INSERT INTO tabs (window_id, tab_index, current_url, title, pinned,
                            last_active_time, tab_group_token, extension_app_id, sync_tab_node_id, navigation_history)
                        VALUES (@wid, @ti, @url, @title, @pinned, @lat, @tgt, @eai, @stn, @nav)
                        """;
                    tabCmd.Parameters.AddWithValue("@wid", windowId);
                    tabCmd.Parameters.AddWithValue("@ti", tab.TabIndex);
                    tabCmd.Parameters.AddWithValue("@url", tab.CurrentUrl);
                    tabCmd.Parameters.AddWithValue("@title", (object?)tab.Title ?? DBNull.Value);
                    tabCmd.Parameters.AddWithValue("@pinned", tab.Pinned ? 1 : 0);
                    tabCmd.Parameters.AddWithValue("@lat",
                        tab.LastActiveTime.HasValue ? (object)tab.LastActiveTime.Value.ToString("O") : DBNull.Value);
                    tabCmd.Parameters.AddWithValue("@tgt", (object?)tab.TabGroupToken ?? DBNull.Value);
                    tabCmd.Parameters.AddWithValue("@eai", (object?)tab.ExtensionAppId ?? DBNull.Value);
                    tabCmd.Parameters.AddWithValue("@stn", (object?)tab.SyncTabNodeId ?? DBNull.Value);
                    tabCmd.Parameters.AddWithValue("@nav", JsonSerializer.Serialize(
                        tab.NavigationHistory.Select(n => new
                        {
                            url = n.Url,
                            title = n.Title,
                            timestamp = n.Timestamp?.ToString("O"),
                            referrer = string.IsNullOrEmpty(n.ReferrerUrl) ? null : n.ReferrerUrl,
                            originalUrl = string.IsNullOrEmpty(n.OriginalRequestUrl) ? null : n.OriginalRequestUrl,
                            httpStatus = n.HttpStatusCode > 0 ? n.HttpStatusCode : (int?)null,
                            transition = n.TransitionType,
                            hasPostData = n.HasPostData ? true : (bool?)null
                        }),
                        JsonSerializerOptions.Web));
                    tabCmd.ExecuteNonQuery();
                }
            }

            transaction.Commit();
            _logger.LogInformation("Saved snapshot {Id} with {WindowCount} windows", snapshotId, snapshot.Windows.Count);
            return snapshotId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Prunes old snapshots according to the retention policy:
    /// - Today: keep all
    /// - Yesterday: keep oldest
    /// - Previous week (2–7 days ago): keep oldest
    /// - Previous month (8–30 days ago): keep oldest
    /// - Older: keep oldest per calendar month
    /// </summary>
    public void PruneSnapshots()
    {
        _logger.LogInformation("Starting snapshot pruning");
        var now = DateTime.UtcNow;
        var today = now.Date;
        var yesterday = today.AddDays(-1);
        var weekAgo = today.AddDays(-7);
        var monthAgo = today.AddDays(-30);

        // Get all snapshots ordered by timestamp
        var snapshots = new List<(long Id, DateTime Timestamp)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, timestamp FROM snapshots ORDER BY timestamp";
            _logger.LogInformation("Querying snapshots for pruning");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var ts = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                snapshots.Add((id, ts));
            }
        }

        _logger.LogInformation("Found {Count} snapshots to evaluate for pruning", snapshots.Count);
        if (snapshots.Count == 0) return;

        var toKeep = new HashSet<long>();

        foreach (var (id, ts) in snapshots)
        {
            var date = ts.Date;

            if (date >= today)
            {
                // Today: keep all
                toKeep.Add(id);
            }
        }

        // Yesterday: keep oldest
        KeepOldest(snapshots.Where(s => s.Timestamp.Date >= yesterday && s.Timestamp.Date < today), toKeep);

        // Previous week (2–7 days ago): keep oldest
        KeepOldest(snapshots.Where(s => s.Timestamp.Date >= weekAgo && s.Timestamp.Date < yesterday), toKeep);

        // Previous month (8–30 days ago): keep oldest
        KeepOldest(snapshots.Where(s => s.Timestamp.Date >= monthAgo && s.Timestamp.Date < weekAgo), toKeep);

        // Older: keep oldest per calendar month
        var olderSnapshots = snapshots.Where(s => s.Timestamp.Date < monthAgo);
        foreach (var monthGroup in olderSnapshots.GroupBy(s => new { s.Timestamp.Year, s.Timestamp.Month }))
        {
            KeepOldest(monthGroup, toKeep);
        }

        // Delete snapshots not in the keep set
        var toDelete = snapshots.Where(s => !toKeep.Contains(s.Id)).Select(s => s.Id).ToList();

        if (toDelete.Count == 0)
        {
            _logger.LogInformation("No snapshots to prune (keeping all {Count})", toKeep.Count);
            return;
        }

        _logger.LogInformation("Pruning {DeleteCount} snapshots, keeping {KeepCount}", toDelete.Count, toKeep.Count);

        for (int i = 0; i < toDelete.Count; i++)
        {
            var id = toDelete[i];
            using var deleteCmd = _connection.CreateCommand();
            deleteCmd.CommandText = $"""
                DELETE FROM tabs WHERE window_id IN (SELECT id FROM windows WHERE snapshot_id = {id});
                DELETE FROM windows WHERE snapshot_id = {id};
                DELETE FROM snapshots WHERE id = {id};
                """;
            deleteCmd.ExecuteNonQuery();
            _logger.LogInformation("Pruned snapshot {Current}/{Total} (id {Id})", i + 1, toDelete.Count, id);
        }

        _logger.LogInformation("Pruning complete, kept {Kept} snapshots", toKeep.Count);
    }

    private static void KeepOldest(IEnumerable<(long Id, DateTime Timestamp)> snapshots, HashSet<long> toKeep)
    {
        var oldest = snapshots.OrderBy(s => s.Timestamp).FirstOrDefault();
        if (oldest.Id != 0)
            toKeep.Add(oldest.Id);
    }

public void BackupDatabase()
    {
        var backupDir = _settings.ResolvedBackupDirectory;
        var backupName = $"tabhistorian-{DateTime.UtcNow:yyyy-MM-dd}.db";
        var backupPath = Path.Combine(backupDir, backupName);

        if (File.Exists(backupPath))
        {
            _logger.LogDebug("Backup already exists for today: {Path}", backupPath);
            return;
        }

        var dbSize = new FileInfo(_settings.ResolvedDatabasePath).Length;
        _logger.LogInformation("Starting database backup ({DbSize:F1} MB) to {Path}",
            dbSize / (1024.0 * 1024.0), backupPath);

        Directory.CreateDirectory(backupDir);

        try
        {
            // Use SQLite online backup API — works cooperatively with WAL mode
            // and doesn't require an exclusive lock on the source database
            using var destConn = new SqliteConnection($"Data Source={backupPath}");
            destConn.Open();
            _connection.BackupDatabase(destConn, "main", "main");
            destConn.Close();

            var backupSize = new FileInfo(backupPath).Length;
            _logger.LogInformation("Database backup complete ({BackupSize:F1} MB): {Path}",
                backupSize / (1024.0 * 1024.0), backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database backup failed, cleaning up partial file: {Path}", backupPath);
            try { File.Delete(backupPath); }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up partial backup file: {Path}", backupPath);
            }
            throw;
        }
    }

    public DateTime? GetLatestSnapshotTimestamp()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT timestamp FROM snapshots ORDER BY timestamp DESC LIMIT 1";
        var result = cmd.ExecuteScalar();
        if (result is string ts)
            return DateTime.Parse(ts, null, System.Globalization.DateTimeStyles.RoundtripKind);
        return null;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
