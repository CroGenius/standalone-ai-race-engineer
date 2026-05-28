namespace RaceEngineer.Core.RaceAwareness;

public enum RaceAwarenessSubtopic
{
    TrackIdentity,
    CarIdentity,
    Position,
    GapAhead,
    GapBehind,
    SessionType,
    OpponentCount,
    RaceContext,
    HistoricalComparison
}

public sealed record RaceAwarenessRoutingResult(
    RaceAwarenessSubtopic Subtopic,
    IReadOnlyList<string> SelectedTelemetryFields,
    IReadOnlyList<string> MissingTelemetryFields,
    string? FallbackReason,
    string? SelectedField = null,
    string? SelectedValue = null)
{
    public static RaceAwarenessRoutingResult ForSubtopic(
        RaceAwarenessSubtopic subtopic,
        LiveRaceContext? raceContext,
        string? fallbackReason = null)
    {
        var selected = new List<string>();
        var missing = new List<string>();

        foreach (var field in FieldsForSubtopic(subtopic))
        {
            if (HasField(raceContext, field))
            {
                selected.Add(field);
            }
            else
            {
                missing.Add(field);
            }
        }

        var (selectedField, selectedValue) = ResolveSelectedField(subtopic, raceContext);
        return new RaceAwarenessRoutingResult(
            subtopic,
            selected,
            missing,
            fallbackReason,
            selectedField,
            selectedValue);
    }

    private static IReadOnlyList<string> FieldsForSubtopic(RaceAwarenessSubtopic subtopic) =>
        subtopic switch
        {
            RaceAwarenessSubtopic.TrackIdentity => ["track_name", "circuit_id", "provider_diag.pm_last_track_id"],
            RaceAwarenessSubtopic.CarIdentity => ["car_name", "provider_diag.pm_last_car_id"],
            RaceAwarenessSubtopic.Position => ["position", "total_cars"],
            RaceAwarenessSubtopic.GapAhead => ["gap_ahead_s", "car_ahead"],
            RaceAwarenessSubtopic.GapBehind => ["gap_behind_s", "car_behind"],
            RaceAwarenessSubtopic.SessionType => ["session_type"],
            RaceAwarenessSubtopic.OpponentCount => ["total_cars", "position"],
            RaceAwarenessSubtopic.RaceContext =>
            [
                "track_name",
                "car_name",
                "session_type",
                "position",
                "total_cars",
                "gap_ahead_s",
                "gap_behind_s"
            ],
            RaceAwarenessSubtopic.HistoricalComparison => [],
            _ => []
        };

    private static (string? Field, string? Value) ResolveSelectedField(
        RaceAwarenessSubtopic subtopic,
        LiveRaceContext? raceContext) =>
        raceContext switch
        {
            null => (null, null),
            var race when subtopic == RaceAwarenessSubtopic.TrackIdentity =>
                !string.IsNullOrWhiteSpace(race.TrackName)
                    ? ("track_name", race.TrackName.Trim())
                    : !string.IsNullOrWhiteSpace(race.CircuitId)
                        ? ("circuit_id", race.CircuitId.Trim())
                        : (null, null),
            var race when subtopic == RaceAwarenessSubtopic.CarIdentity =>
                !string.IsNullOrWhiteSpace(race.CarName)
                    ? ("car_name", race.CarName.Trim())
                    : (null, null),
            _ => (null, null)
        };

    private static bool HasField(LiveRaceContext? raceContext, string field) =>
        raceContext switch
        {
            null => false,
            var race => field switch
            {
                "track_name" => !string.IsNullOrWhiteSpace(race.TrackName),
                "circuit_id" => !string.IsNullOrWhiteSpace(race.CircuitId),
                "provider_diag.pm_last_track_id" => !string.IsNullOrWhiteSpace(race.TrackName) || !string.IsNullOrWhiteSpace(race.CircuitId),
                "car_name" => !string.IsNullOrWhiteSpace(race.CarName),
                "provider_diag.pm_last_car_id" => !string.IsNullOrWhiteSpace(race.CarName),
                "session_type" => !string.IsNullOrWhiteSpace(race.SessionType),
                "position" => race.Position.HasValue,
                "total_cars" => race.TotalCars.HasValue,
                "gap_ahead_s" => race.GapAheadSeconds.HasValue,
                "gap_behind_s" => race.GapBehindSeconds.HasValue,
                "car_ahead" => !string.IsNullOrWhiteSpace(race.CarAhead),
                "car_behind" => !string.IsNullOrWhiteSpace(race.CarBehind),
                _ => false
            }
        };
}
