using Microsoft.Data.Sqlite;

namespace TabHistorian.Common;

public record TabIdentityRow(
    long Id, string ProfileName,
    string FirstUrl, string FirstTitle, string FirstSeen,
    string LastUrl, string LastTitle, string LastSeen,
    string? LastActiveTime, int EventCount, bool IsOpen);

public record TabEventRow(
    long Id, long TabIdentityId, string EventType, string Timestamp,
    string? StateDelta, string? Url, string? Title, string? ProfileName);

public record TabMachineStatsRow(
    int TotalTabs, int OpenTabs, int ClosedTabs, int TotalEvents,
    string? FirstSeen, string? LastSeen);

public record CurrentStateRow(
    long TabIdentityId, string CurrentUrl, string Title, bool Pinned,
    string? LastActiveTime, int TabIndex, int WindowIndex,
    string ProfileName, string? ProfileDisplayName,
    string? NavigationHistory, bool IsOpen);

public class TabMachineDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TabMachineDb(TabHistorianSettings settings)
    {
        var dbPath = settings.ResolvedTabMachineDatabasePath;

        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"TabMachine database not found at {dbPath}. Run the TabHistorian service first.");

        _connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadWrite");
        _connection.Open();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=ON";
        cmd.ExecuteNonQuery();
    }

    public TabMachineStatsRow GetStats()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM tab_identities),
                (SELECT COUNT(*) FROM tab_current_state WHERE is_open = 1),
                (SELECT COUNT(*) FROM tab_current_state WHERE is_open = 0),
                (SELECT COUNT(*) FROM tab_events),
                (SELECT MIN(first_seen) FROM tab_identities),
                (SELECT MAX(last_seen) FROM tab_identities)
            """;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new TabMachineStatsRow(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    public List<string> GetProfiles()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT profile_name FROM tab_identities ORDER BY profile_name";
        var results = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(reader.GetString(0));
        return results;
    }

    public int CountSearch(string? query, string? profile, bool? isOpen)
    {
        using var cmd = _connection.CreateCommand();
        var where = BuildSearchWhere(cmd, query, profile, isOpen);
        cmd.CommandText = $"""
            SELECT COUNT(*)
            FROM tab_identities ti
            LEFT JOIN tab_current_state cs ON cs.tab_identity_id = ti.id
            {where}
            """;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<TabIdentityRow> Search(string? query, string? profile, bool? isOpen, int offset, int limit)
    {
        using var cmd = _connection.CreateCommand();
        var where = BuildSearchWhere(cmd, query, profile, isOpen);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        cmd.CommandText = $"""
            SELECT ti.id, ti.profile_name, ti.first_url, ti.first_title, ti.first_seen,
                   ti.last_url, ti.last_title, ti.last_seen, ti.last_active_time,
                   (SELECT COUNT(*) FROM tab_events te WHERE te.tab_identity_id = ti.id) as event_count,
                   COALESCE(cs.is_open, 0)
            FROM tab_identities ti
            LEFT JOIN tab_current_state cs ON cs.tab_identity_id = ti.id
            {where}
            ORDER BY ti.last_seen DESC
            LIMIT @limit OFFSET @offset
            """;

        var results = new List<TabIdentityRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new TabIdentityRow(
                reader.GetInt64(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10) != 0));
        }
        return results;
    }

    public List<TabEventRow> GetEvents(long? tabIdentityId, string? eventType, string? before, string? after, int offset, int limit)
    {
        using var cmd = _connection.CreateCommand();
        var conditions = new List<string>();

        if (tabIdentityId.HasValue)
        {
            cmd.Parameters.AddWithValue("@tabIdentityId", tabIdentityId.Value);
            conditions.Add("te.tab_identity_id = @tabIdentityId");
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            cmd.Parameters.AddWithValue("@eventType", eventType);
            conditions.Add("te.event_type = @eventType");
        }
        if (!string.IsNullOrWhiteSpace(before))
        {
            cmd.Parameters.AddWithValue("@before", before);
            conditions.Add("te.timestamp <= @before");
        }
        if (!string.IsNullOrWhiteSpace(after))
        {
            cmd.Parameters.AddWithValue("@after", after);
            conditions.Add("te.timestamp >= @after");
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        cmd.CommandText = $"""
            SELECT te.id, te.tab_identity_id, te.event_type, te.timestamp,
                   te.state_delta, te.url, te.title, te.profile_name
            FROM tab_events te
            {where}
            ORDER BY te.timestamp DESC
            LIMIT @limit OFFSET @offset
            """;

        var results = new List<TabEventRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new TabEventRow(
                reader.GetInt64(0), reader.GetInt64(1),
                reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }
        return results;
    }

    public int CountEvents(long? tabIdentityId, string? eventType, string? before, string? after)
    {
        using var cmd = _connection.CreateCommand();
        var conditions = new List<string>();

        if (tabIdentityId.HasValue)
        {
            cmd.Parameters.AddWithValue("@tabIdentityId", tabIdentityId.Value);
            conditions.Add("te.tab_identity_id = @tabIdentityId");
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            cmd.Parameters.AddWithValue("@eventType", eventType);
            conditions.Add("te.event_type = @eventType");
        }
        if (!string.IsNullOrWhiteSpace(before))
        {
            cmd.Parameters.AddWithValue("@before", before);
            conditions.Add("te.timestamp <= @before");
        }
        if (!string.IsNullOrWhiteSpace(after))
        {
            cmd.Parameters.AddWithValue("@after", after);
            conditions.Add("te.timestamp >= @after");
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        cmd.CommandText = $"""
            SELECT COUNT(*)
            FROM tab_events te
            {where}
            """;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<CurrentStateRow> GetTimeline(string timestamp, string? profile)
    {
        // Get tabs that were open at the given timestamp:
        // - Had an Opened event at or before the timestamp
        // - Did NOT have a Closed event at or before the timestamp (or Closed was after the timestamp)
        using var cmd = _connection.CreateCommand();
        cmd.Parameters.AddWithValue("@timestamp", timestamp);

        var profileFilter = "";
        if (!string.IsNullOrWhiteSpace(profile))
        {
            cmd.Parameters.AddWithValue("@profile", profile);
            profileFilter = "AND ti.profile_name = @profile";
        }

        cmd.CommandText = $"""
            SELECT cs.tab_identity_id, cs.current_url, cs.title, cs.pinned,
                   cs.last_active_time, cs.tab_index, cs.window_index,
                   cs.profile_name, cs.profile_display_name,
                   cs.navigation_history, cs.is_open
            FROM tab_identities ti
            JOIN tab_current_state cs ON cs.tab_identity_id = ti.id
            WHERE ti.first_seen <= @timestamp
              AND NOT EXISTS (
                  SELECT 1 FROM tab_events te
                  WHERE te.tab_identity_id = ti.id
                    AND te.event_type = 'Closed'
                    AND te.timestamp <= @timestamp
              )
              {profileFilter}
            ORDER BY cs.profile_name, cs.window_index, cs.tab_index
            """;

        var results = new List<CurrentStateRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CurrentStateRow(
                reader.GetInt64(0), reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                !reader.IsDBNull(3) && reader.GetInt32(3) != 0,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                !reader.IsDBNull(10) && reader.GetInt32(10) != 0));
        }
        return results;
    }

    private static string BuildSearchWhere(SqliteCommand cmd, string? query, string? profile, bool? isOpen)
    {
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            cmd.Parameters.AddWithValue("@q", $"%{query}%");
            conditions.Add("(ti.last_url LIKE @q OR ti.last_title LIKE @q OR ti.first_url LIKE @q OR ti.first_title LIKE @q)");
        }
        if (!string.IsNullOrWhiteSpace(profile))
        {
            cmd.Parameters.AddWithValue("@profile", profile);
            conditions.Add("ti.profile_name = @profile");
        }
        if (isOpen.HasValue)
        {
            cmd.Parameters.AddWithValue("@isOpen", isOpen.Value ? 1 : 0);
            conditions.Add("COALESCE(cs.is_open, 0) = @isOpen");
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }

    public void Dispose() => _connection.Dispose();
}
