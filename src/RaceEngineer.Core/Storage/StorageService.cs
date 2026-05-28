using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Storage;

public sealed record SessionBrowserRow(
    Guid SessionId,
    DateTimeOffset StartedAt,
    string? Car,
    string? Track,
    TimeSpan? Duration,
    TimeSpan? BestLap,
    int EventCount);

public sealed record SessionNoteRow(
    string Kind,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record SessionReviewBundle(
    Guid SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? Car,
    string? Track,
    IReadOnlyList<TelemetrySnapshot> Snapshots,
    IReadOnlyList<TelemetryEvent> Events,
    IReadOnlyList<CompletedLap> CompletedLaps,
    IReadOnlyList<SessionNoteRow> Notes,
    string SummaryMarkdown);

public sealed class StorageService
{
    private const int SchemaVersion = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly string connectionString;
    private readonly string databaseDirectory;

    public StorageService(string databasePath)
    {
        databaseDirectory = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(databaseDirectory);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS sessions (
              id TEXT PRIMARY KEY,
              started_at TEXT NOT NULL,
              ended_at TEXT,
              track TEXT,
              car TEXT,
              summary_markdown TEXT,
              summary_json TEXT
            );
            CREATE TABLE IF NOT EXISTS snapshots (
              id TEXT PRIMARY KEY,
              session_id TEXT NOT NULL,
              timestamp TEXT NOT NULL,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS completed_laps (
              session_id TEXT NOT NULL,
              lap_number INTEGER NOT NULL,
              started_at TEXT NOT NULL,
              ended_at TEXT NOT NULL,
              duration_ms INTEGER,
              is_valid INTEGER NOT NULL,
              fuel_start REAL,
              fuel_end REAL,
              payload_json TEXT NOT NULL,
              PRIMARY KEY (session_id, lap_number)
            );
            CREATE TABLE IF NOT EXISTS events (
              id TEXT PRIMARY KEY,
              session_id TEXT NOT NULL,
              timestamp TEXT NOT NULL,
              type TEXT NOT NULL,
              severity TEXT NOT NULL,
              lap_number INTEGER,
              lap_progress REAL,
              confidence REAL NOT NULL,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS notes (
              id TEXT PRIMARY KEY,
              session_id TEXT,
              kind TEXT NOT NULL,
              track TEXT,
              car TEXT,
              content TEXT NOT NULL,
              created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS driver_profile (
              driver_id TEXT PRIMARY KEY,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS car_metadata (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS track_metadata (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS coaching_preferences (
              id TEXT PRIMARY KEY,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS race_prep_plans (
              id TEXT PRIMARY KEY,
              car TEXT,
              track TEXT,
              session_type TEXT,
              payload_json TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS post_session_reports (
              session_id TEXT PRIMARY KEY,
              markdown TEXT NOT NULL,
              created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS knowledge_sources (
              id TEXT PRIMARY KEY,
              source_type TEXT NOT NULL,
              title TEXT NOT NULL,
              url TEXT,
              retrieved_at TEXT NOT NULL,
              content TEXT NOT NULL,
              confidence_note TEXT,
              car TEXT,
              track TEXT,
              session_type TEXT,
              category TEXT,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS track_car_memory (
              track TEXT NOT NULL,
              car TEXT NOT NULL,
              payload_json TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              PRIMARY KEY (track, car)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "sessions", "summary_markdown", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "sessions", "summary_json", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "notes", "session_id", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "events", "lap_number", "INTEGER", cancellationToken);
        await EnsureColumnAsync(connection, "events", "lap_progress", "REAL", cancellationToken);
        await EnsureColumnAsync(connection, "events", "confidence", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "knowledge_sources", "payload_json", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
        await SetUserVersionAsync(connection, SchemaVersion, cancellationToken);
    }

    public async Task CreateSessionAsync(Guid sessionId, DateTimeOffset startedAt, string? car = null, string? track = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO sessions (id, started_at, car, track) VALUES ($id, $started_at, $car, $track)";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        command.Parameters.AddWithValue("$started_at", startedAt.ToString("O"));
        command.Parameters.AddWithValue("$car", DbValue(car));
        command.Parameters.AddWithValue("$track", DbValue(track));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, string? summaryMarkdown, object? summary, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET ended_at = $ended_at, summary_markdown = $summary_markdown, summary_json = $summary_json WHERE id = $id";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        command.Parameters.AddWithValue("$ended_at", endedAt.ToString("O"));
        command.Parameters.AddWithValue("$summary_markdown", DbValue(summaryMarkdown));
        command.Parameters.AddWithValue("$summary_json", DbValue(summary is null ? null : JsonSerializer.Serialize(summary, JsonOptions)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSnapshotAsync(Guid sessionId, TelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO snapshots (id, session_id, timestamp, payload_json) VALUES ($id, $session_id, $timestamp, $payload_json)";
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString());
        command.Parameters.AddWithValue("$session_id", sessionId.ToString());
        command.Parameters.AddWithValue("$timestamp", snapshot.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(snapshot, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveEventsAsync(Guid sessionId, IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in events)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT OR IGNORE INTO events (id, session_id, timestamp, type, severity, lap_number, lap_progress, confidence, payload_json) VALUES ($id, $session_id, $timestamp, $type, $severity, $lap_number, $lap_progress, $confidence, $payload_json)";
            command.Parameters.AddWithValue("$id", item.Id.ToString());
            command.Parameters.AddWithValue("$session_id", sessionId.ToString());
            command.Parameters.AddWithValue("$timestamp", item.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$type", item.Type.ToString());
            command.Parameters.AddWithValue("$severity", item.Severity.ToString());
            command.Parameters.AddWithValue("$lap_number", DbValue(item.LapNumber));
            command.Parameters.AddWithValue("$lap_progress", DbValue(item.LapProgress));
            command.Parameters.AddWithValue("$confidence", item.Confidence);
            command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(item, JsonOptions));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveCompletedLapAsync(Guid sessionId, CompletedLap lap, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO completed_laps
              (session_id, lap_number, started_at, ended_at, duration_ms, is_valid, fuel_start, fuel_end, payload_json)
            VALUES
              ($session_id, $lap_number, $started_at, $ended_at, $duration_ms, $is_valid, $fuel_start, $fuel_end, $payload_json)
            """;
        command.Parameters.AddWithValue("$session_id", sessionId.ToString());
        command.Parameters.AddWithValue("$lap_number", lap.LapNumber);
        command.Parameters.AddWithValue("$started_at", lap.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$ended_at", lap.EndedAt.ToString("O"));
        command.Parameters.AddWithValue("$duration_ms", DbValue(lap.Duration.HasValue ? (long)Math.Round(lap.Duration.Value.TotalMilliseconds) : null));
        command.Parameters.AddWithValue("$is_valid", lap.IsValid ? 1 : 0);
        command.Parameters.AddWithValue("$fuel_start", DbValue(lap.FuelStart));
        command.Parameters.AddWithValue("$fuel_end", DbValue(lap.FuelEnd));
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(lap, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddNoteAsync(Guid? sessionId, string kind, string content, string? track = null, string? car = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO notes (id, session_id, kind, track, car, content, created_at) VALUES ($id, $session_id, $kind, $track, $car, $content, $created_at)";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$session_id", DbValue(sessionId));
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$track", DbValue(track));
        command.Parameters.AddWithValue("$car", DbValue(car));
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task AddNoteAsync(string kind, string content, string? track = null, string? car = null, CancellationToken cancellationToken = default)
    {
        return AddNoteAsync(null, kind, content, track, car, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionBrowserRow>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
              s.id,
              s.started_at,
              s.ended_at,
              s.car,
              s.track,
              (SELECT MAX(timestamp) FROM snapshots WHERE session_id = s.id) AS latest_snapshot_at,
              (SELECT MIN(duration_ms) FROM completed_laps WHERE session_id = s.id AND is_valid = 1 AND duration_ms IS NOT NULL) AS best_lap_ms,
              (SELECT COUNT(*) FROM events WHERE session_id = s.id) AS event_count
            FROM sessions s
            ORDER BY s.started_at DESC
            LIMIT 100
            """;

        var rows = new List<SessionBrowserRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var startedAt = DateTimeOffset.Parse(reader.GetString(1));
            var endedAt = ReadDateTime(reader, 2) ?? ReadDateTime(reader, 5);
            var bestLapMs = ReadInt64(reader, 6);
            rows.Add(new SessionBrowserRow(
                Guid.Parse(reader.GetString(0)),
                startedAt,
                ReadString(reader, 3),
                ReadString(reader, 4),
                endedAt.HasValue ? endedAt.Value - startedAt : null,
                bestLapMs.HasValue ? TimeSpan.FromMilliseconds(bestLapMs.Value) : null,
                reader.GetInt32(7)));
        }

        return rows;
    }

    public async Task<string> LoadSessionSummaryMarkdownAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT summary_markdown FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : await BuildMarkdownSummaryAsync(sessionId, cancellationToken);
    }

    public async Task<SessionReviewBundle?> LoadSessionReviewBundleAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var sessionCommand = connection.CreateCommand();
        sessionCommand.CommandText = "SELECT started_at, ended_at, car, track, summary_markdown FROM sessions WHERE id = $id";
        sessionCommand.Parameters.AddWithValue("$id", sessionId.ToString());
        await using var sessionReader = await sessionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await sessionReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var startedAt = DateTimeOffset.Parse(sessionReader.GetString(0));
        var endedAt = ReadDateTime(sessionReader, 1);
        var car = ReadString(sessionReader, 2);
        var track = ReadString(sessionReader, 3);
        var summaryMarkdown = ReadString(sessionReader, 4);
        await sessionReader.CloseAsync();

        if (string.IsNullOrWhiteSpace(summaryMarkdown))
        {
            var reportCommand = connection.CreateCommand();
            reportCommand.CommandText = "SELECT markdown FROM post_session_reports WHERE session_id = $id";
            reportCommand.Parameters.AddWithValue("$id", sessionId.ToString());
            summaryMarkdown = await reportCommand.ExecuteScalarAsync(cancellationToken) as string;
        }

        if (string.IsNullOrWhiteSpace(summaryMarkdown))
        {
            summaryMarkdown = await BuildMarkdownSummaryAsync(sessionId, cancellationToken);
        }

        var snapshots = await LoadSnapshotsAsync(connection, sessionId, cancellationToken);
        var events = await LoadEventsAsync(connection, sessionId, cancellationToken);
        var completedLaps = await LoadCompletedLapsAsync(connection, sessionId, cancellationToken);
        var notes = await LoadSessionNotesAsync(connection, sessionId, cancellationToken);

        return new SessionReviewBundle(
            sessionId,
            startedAt,
            endedAt,
            car,
            track,
            snapshots,
            events,
            completedLaps,
            notes,
            summaryMarkdown);
    }

    public async Task<string> ExportSessionJsonAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var export = await BuildSessionExportAsync(sessionId, cancellationToken);
        var path = ExportPath(sessionId, "json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(export, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }), cancellationToken);
        return path;
    }

    public async Task<string> ExportCoachingMarkdownAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var markdown = await BuildMarkdownSummaryAsync(sessionId, cancellationToken);
        var path = ExportPath(sessionId, "md");
        await File.WriteAllTextAsync(path, markdown, cancellationToken);
        return path;
    }

    public async Task SavePostSessionSummaryAsync(Guid sessionId, string markdown, object? summary, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET summary_markdown = $markdown, summary_json = $json WHERE id = $id";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        command.Parameters.AddWithValue("$markdown", markdown);
        command.Parameters.AddWithValue("$json", DbValue(summary is null ? null : JsonSerializer.Serialize(summary, JsonOptions)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveRacePrepPlanAsync(RacePrepPlan plan, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO race_prep_plans (id, car, track, session_type, payload_json, updated_at)
            VALUES ($id, $car, $track, $session_type, $payload_json, $updated_at)
            """;
        command.Parameters.AddWithValue("$id", PrepKey(plan.Car, plan.Track, plan.SessionType));
        command.Parameters.AddWithValue("$car", DbValue(plan.Car));
        command.Parameters.AddWithValue("$track", DbValue(plan.Track));
        command.Parameters.AddWithValue("$session_type", DbValue(plan.SessionType));
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(plan, JsonOptions));
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RacePrepPlan?> LoadRacePrepPlanAsync(string? car, string? track, string? sessionType = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payload_json FROM race_prep_plans
            WHERE ($car IS NULL OR car = $car)
              AND ($track IS NULL OR track = $track)
              AND ($session_type IS NULL OR session_type = $session_type)
            ORDER BY updated_at DESC
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$car", DbValue(string.IsNullOrWhiteSpace(car) ? null : car));
        command.Parameters.AddWithValue("$track", DbValue(string.IsNullOrWhiteSpace(track) ? null : track));
        command.Parameters.AddWithValue("$session_type", DbValue(string.IsNullOrWhiteSpace(sessionType) ? null : sessionType));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<RacePrepPlan>(json, JsonOptions) : null;
    }

    public async Task SavePostSessionReportAsync(Guid sessionId, string markdown, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO post_session_reports (session_id, markdown, created_at) VALUES ($session_id, $markdown, $created_at)";
        command.Parameters.AddWithValue("$session_id", sessionId.ToString());
        command.Parameters.AddWithValue("$markdown", markdown);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await SavePostSessionSummaryAsync(sessionId, markdown, new { markdown }, cancellationToken);
    }

    public Task SaveDriverProfileAsync(string driverId, object profile, CancellationToken cancellationToken = default)
    {
        return UpsertJsonAsync("driver_profile", "driver_id", driverId, "payload_json", profile, cancellationToken);
    }

    public async Task<Profile.DriverProfileRecord?> LoadDriverProfileAsync(
        string driverId = Profile.ProfilePreferencesService.DefaultDriverId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM driver_profile WHERE driver_id = $driver_id LIMIT 1";
        command.Parameters.AddWithValue("$driver_id", driverId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<Profile.DriverProfileRecord>(json, JsonOptions) : null;
    }

    public Task SaveCarMetadataAsync(string id, string name, object metadata, CancellationToken cancellationToken = default)
    {
        return UpsertNamedJsonAsync("car_metadata", id, name, metadata, cancellationToken);
    }

    public Task SaveTrackMetadataAsync(string id, string name, object metadata, CancellationToken cancellationToken = default)
    {
        return UpsertNamedJsonAsync("track_metadata", id, name, metadata, cancellationToken);
    }

    public async Task<TrackMemoryRecord?> LoadTrackCarMemoryAsync(
        string track,
        string car,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM track_car_memory WHERE track = $track AND car = $car LIMIT 1";
        command.Parameters.AddWithValue("$track", track);
        command.Parameters.AddWithValue("$car", car);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<TrackMemoryRecord>(json, JsonOptions) : null;
    }

    public async Task SaveTrackCarMemoryAsync(TrackMemoryRecord memory, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO track_car_memory (track, car, payload_json, updated_at)
            VALUES ($track, $car, $payload_json, $updated_at)
            """;
        command.Parameters.AddWithValue("$track", memory.TrackName);
        command.Parameters.AddWithValue("$car", memory.CarName);
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(memory, JsonOptions));
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task SaveCoachingPreferencesAsync(string id, object preferences, CancellationToken cancellationToken = default)
    {
        return UpsertJsonAsync("coaching_preferences", "id", id, "payload_json", preferences, cancellationToken);
    }

    public async Task<Profile.CoachingPreferencesPayload?> LoadCoachingPreferencesAsync(
        string id = Profile.ProfilePreferencesService.DefaultPreferencesId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM coaching_preferences WHERE id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json)
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<Profile.CoachingPreferencesPayload>(json, JsonOptions);
        if (payload is not null)
        {
            return payload;
        }

        var legacyCoach = JsonSerializer.Deserialize<Profile.CoachPreferencesRecord>(json, JsonOptions);
        return legacyCoach is null ? null : new Profile.CoachingPreferencesPayload(legacyCoach, Profile.StrategyPreferencesRecord.Default);
    }

    public async Task SaveKnowledgeSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO knowledge_sources
              (id, source_type, title, url, retrieved_at, content, confidence_note, car, track, session_type, category, payload_json)
            VALUES
              ($id, $source_type, $title, $url, $retrieved_at, $content, $confidence_note, $car, $track, $session_type, $category, $payload_json)
            """;
        AddKnowledgeParameters(command, source);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSource>> ListKnowledgeSourcesAsync(
        string? car = null,
        string? track = null,
        string? sessionType = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payload_json FROM knowledge_sources
            WHERE ($car IS NULL OR car = $car)
              AND ($track IS NULL OR track = $track)
              AND ($session_type IS NULL OR session_type = $session_type)
              AND ($category IS NULL OR category = $category)
            ORDER BY retrieved_at DESC
            LIMIT 200
            """;
        command.Parameters.AddWithValue("$car", DbValue(EmptyToNull(car)));
        command.Parameters.AddWithValue("$track", DbValue(EmptyToNull(track)));
        command.Parameters.AddWithValue("$session_type", DbValue(EmptyToNull(sessionType)));
        command.Parameters.AddWithValue("$category", DbValue(EmptyToNull(category)));
        return await ReadKnowledgeSourcesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSource>> SearchKnowledgeSourcesAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var normalized = EmptyToNull(query);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payload_json FROM knowledge_sources
            WHERE $query IS NULL
               OR title LIKE $like
               OR content LIKE $like
               OR car LIKE $like
               OR track LIKE $like
               OR category LIKE $like
            ORDER BY retrieved_at DESC
            LIMIT 50
            """;
        command.Parameters.AddWithValue("$query", DbValue(normalized));
        command.Parameters.AddWithValue("$like", normalized is null ? DBNull.Value : $"%{normalized}%");
        return await ReadKnowledgeSourcesAsync(command, cancellationToken);
    }

    public async Task DeleteKnowledgeSourceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM knowledge_sources WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<KnowledgeSource> ImportKnowledgeFileAsync(
        string filePath,
        string? car = null,
        string? track = null,
        string? sessionType = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var source = KnowledgeSource.ImportedFile(
            Path.GetFileName(filePath),
            content,
            filePath,
            car,
            track,
            sessionType,
            category);
        await SaveKnowledgeSourceAsync(source, cancellationToken);
        return source;
    }

    private async Task<object> BuildSessionExportAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return new
        {
            session = await QuerySingleDictionaryAsync(connection, "SELECT * FROM sessions WHERE id = $id", sessionId, cancellationToken),
            completed_laps = await QueryDictionariesAsync(connection, "SELECT * FROM completed_laps WHERE session_id = $id ORDER BY lap_number", sessionId, cancellationToken),
            events = await QueryDictionariesAsync(connection, "SELECT * FROM events WHERE session_id = $id ORDER BY timestamp", sessionId, cancellationToken),
            notes = await QueryDictionariesAsync(connection, "SELECT * FROM notes WHERE session_id = $id ORDER BY created_at", sessionId, cancellationToken),
            knowledge_sources = await QueryDictionariesAsync(connection, "SELECT * FROM knowledge_sources ORDER BY retrieved_at DESC LIMIT 200", sessionId, cancellationToken),
            report = await QuerySingleDictionaryAsync(connection, "SELECT * FROM post_session_reports WHERE session_id = $id", sessionId, cancellationToken),
            snapshots = await QueryDictionariesAsync(connection, "SELECT * FROM snapshots WHERE session_id = $id ORDER BY timestamp", sessionId, cancellationToken)
        };
    }

    private static void AddKnowledgeParameters(SqliteCommand command, KnowledgeSource source)
    {
        command.Parameters.AddWithValue("$id", source.Id.ToString());
        command.Parameters.AddWithValue("$source_type", source.SourceType);
        command.Parameters.AddWithValue("$title", source.Title);
        command.Parameters.AddWithValue("$url", DbValue(source.Url));
        command.Parameters.AddWithValue("$retrieved_at", source.RetrievedAt.ToString("O"));
        command.Parameters.AddWithValue("$content", source.Content);
        command.Parameters.AddWithValue("$confidence_note", DbValue(source.ConfidenceNote));
        command.Parameters.AddWithValue("$car", DbValue(source.Car));
        command.Parameters.AddWithValue("$track", DbValue(source.Track));
        command.Parameters.AddWithValue("$session_type", DbValue(source.SessionType));
        command.Parameters.AddWithValue("$category", DbValue(source.Category));
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(source, JsonOptions));
    }

    private static async Task<IReadOnlyList<KnowledgeSource>> ReadKnowledgeSourcesAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var sources = new List<KnowledgeSource>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var source = JsonSerializer.Deserialize<KnowledgeSource>(reader.GetString(0), JsonOptions);
            if (source is not null)
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    private async Task UpsertJsonAsync(string table, string keyColumn, string key, string jsonColumn, object payload, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"INSERT OR REPLACE INTO {table} ({keyColumn}, {jsonColumn}) VALUES ($key, $payload_json)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(payload, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpsertNamedJsonAsync(string table, string id, string name, object payload, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"INSERT OR REPLACE INTO {table} (id, name, payload_json) VALUES ($id, $name, $payload_json)";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(payload, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string> BuildMarkdownSummaryAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var sessions = await ListSessionsAsync(cancellationToken);
        var session = sessions.FirstOrDefault(item => item.SessionId == sessionId);
        var export = await BuildSessionExportAsync(sessionId, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("# Coaching Summary");
        builder.AppendLine();
        builder.AppendLine($"- Session: {sessionId}");
        builder.AppendLine($"- Date: {session?.StartedAt.LocalDateTime.ToString("g") ?? "unknown"}");
        builder.AppendLine($"- Car: {session?.Car ?? "unknown"}");
        builder.AppendLine($"- Track: {session?.Track ?? "unknown"}");
        builder.AppendLine($"- Duration: {FormatDuration(session?.Duration)}");
        builder.AppendLine($"- Best lap: {FormatDuration(session?.BestLap)}");
        builder.AppendLine($"- Events: {session?.EventCount.ToString() ?? "0"}");
        builder.AppendLine();
        builder.AppendLine("## Data");
        builder.AppendLine();
        builder.AppendLine("The JSON export contains deterministic events, completed laps, notes, and sampled telemetry snapshots.");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(JsonSerializer.Serialize(export, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
        builder.AppendLine("```");
        return builder.ToString();
    }

    private static async Task<IReadOnlyList<TelemetrySnapshot>> LoadSnapshotsAsync(SqliteConnection connection, Guid sessionId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM snapshots WHERE session_id = $id ORDER BY timestamp";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        return await ReadPayloadListAsync(command, JsonSerializer.Deserialize<TelemetrySnapshot>, cancellationToken);
    }

    private static async Task<IReadOnlyList<TelemetryEvent>> LoadEventsAsync(SqliteConnection connection, Guid sessionId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM events WHERE session_id = $id ORDER BY timestamp";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        return await ReadPayloadListAsync(command, JsonSerializer.Deserialize<TelemetryEvent>, cancellationToken);
    }

    private static async Task<IReadOnlyList<CompletedLap>> LoadCompletedLapsAsync(SqliteConnection connection, Guid sessionId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM completed_laps WHERE session_id = $id ORDER BY lap_number";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        return await ReadPayloadListAsync(command, JsonSerializer.Deserialize<CompletedLap>, cancellationToken);
    }

    private static async Task<IReadOnlyList<SessionNoteRow>> LoadSessionNotesAsync(SqliteConnection connection, Guid sessionId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT kind, content, created_at FROM notes WHERE session_id = $id ORDER BY created_at";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        var notes = new List<SessionNoteRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notes.Add(new SessionNoteRow(
                reader.GetString(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2))));
        }

        return notes;
    }

    private static async Task<IReadOnlyList<T>> ReadPayloadListAsync<T>(
        SqliteCommand command,
        Func<string, JsonSerializerOptions, T?> deserialize,
        CancellationToken cancellationToken)
        where T : class
    {
        var items = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = deserialize(reader.GetString(0), JsonOptions);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private async Task<IReadOnlyDictionary<string, object?>?> QuerySingleDictionaryAsync(SqliteConnection connection, string sql, Guid sessionId, CancellationToken cancellationToken)
    {
        return (await QueryDictionariesAsync(connection, sql, sessionId, cancellationToken)).FirstOrDefault();
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryDictionariesAsync(SqliteConnection connection, string sql, Guid sessionId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private string ExportPath(Guid sessionId, string extension)
    {
        var directory = Path.Combine(databaseDirectory, "Exports");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{sessionId}.{extension}");
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetUserVersionAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version}";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static object DbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string PrepKey(string? car, string? track, string? sessionType)
    {
        return $"{car?.Trim().ToLowerInvariant()}|{track?.Trim().ToLowerInvariant()}|{sessionType?.Trim().ToLowerInvariant()}";
    }

    private static string? ReadString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long? ReadInt64(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static DateTimeOffset? ReadDateTime(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal));
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            return "unknown";
        }

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss\.fff");
    }
}
