using System.Text.Json;
using Microsoft.Data.Sqlite;
using TabHistorian.Models;

namespace TabHistorian.Services;

public class StorageService : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<StorageService> _logger;

    public StorageService(ILogger<StorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TabHistorian");
        var dbPath = configuration.GetValue<string>("DatabasePath")
            ?? Path.Combine(defaultDir, "tabhistorian.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
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
                window_index INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tabs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                window_id INTEGER NOT NULL REFERENCES windows(id),
                tab_index INTEGER NOT NULL,
                current_url TEXT NOT NULL,
                title TEXT,
                pinned INTEGER DEFAULT 0,
                navigation_history TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_snapshots_timestamp ON snapshots(timestamp);
            CREATE INDEX IF NOT EXISTS idx_tabs_current_url ON tabs(current_url);
            CREATE INDEX IF NOT EXISTS idx_windows_profile ON windows(profile_name);
            """;
        cmd.ExecuteNonQuery();
    }

    public long SaveSnapshot(Snapshot snapshot)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Insert snapshot
            using var snapshotCmd = _connection.CreateCommand();
            snapshotCmd.CommandText = "INSERT INTO snapshots (timestamp) VALUES (@ts) RETURNING id";
            snapshotCmd.Parameters.AddWithValue("@ts", snapshot.Timestamp.ToString("O"));
            long snapshotId = (long)snapshotCmd.ExecuteScalar()!;

            foreach (var window in snapshot.Windows)
            {
                // Insert window
                using var windowCmd = _connection.CreateCommand();
                windowCmd.CommandText = """
                    INSERT INTO windows (snapshot_id, profile_name, profile_display_name, window_index)
                    VALUES (@sid, @pn, @pdn, @wi) RETURNING id
                    """;
                windowCmd.Parameters.AddWithValue("@sid", snapshotId);
                windowCmd.Parameters.AddWithValue("@pn", window.ProfileName);
                windowCmd.Parameters.AddWithValue("@pdn", window.ProfileDisplayName);
                windowCmd.Parameters.AddWithValue("@wi", window.WindowIndex);
                long windowId = (long)windowCmd.ExecuteScalar()!;

                foreach (var tab in window.Tabs)
                {
                    using var tabCmd = _connection.CreateCommand();
                    tabCmd.CommandText = """
                        INSERT INTO tabs (window_id, tab_index, current_url, title, pinned, navigation_history)
                        VALUES (@wid, @ti, @url, @title, @pinned, @nav)
                        """;
                    tabCmd.Parameters.AddWithValue("@wid", windowId);
                    tabCmd.Parameters.AddWithValue("@ti", tab.TabIndex);
                    tabCmd.Parameters.AddWithValue("@url", tab.CurrentUrl);
                    tabCmd.Parameters.AddWithValue("@title", (object?)tab.Title ?? DBNull.Value);
                    tabCmd.Parameters.AddWithValue("@pinned", tab.Pinned ? 1 : 0);
                    tabCmd.Parameters.AddWithValue("@nav", JsonSerializer.Serialize(
                        tab.NavigationHistory.Select(n => new { url = n.Url, title = n.Title }),
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

    public void Dispose()
    {
        _connection.Dispose();
    }
}
