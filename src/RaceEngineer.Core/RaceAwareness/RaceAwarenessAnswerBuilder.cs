using System.Globalization;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.RaceAwareness;

public static class RaceAwarenessAnswerBuilder
{
    public const string TrackUnavailableMessage = "Track name is unavailable from telemetry.";
    public const string CarUnavailableMessage = "Car name is unavailable from telemetry.";
    public const string PositionUnavailableMessage = "Position data is unavailable.";

    public static RaceAwarenessAnswer Build(
        RaceAwarenessSubtopic subtopic,
        SessionState session,
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        string? prepTrack = null,
        string? prepCar = null,
        OpponentIntelligenceRecommendation? opponentIntelligence = null,
        TelemetryProviderCapabilities? telemetryProviderCapabilities = null,
        TelemetryProviderStatus? telemetryProviderStatus = null,
        string? telemetryProviderDiagnostics = null)
    {
        if (subtopic is RaceAwarenessSubtopic.CatchingAhead
            or RaceAwarenessSubtopic.PullingAway
            or RaceAwarenessSubtopic.AttackOpportunity
            or RaceAwarenessSubtopic.DefendRecommendation
            or RaceAwarenessSubtopic.RaceSituation
            or RaceAwarenessSubtopic.OpponentIdentity)
        {
            return OpponentIntelligenceAnswerBuilder.Build(
                subtopic,
                opponentIntelligence,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics);
        }

        return subtopic switch
        {
            RaceAwarenessSubtopic.TrackIdentity => BuildTrackIdentity(
                raceContext,
                routing,
                prepTrack,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.CarIdentity => BuildCarIdentity(
                raceContext,
                routing,
                prepCar,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.Position => BuildPosition(
                session,
                raceContext,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.GapAhead => BuildGapAhead(
                raceContext,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.GapBehind => BuildGapBehind(
                raceContext,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.SessionType => BuildSessionType(
                raceContext,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.OpponentCount => BuildOpponentCount(
                raceContext,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            _ => BuildRaceContext(
                raceContext,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics)
        };
    }

    public static RaceAwarenessAnswer BuildTrackIdentity(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        string? prepTrack = null,
        TelemetryProviderCapabilities? telemetryProviderCapabilities = null,
        TelemetryProviderStatus? telemetryProviderStatus = null,
        string? telemetryProviderDiagnostics = null)
    {
        var track = FirstNonEmpty(raceContext?.TrackName, raceContext?.CircuitId, prepTrack);
        if (!string.IsNullOrWhiteSpace(track))
        {
            return new RaceAwarenessAnswer(
                $"You are on {track}.",
                [$"track: {track}"],
                routing with { FallbackReason = null });
        }

        return new RaceAwarenessAnswer(
            UnavailableTelemetryMessage(
                TrackUnavailableMessage,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    routing.MissingTelemetryFields.Count > 0
                        ? $"track_name and circuit_id missing ({string.Join(", ", routing.MissingTelemetryFields)})"
                        : "No track or circuit field is available from telemetry or prep.")
            });
    }

    public static RaceAwarenessAnswer BuildCarIdentity(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        string? prepCar = null,
        TelemetryProviderCapabilities? telemetryProviderCapabilities = null,
        TelemetryProviderStatus? telemetryProviderStatus = null,
        string? telemetryProviderDiagnostics = null)
    {
        var car = FirstNonEmpty(raceContext?.CarName, prepCar);
        if (!string.IsNullOrWhiteSpace(car))
        {
            return new RaceAwarenessAnswer(
                $"{car}.",
                [$"car: {car}"],
                routing with { FallbackReason = null });
        }

        return new RaceAwarenessAnswer(
            UnavailableTelemetryMessage(
                CarUnavailableMessage,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    routing.MissingTelemetryFields.Count > 0
                        ? $"car_name missing ({string.Join(", ", routing.MissingTelemetryFields)})"
                        : "No car field is available from telemetry or prep.")
            });
    }

    public static RaceAwarenessAnswer BuildPosition(
        SessionState session,
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities = null,
        TelemetryProviderStatus? telemetryProviderStatus = null,
        string? telemetryProviderDiagnostics = null)
    {
        if (raceContext?.Position is { } position)
        {
            var content = raceContext.TotalCars is { } total
                ? $"You are P{position} of {total}."
                : $"You are P{position}.";
            return new RaceAwarenessAnswer(content, [$"position: {position}"], routing with { FallbackReason = null });
        }

        var snapshotPosition = session.LatestSnapshot?.Race.Position;
        if (snapshotPosition is { } snapshotPos)
        {
            return new RaceAwarenessAnswer(
                $"You are P{snapshotPos}.",
                [$"position: {snapshotPos}"],
                routing with { FallbackReason = "Used snapshot race.position because live race context had no position." });
        }

        return new RaceAwarenessAnswer(
            UnavailableTelemetryMessage(
                PositionUnavailableMessage,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    "position and total_cars missing from telemetry.")
            });
    }

    private static RaceAwarenessAnswer BuildGapAhead(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        if (raceContext?.GapAheadSeconds is { } gapAhead)
        {
            var content = !string.IsNullOrWhiteSpace(raceContext.CarAhead)
                ? $"Gap ahead to {raceContext.CarAhead} is {FormatNumber(gapAhead, "0.000")}s."
                : $"Gap ahead is {FormatNumber(gapAhead, "0.000")}s.";
            return new RaceAwarenessAnswer(content, [$"gap ahead: {FormatNumber(gapAhead, "0.000")}s"], routing with { FallbackReason = null });
        }

        return new RaceAwarenessAnswer(
            TelemetryProviderCapabilityMessages.GapAheadUnavailable(
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderCapabilities is not null && !telemetryProviderCapabilities.OpponentGaps
                        ? "provider does not expose opponent gaps"
                        : "gap_ahead_s missing from telemetry.")
            });
    }

    private static RaceAwarenessAnswer BuildGapBehind(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        if (raceContext?.GapBehindSeconds is { } gapBehind)
        {
            var content = !string.IsNullOrWhiteSpace(raceContext.CarBehind)
                ? $"Gap behind to {raceContext.CarBehind} is {FormatNumber(gapBehind, "0.000")}s."
                : $"Gap behind is {FormatNumber(gapBehind, "0.000")}s.";
            return new RaceAwarenessAnswer(content, [$"gap behind: {FormatNumber(gapBehind, "0.000")}s"], routing with { FallbackReason = null });
        }

        return new RaceAwarenessAnswer(
            TelemetryProviderCapabilityMessages.GapBehindUnavailable(
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderCapabilities is not null && !telemetryProviderCapabilities.OpponentGaps
                        ? "provider does not expose opponent gaps"
                        : "gap_behind_s missing from telemetry.")
            });
    }

    private static RaceAwarenessAnswer BuildSessionType(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        if (!string.IsNullOrWhiteSpace(raceContext?.SessionType))
        {
            return new RaceAwarenessAnswer(
                $"Session type is {raceContext.SessionType}.",
                [$"session type: {raceContext.SessionType}"],
                routing with { FallbackReason = null });
        }

        return new RaceAwarenessAnswer(
            UnavailableTelemetryMessage(
                "Session type is unavailable from telemetry.",
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    "session_type missing from telemetry.")
            });
    }

    private static RaceAwarenessAnswer BuildOpponentCount(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        if (raceContext?.TotalCars is { } total)
        {
            var content = raceContext.Position is { } position
                ? $"There are {total} cars in session. You are P{position}."
                : $"There are {total} cars in session.";
            return new RaceAwarenessAnswer(content, [$"total cars: {total}"], routing with { FallbackReason = null });
        }

        return new RaceAwarenessAnswer(
            UnavailableTelemetryMessage(
                "Opponent count is unavailable from telemetry.",
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            [],
            routing with
            {
                FallbackReason = ProviderFallbackReason(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    "total_cars missing from telemetry.")
            });
    }

    private static RaceAwarenessAnswer BuildRaceContext(
        LiveRaceContext? raceContext,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        if (raceContext is null || raceContext.Confidence == RaceContextConfidence.Unavailable)
        {
            return new RaceAwarenessAnswer(
                UnavailableTelemetryMessage(
                    "Race context is unavailable.",
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderDiagnostics),
                [],
                routing with
                {
                    FallbackReason = ProviderFallbackReason(
                        telemetryProviderCapabilities,
                        telemetryProviderStatus,
                        raceContext?.Diagnostics.Summary ?? "No race awareness fields are available from telemetry or prep.")
                });
        }

        var parts = new List<string>();
        var evidence = new List<string>();

        if (!string.IsNullOrWhiteSpace(raceContext.TrackName))
        {
            parts.Add($"Track is {raceContext.TrackName}.");
            evidence.Add($"track: {raceContext.TrackName}");
        }

        if (!string.IsNullOrWhiteSpace(raceContext.CarName))
        {
            parts.Add($"Car is {raceContext.CarName}.");
            evidence.Add($"car: {raceContext.CarName}");
        }

        if (!string.IsNullOrWhiteSpace(raceContext.SessionType))
        {
            parts.Add($"Session type is {raceContext.SessionType}.");
            evidence.Add($"session type: {raceContext.SessionType}");
        }

        if (raceContext.Position is { } position)
        {
            parts.Add(raceContext.TotalCars is { } total
                ? $"You are P{position} of {total}."
                : $"You are P{position}.");
            evidence.Add($"position: {position}");
        }

        if (parts.Count == 0)
        {
            return new RaceAwarenessAnswer(
                "Race context is unavailable.",
                [],
                routing with { FallbackReason = "Race context packet parsed but no usable fields were present." });
        }

        return new RaceAwarenessAnswer(string.Join(" ", parts), evidence, routing with { FallbackReason = null });
    }

    private static string UnavailableTelemetryMessage(
        string defaultMessage,
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status,
        string? diagnostics)
    {
        var providerMessage = TelemetryProviderCapabilityMessages.ProviderUnavailable(capabilities, status, diagnostics);
        return string.IsNullOrWhiteSpace(providerMessage) ? defaultMessage : providerMessage;
    }

    private static string ProviderFallbackReason(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status,
        string defaultReason)
    {
        return TelemetryProviderCapabilityMessages.IsProviderUnavailable(status)
            ? "telemetry provider unavailable"
            : defaultReason;
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

    private static string FormatNumber(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);
}

public sealed record RaceAwarenessAnswer(
    string Content,
    IReadOnlyList<string> Evidence,
    RaceAwarenessRoutingResult Routing);
