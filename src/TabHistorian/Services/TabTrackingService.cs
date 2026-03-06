using System.Text.Json;
using Microsoft.Data.Sqlite;
using TabHistorian.Models;

namespace TabHistorian.Services;

/// <summary>
/// Tracks tab identity across snapshots using heuristic matching.
/// Diffs consecutive snapshots to produce tab lifecycle events (opened, closed, navigated, etc.).
/// </summary>
public class TabTrackingService
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<TabTrackingService> _logger;

    public TabTrackingService(SqliteConnection connection, ILogger<TabTrackingService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    private record SnapshotTab(
        long TabDbId,
        string ProfileName,
        string Url,
        string Title,
        bool Pinned,
        string? LastActiveTime,
        int WindowIndex,
        int TabIndex,
        string? SyncTabNodeId,
        List<string> NavUrls);

    public void ProcessSnapshot(long snapshotId, DateTime snapshotTimestamp)
    {
        var previousSnapshotId = GetPreviousSnapshotId(snapshotId);
        var currentTabs = LoadSnapshotTabs(snapshotId);

        if (previousSnapshotId == null)
        {
            _logger.LogInformation("First snapshot for tracking — recording {Count} tabs as opened", currentTabs.Count);
            foreach (var tab in currentTabs)
            {
                var identityId = CreateTabIdentity(tab, snapshotTimestamp);
                LinkTabToIdentity(tab.TabDbId, identityId);
                RecordEvent(identityId, TabEventType.Opened, snapshotTimestamp, snapshotId, tab);
            }
            return;
        }

        var previousTabs = LoadSnapshotTabs(previousSnapshotId.Value);
        DiffAndRecordEvents(previousTabs, currentTabs, snapshotTimestamp, snapshotId);
    }

    /// <summary>
    /// Retroactively processes all existing snapshots in chronological order.
    /// Skips if already processed (events exist).
    /// </summary>
    public void ProcessExistingSnapshots()
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM tab_events";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count > 0)
            {
                _logger.LogInformation("Tab events already exist ({Count} events), skipping retroactive processing", count);
                return;
            }
        }

        var snapshots = new List<(long Id, DateTime Timestamp)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, timestamp FROM snapshots ORDER BY timestamp ASC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                snapshots.Add((
                    reader.GetInt64(0),
                    DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind)));
            }
        }

        if (snapshots.Count == 0) return;

        _logger.LogInformation("Retroactively processing {Count} existing snapshots for tab tracking", snapshots.Count);

        using var transaction = _connection.BeginTransaction();
        try
        {
            List<SnapshotTab>? previousTabs = null;

            for (int i = 0; i < snapshots.Count; i++)
            {
                var (id, ts) = snapshots[i];
                var currentTabs = LoadSnapshotTabs(id);

                if (i == 0)
                {
                    foreach (var tab in currentTabs)
                    {
                        var identityId = CreateTabIdentity(tab, ts);
                        LinkTabToIdentity(tab.TabDbId, identityId);
                        RecordEvent(identityId, TabEventType.Opened, ts, id, tab);
                    }
                }
                else
                {
                    DiffAndRecordEvents(previousTabs!, currentTabs, ts, id);
                }

                previousTabs = currentTabs;

                if ((i + 1) % 100 == 0)
                    _logger.LogInformation("Processed {Current}/{Total} snapshots", i + 1, snapshots.Count);
            }

            transaction.Commit();
            _logger.LogInformation("Retroactive processing complete");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void DiffAndRecordEvents(
        List<SnapshotTab> previousTabs,
        List<SnapshotTab> currentTabs,
        DateTime timestamp,
        long snapshotId)
    {
        var (matched, opened, closed) = MatchTabs(previousTabs, currentTabs);

        foreach (var tab in opened)
        {
            var identityId = CreateTabIdentity(tab, timestamp);
            LinkTabToIdentity(tab.TabDbId, identityId);
            RecordEvent(identityId, TabEventType.Opened, timestamp, snapshotId, tab);
        }

        foreach (var tab in closed)
        {
            var identityId = LookupIdentityForTab(tab.TabDbId);
            if (identityId != null)
                RecordEvent(identityId.Value, TabEventType.Closed, timestamp, snapshotId, tab);
        }

        foreach (var (prev, curr) in matched)
        {
            var identityId = LookupIdentityForTab(prev.TabDbId);
            if (identityId == null)
            {
                // Identity lost (e.g. from pruned snapshots) — create a new one
                identityId = CreateTabIdentity(curr, timestamp);
                LinkTabToIdentity(curr.TabDbId, identityId.Value);
                RecordEvent(identityId.Value, TabEventType.Opened, timestamp, snapshotId, curr);
                continue;
            }

            // Link current tab to same identity
            LinkTabToIdentity(curr.TabDbId, identityId.Value);

            bool changed = false;

            if (prev.Url != curr.Url)
            {
                RecordEvent(identityId.Value, TabEventType.Navigated, timestamp, snapshotId, curr, prev.Url, prev.Title);
                changed = true;
            }
            else if (prev.Title != curr.Title && !string.IsNullOrEmpty(curr.Title))
            {
                RecordEvent(identityId.Value, TabEventType.TitleChanged, timestamp, snapshotId, curr, null, prev.Title);
                changed = true;
            }

            if (prev.Pinned != curr.Pinned)
            {
                RecordEvent(identityId.Value, curr.Pinned ? TabEventType.Pinned : TabEventType.Unpinned,
                    timestamp, snapshotId, curr);
                changed = true;
            }

            if (changed)
                UpdateTabIdentity(identityId.Value, curr, timestamp);
        }
    }

    /// <summary>
    /// Matches tabs between two snapshots using navigation history.
    /// Unmatched tabs become opened/closed events — we prefer false negatives over false positives.
    /// </summary>
    private static (List<(SnapshotTab Prev, SnapshotTab Curr)> Matched,
                     List<SnapshotTab> Opened,
                     List<SnapshotTab> Closed)
        MatchTabs(List<SnapshotTab> previous, List<SnapshotTab> current)
    {
        var matched = new List<(SnapshotTab, SnapshotTab)>();
        var unmatchedPrev = new List<SnapshotTab>(previous);
        var unmatchedCurr = new List<SnapshotTab>(current);

        // Pass 0: Synced tabs matched by stable SyncTabNodeId
        MatchByPredicate(unmatchedPrev, unmatchedCurr, matched,
            (p, c) => p.SyncTabNodeId != null
                    && p.SyncTabNodeId == c.SyncTabNodeId);

        // Passes 1–3 use heuristics — skip pairs where both tabs have a SyncTabNodeId,
        // since those should only match via Pass 0. Allow heuristics when either side
        // lacks an ID (e.g. transition from old snapshots without sync_tab_node_id).
        static bool allowHeuristic(SnapshotTab p, SnapshotTab c)
            => p.SyncTabNodeId == null || c.SyncTabNodeId == null;

        // Pass 1: Exact nav history match — tab hasn't navigated
        MatchByPredicate(unmatchedPrev, unmatchedCurr, matched,
            (p, c) => allowHeuristic(p, c)
                    && p.ProfileName == c.ProfileName
                    && p.NavUrls.Count >= 2
                    && p.NavUrls.Count == c.NavUrls.Count
                    && p.NavUrls.SequenceEqual(c.NavUrls));

        // Pass 2: Nav history prefix — tab navigated forward
        // (e.g. [A, B, C] → [A, B, C, D])
        MatchByPredicate(unmatchedPrev, unmatchedCurr, matched,
            (p, c) => allowHeuristic(p, c)
                    && p.ProfileName == c.ProfileName
                    && p.NavUrls.Count >= 2
                    && c.NavUrls.Count > p.NavUrls.Count
                    && c.NavUrls.Take(p.NavUrls.Count).SequenceEqual(p.NavUrls));

        // Pass 3: Single-nav-entry tabs matched by (profile, URL)
        // These tabs have no back/forward history so nav matching can't help.
        // A false positive just merges two identical-URL tabs — acceptable tradeoff.
        MatchByPredicate(unmatchedPrev, unmatchedCurr, matched,
            (p, c) => allowHeuristic(p, c)
                    && p.ProfileName == c.ProfileName
                    && p.NavUrls.Count <= 1
                    && c.NavUrls.Count <= 1
                    && p.Url == c.Url);

        return (matched, unmatchedCurr, unmatchedPrev);
    }

    private static void MatchByPredicate(
        List<SnapshotTab> unmatchedPrev,
        List<SnapshotTab> unmatchedCurr,
        List<(SnapshotTab, SnapshotTab)> matched,
        Func<SnapshotTab, SnapshotTab, bool> predicate)
    {
        for (int i = unmatchedCurr.Count - 1; i >= 0; i--)
        {
            var curr = unmatchedCurr[i];
            for (int j = unmatchedPrev.Count - 1; j >= 0; j--)
            {
                if (predicate(unmatchedPrev[j], curr))
                {
                    matched.Add((unmatchedPrev[j], curr));
                    unmatchedPrev.RemoveAt(j);
                    unmatchedCurr.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private List<SnapshotTab> LoadSnapshotTabs(long snapshotId)
    {
        var tabs = new List<SnapshotTab>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, w.profile_name, t.current_url, t.title, t.pinned,
                   t.last_active_time, w.window_index, t.tab_index, t.navigation_history,
                   t.sync_tab_node_id
            FROM tabs t
            JOIN windows w ON w.id = t.window_id
            WHERE w.snapshot_id = @sid
            ORDER BY w.window_index, t.tab_index
            """;
        cmd.Parameters.AddWithValue("@sid", snapshotId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var navJson = reader.IsDBNull(8) ? "[]" : reader.GetString(8);
            var navUrls = ExtractNavUrls(navJson);

            tabs.Add(new SnapshotTab(
                TabDbId: reader.GetInt64(0),
                ProfileName: reader.GetString(1),
                Url: reader.GetString(2),
                Title: reader.IsDBNull(3) ? "" : reader.GetString(3),
                Pinned: !reader.IsDBNull(4) && reader.GetInt32(4) != 0,
                LastActiveTime: reader.IsDBNull(5) ? null : reader.GetString(5),
                WindowIndex: reader.GetInt32(6),
                TabIndex: reader.GetInt32(7),
                SyncTabNodeId: reader.IsDBNull(9) ? null : reader.GetString(9),
                NavUrls: navUrls));
        }

        return tabs;
    }

    private static List<string> ExtractNavUrls(string navJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(navJson);
            return doc.RootElement.EnumerateArray()
                .Select(e => e.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "")
                .Where(u => !string.IsNullOrEmpty(u))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private long? GetPreviousSnapshotId(long currentSnapshotId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id FROM snapshots
            WHERE id < @id
            ORDER BY id DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@id", currentSnapshotId);
        var result = cmd.ExecuteScalar();
        return result == null ? null : (long)result;
    }

    private long CreateTabIdentity(SnapshotTab tab, DateTime timestamp)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tab_identities (profile_name, first_url, first_title, first_seen,
                last_url, last_title, last_seen, last_active_time)
            VALUES (@pn, @fu, @ft, @fs, @lu, @lt, @ls, @lat)
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("@pn", tab.ProfileName);
        cmd.Parameters.AddWithValue("@fu", tab.Url);
        cmd.Parameters.AddWithValue("@ft", tab.Title);
        cmd.Parameters.AddWithValue("@fs", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@lu", tab.Url);
        cmd.Parameters.AddWithValue("@lt", tab.Title);
        cmd.Parameters.AddWithValue("@ls", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@lat", (object?)tab.LastActiveTime ?? DBNull.Value);

        return (long)cmd.ExecuteScalar()!;
    }

    private void LinkTabToIdentity(long tabDbId, long identityId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO tab_identity_map (tab_id, tab_identity_id)
            VALUES (@tid, @iid)
            """;
        cmd.Parameters.AddWithValue("@tid", tabDbId);
        cmd.Parameters.AddWithValue("@iid", identityId);
        cmd.ExecuteNonQuery();
    }

    private long? LookupIdentityForTab(long tabDbId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT tab_identity_id FROM tab_identity_map WHERE tab_id = @tid";
        cmd.Parameters.AddWithValue("@tid", tabDbId);
        var result = cmd.ExecuteScalar();
        return result == null ? null : (long)result;
    }

    private void UpdateTabIdentity(long identityId, SnapshotTab tab, DateTime timestamp)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE tab_identities
            SET last_url = @lu, last_title = @lt, last_seen = @ls, last_active_time = @lat
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@lu", tab.Url);
        cmd.Parameters.AddWithValue("@lt", tab.Title);
        cmd.Parameters.AddWithValue("@ls", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@lat", (object?)tab.LastActiveTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", identityId);
        cmd.ExecuteNonQuery();
    }

    private void RecordEvent(long identityId, TabEventType eventType,
        DateTime timestamp, long snapshotId, SnapshotTab tab,
        string? oldUrl = null, string? oldTitle = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tab_events (tab_identity_id, event_type, timestamp, snapshot_id,
                url, title, old_url, old_title, profile_name, window_index, tab_index)
            VALUES (@tid, @et, @ts, @sid, @url, @title, @ou, @ot, @pn, @wi, @ti)
            """;
        cmd.Parameters.AddWithValue("@tid", identityId);
        cmd.Parameters.AddWithValue("@et", eventType.ToString());
        cmd.Parameters.AddWithValue("@ts", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        cmd.Parameters.AddWithValue("@url", tab.Url);
        cmd.Parameters.AddWithValue("@title", (object?)tab.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ou", (object?)oldUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ot", (object?)oldTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pn", tab.ProfileName);
        cmd.Parameters.AddWithValue("@wi", tab.WindowIndex);
        cmd.Parameters.AddWithValue("@ti", tab.TabIndex);
        cmd.ExecuteNonQuery();
    }
}
