# TabHistorian - Chrome Tab Backup Tool Implementation Plan

## Context
Build a .NET 10 / C# background service that snapshots all Chrome tabs (across all windows and profiles) with navigation history every 30 minutes, stored in SQLite.

Chrome uses SNSS binary format (version 3, modern) with files in `<Profile>/Sessions/` named `Session_<timestamp>` and `Tabs_<timestamp>`. The most recent `Session_*` file contains the current session state as a sequential command log.

## Implementation Steps

### Step 1: Project scaffolding
- Create solution + Worker Service project targeting `net10.0`
- Add NuGet packages: `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Hosting`
- Create folder structure: `Models/`, `Services/`, `Parsing/`

### Step 2: Models
- `Snapshot.cs` - id, timestamp
- `ChromeWindow.cs` - id, snapshot_id, profile info, window_index
- `ChromeTab.cs` - id, window_id, tab_index, current_url, title, pinned, navigation_history
- `NavigationEntry.cs` - url, title, transition_type

### Step 3: Chrome Profile Discovery (`Services/ChromeProfileDiscovery.cs`)
- Read `Local State` JSON from `%LOCALAPPDATA%\Google\Chrome\User Data\`
- Parse `profile.info_cache` to get profile dirs + display names
- Return list of profile paths

### Step 4: SNSS Parser (`Parsing/SnssParser.cs`, `Parsing/PickleReader.cs`)
- Parse 8-byte file header (magic `0x53534E53`, version)
- Read command stream: 2-byte size + 1-byte command ID + payload
- Handle version 3 marker command (ID 255)
- `PickleReader`: read int32, int64, string (UTF-8), string16 (UTF-16), with 4-byte alignment
- Key session commands to handle:
  - ID 0 `SetTabWindow` - link tab to window (IDAndIndexPayload: two int32s)
  - ID 2 `SetTabIndexInWindow` - tab position (IDAndIndexPayload)
  - ID 6 `UpdateTabNavigation` - Pickle with tab_id, nav_index, url, title, page_state, transition, etc.
  - ID 7 `SetSelectedNavigationIndex` - current nav position (IDAndIndexPayload)
  - ID 8 `SetSelectedTabInIndex` - active tab in window (IDAndIndexPayload)
  - ID 12 `SetPinnedState` - 8-byte struct (tab_id + bool + padding)
  - ID 16/17 `TabClosed`/`WindowClosed` - mark entities as closed

### Step 5: Session Reconstructor (`Services/SessionFileReader.cs`)
- For each profile, find the newest `Session_*` file in `<Profile>/Sessions/`
- Copy to temp dir (handles file locking while Chrome is open)
- Parse SNSS commands and replay them to build state:
  - `Dictionary<int, WindowState>` keyed by window ID
  - `Dictionary<int, TabState>` keyed by tab ID
  - Link tabs to windows, build nav history per tab, track pinned/active state
- Filter out closed windows/tabs
- Return structured data: list of windows, each with their tabs and nav histories

### Step 6: SQLite Storage (`Services/StorageService.cs`)
- Create DB + tables on first run (schema from handoff doc)
- `SaveSnapshot()` - insert snapshot, windows, tabs in a transaction
- Connection string: `Data Source=tabhistorian.db` in app directory

### Step 7: Snapshot Orchestrator (`Services/SnapshotService.cs`)
- Discover all profiles
- For each profile, copy session files and parse
- Assemble full snapshot across all profiles
- Save to SQLite via StorageService
- Clean up temp files

### Step 8: Background Worker (`Worker.cs` + `Program.cs`)
- `BackgroundService` with 30-minute timer
- Run snapshot on startup, then every 30 minutes
- Graceful handling: Chrome not running (skip), parse errors (log and continue)
- Standard `Host.CreateDefaultBuilder` setup with logging

## Key Files
```
TabHistorian/
├── TabHistorian.sln
└── src/
    └── TabHistorian/
        ├── Program.cs
        ├── Worker.cs
        ├── TabHistorian.csproj
        ├── Models/
        │   ├── Snapshot.cs
        │   ├── ChromeWindow.cs
        │   ├── ChromeTab.cs
        │   └── NavigationEntry.cs
        ├── Services/
        │   ├── ChromeProfileDiscovery.cs
        │   ├── SessionFileReader.cs
        │   ├── SnapshotService.cs
        │   └── StorageService.cs
        └── Parsing/
            ├── SnssParser.cs
            └── PickleReader.cs
```

## Verification
1. `dotnet build` - compiles without errors
2. `dotnet run` - runs, discovers Chrome profiles, parses session files, creates SQLite DB with a snapshot
3. Manually inspect the DB to verify tabs/windows/navigation data looks correct
