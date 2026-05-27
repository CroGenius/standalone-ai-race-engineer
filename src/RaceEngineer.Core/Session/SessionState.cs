using RaceEngineer.Core.Events;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Session;

public sealed class SessionState
{
    private readonly List<TelemetryEvent> events = [];
    private readonly List<CompletedLap> completedLaps = [];
    private DateTimeOffset currentLapStartedAt;
    private double? currentLapStartFuel;
    private bool currentLapInvalid;

    public SessionState()
    {
        SessionId = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
    }

    private SessionState(Guid sessionId, DateTimeOffset startedAt)
    {
        SessionId = sessionId;
        StartedAt = startedAt;
    }

    public Guid SessionId { get; }
    public DateTimeOffset StartedAt { get; }
    public TelemetrySnapshot? LatestSnapshot { get; private set; }
    public IReadOnlyList<TelemetryEvent> RecentEvents => events.TakeLast(50).ToArray();
    public IReadOnlyList<TelemetryEvent> Events => events;
    public IReadOnlyList<CompletedLap> CompletedLaps => completedLaps;
    public bool TelemetryOnline => LatestSnapshot is not null;
    public int CurrentLap { get; private set; } = 1;
    public bool CurrentLapIsValid => !currentLapInvalid;
    public DateTimeOffset CurrentLapStartedAt => currentLapStartedAt == default ? StartedAt : currentLapStartedAt;
    public CompletedLap? LastLap => completedLaps.LastOrDefault();
    public CompletedLap? BestLap => completedLaps
        .Where(lap => lap.IsValid && lap.Duration.HasValue)
        .OrderBy(lap => lap.Duration!.Value)
        .FirstOrDefault();
    public TimeSpan StintDuration => (LatestSnapshot?.Timestamp ?? DateTimeOffset.UtcNow) - StartedAt;
    public double? LatestFuelLevel => LatestSnapshot?.Condition.Fuel;
    public double? FuelUsedPerLap => completedLaps
        .Where(lap => lap.IsValid && lap.FuelUsed.HasValue)
        .Select(lap => lap.FuelUsed!.Value)
        .TakeLast(3)
        .ToArray() is var samples && samples.Length >= 2
            ? samples.Average()
            : null;
    public double? EstimatedLapsRemaining => LatestFuelLevel.HasValue && FuelUsedPerLap is { } perLap && perLap > 0
        ? LatestFuelLevel.Value / perLap
        : null;

    public static SessionState FromPersisted(
        Guid sessionId,
        DateTimeOffset startedAt,
        IReadOnlyList<TelemetrySnapshot> snapshots,
        IReadOnlyList<TelemetryEvent> persistedEvents,
        IReadOnlyList<CompletedLap> persistedLaps)
    {
        var state = new SessionState(sessionId, startedAt);
        state.events.AddRange(persistedEvents);
        state.completedLaps.AddRange(persistedLaps.OrderBy(lap => lap.LapNumber));
        state.LatestSnapshot = snapshots.Count > 0 ? snapshots[^1] : null;

        var lastLapStart = persistedEvents.LastOrDefault(item => item.Type == EventType.LapStart);
        var lastLapEnd = persistedEvents.LastOrDefault(item => item.Type == EventType.LapEnd);
        if (lastLapStart is not null && (lastLapEnd is null || lastLapStart.Timestamp > lastLapEnd.Timestamp))
        {
            state.CurrentLap = lastLapStart.LapNumber ?? state.completedLaps.Count + 1;
            state.currentLapStartedAt = lastLapStart.Timestamp;
        }
        else if (state.completedLaps.Count > 0)
        {
            var lastCompleted = state.completedLaps[^1];
            state.CurrentLap = lastCompleted.LapNumber + 1;
            state.currentLapStartedAt = lastCompleted.EndedAt;
        }

        if (state.LatestSnapshot is not null)
        {
            state.currentLapStartFuel = state.LatestSnapshot.Condition.Fuel;
        }

        return state;
    }

    public void ApplySnapshot(TelemetrySnapshot snapshot, IReadOnlyList<TelemetryEvent> newEvents)
    {
        if (LatestSnapshot is null)
        {
            currentLapStartedAt = snapshot.Timestamp;
            currentLapStartFuel = snapshot.Condition.Fuel;
        }

        LatestSnapshot = snapshot;
        if (FlagsIndicateInvalidLap(snapshot.Race.Flags))
        {
            currentLapInvalid = true;
        }

        foreach (var item in newEvents)
        {
            ApplyEvent(snapshot, item);
        }

        events.AddRange(newEvents);
    }

    private void ApplyEvent(TelemetrySnapshot snapshot, TelemetryEvent item)
    {
        if (item.Type == EventType.LapEnd)
        {
            var lapNumber = item.LapNumber ?? CurrentLap;
            var duration = EvidenceTimeSpan(item, "duration_seconds");
            completedLaps.Add(new CompletedLap(
                lapNumber,
                currentLapStartedAt == default ? StartedAt : currentLapStartedAt,
                item.Timestamp,
                duration,
                !currentLapInvalid,
                currentLapStartFuel,
                snapshot.Condition.Fuel));
        }
        else if (item.Type == EventType.LapStart)
        {
            CurrentLap = item.LapNumber ?? CurrentLap + 1;
            currentLapStartedAt = item.Timestamp;
            currentLapStartFuel = snapshot.Condition.Fuel;
            currentLapInvalid = FlagsIndicateInvalidLap(snapshot.Race.Flags);
        }
    }

    private static TimeSpan? EvidenceTimeSpan(TelemetryEvent item, string key)
    {
        if (!item.Evidence.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            double seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            float seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            decimal seconds when seconds > 0 => TimeSpan.FromSeconds((double)seconds),
            int seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            long seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            _ => null
        };
    }

    private static bool FlagsIndicateInvalidLap(object? flags)
    {
        return flags?.ToString()?.Contains("invalid", StringComparison.OrdinalIgnoreCase) == true;
    }
}
