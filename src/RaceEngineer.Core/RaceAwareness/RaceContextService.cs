using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.RaceAwareness;

public static class RaceContextService
{
    public static readonly string[] AllTrackedFields =
    [
        "track_name",
        "circuit_id",
        "car_name",
        "car_class",
        "session_type",
        "current_lap",
        "total_laps",
        "position",
        "total_cars",
        "gap_ahead_s",
        "gap_behind_s",
        "car_ahead",
        "car_behind",
        "pit_state",
        "flags",
        "sector",
        "session_time_remaining_s",
        "laps_remaining"
    ];

    public static LiveRaceContext Build(
        SessionState session,
        TelemetrySnapshot? snapshot,
        RacePrepPlan? prep = null,
        SessionContextAssessment? sessionContext = null)
    {
        var awareness = snapshot?.RaceAwareness;
        var trackName = FirstNonEmpty(awareness?.TrackName, prep?.Track);
        var carName = FirstNonEmpty(awareness?.CarName, prep?.Car);
        var sessionType = FirstNonEmpty(awareness?.SessionType, prep?.SessionType, sessionContext?.Phase.ToString());

        var currentLap = awareness?.CurrentLap ?? snapshot?.Lap.LapNumber ?? (session.CurrentLap > 0 ? session.CurrentLap : null);
        var position = awareness?.Position ?? snapshot?.Race.Position;
        var flags = FormatFlags(awareness?.Flags, snapshot?.Race.Flags);
        var pitState = awareness?.PitState ?? InferPitState(sessionContext, flags);

        var present = new List<string>();
        var missing = new List<string>();
        Track("track_name", trackName, present, missing);
        Track("circuit_id", awareness?.CircuitId, present, missing);
        Track("car_name", carName, present, missing);
        Track("car_class", awareness?.CarClass, present, missing);
        Track("session_type", sessionType, present, missing);
        Track("current_lap", currentLap?.ToString(), present, missing);
        Track("total_laps", awareness?.TotalLaps?.ToString(), present, missing);
        Track("position", position?.ToString(), present, missing);
        Track("total_cars", awareness?.TotalCars?.ToString(), present, missing);
        Track("gap_ahead_s", awareness?.GapAheadSeconds?.ToString("0.000"), present, missing);
        Track("gap_behind_s", awareness?.GapBehindSeconds?.ToString("0.000"), present, missing);
        Track("car_ahead", awareness?.CarAhead, present, missing);
        Track("car_behind", awareness?.CarBehind, present, missing);
        Track("pit_state", pitState, present, missing);
        Track("flags", flags, present, missing);
        Track("sector", awareness?.Sector?.ToString(), present, missing);
        Track("session_time_remaining_s", awareness?.SessionTimeRemainingSeconds?.ToString("0"), present, missing);
        Track("laps_remaining", awareness?.LapsRemaining?.ToString(), present, missing);

        var confidence = present.Count switch
        {
            0 => RaceContextConfidence.Unavailable,
            >= 8 => RaceContextConfidence.Good,
            _ => RaceContextConfidence.Partial
        };

        var summary = present.Count == 0
            ? "No race awareness fields are available from telemetry or prep."
            : $"{present.Count}/{AllTrackedFields.Length} race fields available ({string.Join(", ", present)}).";

        return new LiveRaceContext(
            trackName,
            awareness?.CircuitId,
            carName,
            awareness?.CarClass,
            sessionType,
            currentLap,
            awareness?.TotalLaps,
            position,
            awareness?.TotalCars,
            awareness?.GapAheadSeconds,
            awareness?.GapBehindSeconds,
            awareness?.CarAhead,
            awareness?.CarBehind,
            pitState,
            flags,
            awareness?.Sector,
            awareness?.SessionTimeRemainingSeconds,
            awareness?.LapsRemaining,
            confidence,
            new RaceFieldDiagnostics(present, missing, summary));
    }

    public static RaceFieldDiagnostics BuildTelemetryDiagnostics(TelemetrySnapshot? snapshot)
    {
        if (snapshot?.RaceAwareness is null)
        {
            return new RaceFieldDiagnostics([], AllTrackedFields, "Latest packet has no race-awareness block.");
        }

        var awareness = snapshot.RaceAwareness;
        var present = new List<string>();
        var missing = new List<string>();
        Track("track_name", awareness.TrackName, present, missing);
        Track("circuit_id", awareness.CircuitId, present, missing);
        Track("car_name", awareness.CarName, present, missing);
        Track("car_class", awareness.CarClass, present, missing);
        Track("session_type", awareness.SessionType, present, missing);
        Track("current_lap", awareness.CurrentLap?.ToString(), present, missing);
        Track("total_laps", awareness.TotalLaps?.ToString(), present, missing);
        Track("position", awareness.Position?.ToString(), present, missing);
        Track("total_cars", awareness.TotalCars?.ToString(), present, missing);
        Track("gap_ahead_s", awareness.GapAheadSeconds?.ToString("0.000"), present, missing);
        Track("gap_behind_s", awareness.GapBehindSeconds?.ToString("0.000"), present, missing);
        Track("car_ahead", awareness.CarAhead, present, missing);
        Track("car_behind", awareness.CarBehind, present, missing);
        Track("pit_state", awareness.PitState, present, missing);
        Track("flags", awareness.Flags, present, missing);
        Track("sector", awareness.Sector?.ToString(), present, missing);
        Track("session_time_remaining_s", awareness.SessionTimeRemainingSeconds?.ToString("0"), present, missing);
        Track("laps_remaining", awareness.LapsRemaining?.ToString(), present, missing);

        return new RaceFieldDiagnostics(
            present,
            missing,
            present.Count == 0
                ? "Race-awareness packet parsed but all fields were empty."
                : $"Telemetry race fields present: {string.Join(", ", present)}.");
    }

    private static void Track(string field, string? value, ICollection<string> present, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(field);
            return;
        }

        present.Add(field);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? FormatFlags(string? awarenessFlags, object? raceFlags)
    {
        if (!string.IsNullOrWhiteSpace(awarenessFlags))
        {
            return awarenessFlags.Trim();
        }

        return raceFlags switch
        {
            null => null,
            string text => text,
            _ => raceFlags.ToString()
        };
    }

    private static string? InferPitState(SessionContextAssessment? sessionContext, string? flags)
    {
        if (sessionContext?.Activity == VehicleActivity.PitLane)
        {
            return "pit lane";
        }

        if (!string.IsNullOrWhiteSpace(flags)
            && flags.Contains("pit", StringComparison.OrdinalIgnoreCase))
        {
            return "in pit";
        }

        return null;
    }
}
