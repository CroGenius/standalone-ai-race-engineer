using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;

namespace RaceEngineer.Core.Knowledge;

public static class TrackCarKnowledgeService
{
    public static TrackCarKnowledgeRecommendation Build(TrackCarKnowledgeInput input) =>
        Build(input, query: null);

    public static TrackCarKnowledgeRecommendation Build(TrackCarKnowledgeInput input, string? query)
    {
        var track = TrackGuideCatalogIdentity.CanonicalName(input.TrackName);
        if (string.IsNullOrWhiteSpace(track))
        {
            return TrackCarKnowledgeRecommendation.Unavailable(
                input.TrackName,
                input.CarName,
                input.CarClass,
                "Track is unavailable.");
        }

        var carClass = TrackCarKnowledgeCatalog.NormalizeCarClass(input.CarClass ?? input.CarName);
        if (!TrackCarKnowledgeCatalog.TryResolve(track, carClass ?? input.CarClass ?? input.CarName, out var knowledge))
        {
            return TrackCarKnowledgeRecommendation.Unavailable(
                track,
                input.CarName,
                carClass,
                $"No stored track-car knowledge is available for {track}.");
        }

        var targetLaps = input.RequestedLapCount
            ?? StrategyLapCountParser.Parse(query, null, null);
        var sessionFuel = input.Session.FuelUsedPerLap ?? input.Analytics?.FuelTrend.FuelPerLap;
        double? fuelPerLap = null;
        TrackCarKnowledgeDataSource fuelSource;
        string? liveFuelNote = null;
        string? knowledgeFuelNote = null;

        if (sessionFuel is > 0)
        {
            fuelPerLap = sessionFuel;
            fuelSource = TrackCarKnowledgeDataSource.LiveTelemetry;
        }
        else if (input.TrackMemory?.FuelUsedPerLap is { } storedCarFuel && storedCarFuel > 0)
        {
            fuelPerLap = storedCarFuel;
            fuelSource = TrackCarKnowledgeDataSource.StoredKnowledge;
        }
        else if (input.PreviousSessionMemory?.FuelUsedPerLap is { } previousFuel && previousFuel > 0)
        {
            fuelPerLap = previousFuel;
            fuelSource = TrackCarKnowledgeDataSource.StoredKnowledge;
        }
        else if (knowledge.BaselineFuelPerLapLiters is > 0)
        {
            fuelPerLap = knowledge.BaselineFuelPerLapLiters;
            fuelSource = TrackCarKnowledgeDataSource.StoredKnowledge;
        }
        else
        {
            fuelSource = TrackCarKnowledgeDataSource.Unavailable;
        }

        var baselineFuel = knowledge.BaselineFuelPerLapLiters;
        var marginLaps = knowledge.FuelSafetyMarginLaps > 0 ? knowledge.FuelSafetyMarginLaps : 1.0;
        var marginLiters = fuelPerLap is > 0 ? fuelPerLap * marginLaps : null;
        var recommendedFuel = fuelPerLap is > 0 && targetLaps is > 0
            ? fuelPerLap * targetLaps + (marginLiters ?? 0)
            : null;

        if (fuelSource == TrackCarKnowledgeDataSource.LiveTelemetry && sessionFuel is > 0)
        {
            liveFuelNote = targetLaps is > 0
                ? $"Based on live telemetry ({FormatLiters(sessionFuel)}/lap), {targetLaps} laps need about {FormatLiters(recommendedFuel)} including margin."
                : $"Live telemetry shows about {FormatLiters(sessionFuel)} per lap.";
        }
        else if (fuelSource == TrackCarKnowledgeDataSource.StoredKnowledge && fuelPerLap is > 0)
        {
            knowledgeFuelNote = targetLaps is > 0
                ? $"Stored knowledge: {track} {carClass ?? "cars"} typically use about {FormatLiters(fuelPerLap)}/lap. For {targetLaps} laps, plan about {FormatLiters(fuelPerLap * targetLaps + (marginLiters ?? 0))} including margin."
                : $"Stored knowledge: {track} {carClass ?? "cars"} typically use about {FormatLiters(fuelPerLap)}/lap.";
        }
        else if (baselineFuel is > 0)
        {
            knowledgeFuelNote = targetLaps is > 0
                ? $"Stored knowledge: {track} {carClass ?? "cars"} typically use about {FormatLiters(baselineFuel)}/lap. For {targetLaps} laps, plan about {FormatLiters(baselineFuel * targetLaps + (marginLiters ?? 0))} including margin."
                : $"Stored knowledge: {track} {carClass ?? "cars"} typically use about {FormatLiters(baselineFuel)}/lap.";
        }
        else
        {
            knowledgeFuelNote = knowledge.FuelUsageExpectation;
        }

        return new TrackCarKnowledgeRecommendation(
            track,
            input.CarName,
            carClass,
            knowledge.FuelUsageExpectation,
            knowledge.TyreWearExpectation,
            knowledge.TyreWarmupExpectation,
            knowledge.BrakeDemand,
            knowledge.TopSpeedSensitivity,
            knowledge.DownforceSensitivity,
            knowledge.OvertakingDifficulty,
            knowledge.PitStrategyNotes,
            knowledge.SetupPriorities,
            knowledge.OvertakingZones,
            targetLaps,
            sessionFuel,
            baselineFuel,
            recommendedFuel,
            fuelSource,
            TrackCarKnowledgeDataSource.StoredKnowledge,
            BuildConfidenceLabel(fuelSource),
            FormatSourceLabel(fuelSource),
            liveFuelNote,
            knowledgeFuelNote,
            BuildKnowledgeSummary(track, carClass, knowledge, fuelSource, sessionFuel, baselineFuel));
    }

    public static TrackCarKnowledgeInput FromCoachContext(
        CoachContext? context,
        SessionState session,
        string? query = null,
        int? requestedLaps = null)
    {
        var race = context?.RaceContext;
        return new TrackCarKnowledgeInput(
            context?.ReviewSessionTrack
                ?? race?.TrackName
                ?? context?.RacePrepPlan?.Track,
            race?.CarName ?? context?.RacePrepPlan?.Car,
            race?.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(context?.RacePrepPlan?.Car),
            session,
            context?.Analytics,
            context?.TrackMemory,
            context?.PreviousStoredSessionMemory,
            requestedLaps ?? StrategyLapCountParser.Parse(query, race, context?.RacePrepPlan));
    }

    public static string BuildCoachAnswer(
        TrackCarKnowledgeRecommendation recommendation,
        string query,
        SessionState? session = null)
    {
        if (!recommendation.IsAvailable)
        {
            return recommendation.KnowledgeSummary;
        }

        var text = query.ToLowerInvariant();
        var prefix = $"Track-car knowledge for {recommendation.TrackName}" +
                       (string.IsNullOrWhiteSpace(recommendation.CarClass) ? string.Empty : $" {recommendation.CarClass}") +
                       $" ({recommendation.SourceLabel}, confidence {recommendation.ConfidenceLabel}).";

        if (IsFuelPlanningQuery(text))
        {
            var parts = new List<string> { prefix };
            if (!string.IsNullOrWhiteSpace(recommendation.LiveFuelNote))
            {
                parts.Add(recommendation.LiveFuelNote);
            }
            else if (!string.IsNullOrWhiteSpace(recommendation.KnowledgeFuelNote))
            {
                parts.Add(recommendation.KnowledgeFuelNote);
            }
            else
            {
                parts.Add(recommendation.FuelUsageExpectation);
            }

            return string.Join(" ", parts);
        }

        if (IsFuelUsageHighQuery(text))
        {
            return $"{prefix} {recommendation.FuelUsageExpectation}";
        }

        if (IsTyreWatchQuery(text) || IsTyreDemandQuery(text))
        {
            return $"{prefix} Tyre wear: {recommendation.TyreWearExpectation} Warmup: {recommendation.TyreWarmupExpectation}";
        }

        if (IsBrakeDemandQuery(text))
        {
            var demand = ExtractDemandLevel(recommendation.BrakeDemand);
            var carLabel = recommendation.CarClass ?? "this class";
            return $"Stored knowledge: {recommendation.TrackName} is typically {demand} brake demand for {carLabel} cars.";
        }

        if (IsSetupQuery(text))
        {
            return recommendation.SetupPriorities.Count == 0
                ? $"{prefix} Setup priorities are unavailable for this combination."
                : $"{prefix} Setup focus: {string.Join("; ", recommendation.SetupPriorities)}.";
        }

        if (IsOvertakingQuery(text))
        {
            return recommendation.OvertakingZones.Count == 0
                ? $"{prefix} Overtaking difficulty: {recommendation.OvertakingDifficulty}"
                : $"{prefix} Overtaking difficulty: {recommendation.OvertakingDifficulty} Key zones: {string.Join("; ", recommendation.OvertakingZones)}.";
        }

        return $"{prefix} {recommendation.KnowledgeSummary}";
    }

    public static IReadOnlyList<EngineerAiFact> BuildAiFacts(TrackCarKnowledgeRecommendation recommendation)
    {
        if (!recommendation.IsAvailable)
        {
            return [];
        }

        var facts = new List<EngineerAiFact>
        {
            new(
                "TrackCarKnowledge",
                "Track-car knowledge summary",
                recommendation.KnowledgeSummary,
                recommendation.FuelSource == TrackCarKnowledgeDataSource.LiveTelemetry ? 0.92 : 0.84,
                recommendation.SourceLabel,
                null)
        };

        if (recommendation.BaselineFuelPerLapLiters is { } baseline)
        {
            facts.Add(new EngineerAiFact(
                "TrackCarKnowledge",
                "Baseline fuel expectation",
                $"Stored knowledge baseline {baseline.ToString("0.0", CultureInfo.InvariantCulture)} L/lap for {recommendation.TrackName} {recommendation.CarClass}.",
                0.84,
                "stored knowledge",
                null));
        }

        if (recommendation.LiveFuelPerLapLiters is { } live)
        {
            facts.Add(new EngineerAiFact(
                "TrackCarKnowledge",
                "Live fuel burn",
                $"Live telemetry fuel burn {live.ToString("0.00", CultureInfo.InvariantCulture)} L/lap.",
                0.92,
                "live telemetry",
                null));
        }

        facts.Add(new EngineerAiFact(
            "TrackCarKnowledge",
            "Brake demand",
            recommendation.BrakeDemand,
            0.82,
            "stored knowledge",
            null));
        facts.Add(new EngineerAiFact(
            "TrackCarKnowledge",
            "Tyre expectations",
            $"{recommendation.TyreWearExpectation} Warmup: {recommendation.TyreWarmupExpectation}",
            0.82,
            "stored knowledge",
            null));

        if (recommendation.SetupPriorities.Count > 0)
        {
            facts.Add(new EngineerAiFact(
                "TrackCarKnowledge",
                "Setup priorities",
                string.Join("; ", recommendation.SetupPriorities),
                0.8,
                "stored knowledge",
                null));
        }

        return facts;
    }

    public static string BuildEvidenceSummary(TrackCarKnowledgeRecommendation recommendation)
    {
        var builder = new StringBuilder();
        builder.Append($"Track-car knowledge ({recommendation.SourceLabel}, confidence {recommendation.ConfidenceLabel}).");
        if (recommendation.LiveFuelPerLapLiters is { } live)
        {
            builder.Append($" Live fuel {live.ToString("0.00", CultureInfo.InvariantCulture)} L/lap.");
        }
        else if (recommendation.BaselineFuelPerLapLiters is { } baseline)
        {
            builder.Append($" Baseline fuel {baseline.ToString("0.0", CultureInfo.InvariantCulture)} L/lap.");
        }

        builder.Append($" Brake demand: {Shorten(recommendation.BrakeDemand)}.");
        builder.Append($" Tyre wear: {Shorten(recommendation.TyreWearExpectation)}.");
        return builder.ToString().Trim();
    }

    private static string BuildKnowledgeSummary(
        string track,
        string? carClass,
        TrackCarKnowledge knowledge,
        TrackCarKnowledgeDataSource fuelSource,
        double? liveFuel,
        double? baselineFuel)
    {
        var fuelPart = fuelSource == TrackCarKnowledgeDataSource.LiveTelemetry && liveFuel is > 0
            ? $"Live fuel burn about {FormatLiters(liveFuel)}/lap."
            : baselineFuel is > 0
                ? $"Stored knowledge fuel baseline about {FormatLiters(baselineFuel)}/lap."
                : knowledge.FuelUsageExpectation;
        return $"{track} {carClass ?? "baseline"}: {fuelPart} Brake demand: {Shorten(knowledge.BrakeDemand)}. Tyre wear: {Shorten(knowledge.TyreWearExpectation)}. Setup focus: {string.Join("; ", knowledge.SetupPriorities.Take(2))}.";
    }

    private static string BuildConfidenceLabel(TrackCarKnowledgeDataSource fuelSource) =>
        fuelSource switch
        {
            TrackCarKnowledgeDataSource.LiveTelemetry => "High",
            TrackCarKnowledgeDataSource.StoredKnowledge => "Medium",
            _ => "Low"
        };

    private static string FormatSourceLabel(TrackCarKnowledgeDataSource source) =>
        source switch
        {
            TrackCarKnowledgeDataSource.LiveTelemetry => "live telemetry",
            TrackCarKnowledgeDataSource.StoredKnowledge => "stored knowledge",
            _ => "unavailable"
        };

    private static string FormatLiters(double? value) =>
        value is { } liters
            ? $"{liters.ToString("0.0", CultureInfo.InvariantCulture)} L"
            : "unavailable";

    private static string Shorten(string value) =>
        value.Length <= 120 ? value : value[..117] + "...";

    private static string ExtractDemandLevel(string demand)
    {
        if (demand.StartsWith("Very high", StringComparison.OrdinalIgnoreCase))
        {
            return "very high";
        }

        if (demand.StartsWith("Medium-high", StringComparison.OrdinalIgnoreCase))
        {
            return "medium-high";
        }

        if (demand.StartsWith("Medium", StringComparison.OrdinalIgnoreCase))
        {
            return "medium";
        }

        if (demand.StartsWith("High", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        if (demand.StartsWith("Low", StringComparison.OrdinalIgnoreCase))
        {
            return "low";
        }

        return "variable";
    }

    private static bool IsFuelPlanningQuery(string text) =>
        text.Contains("how much fuel", StringComparison.Ordinal)
            || text.Contains("fuel for", StringComparison.Ordinal)
            || text.Contains("fuel do i need", StringComparison.Ordinal)
            || text.Contains("goriva za", StringComparison.Ordinal)
            || text.Contains("goriva trebam", StringComparison.Ordinal);

    private static bool IsFuelUsageHighQuery(string text) =>
        text.Contains("fuel usage", StringComparison.Ordinal)
            || text.Contains("fuel use high", StringComparison.Ordinal)
            || text.Contains("is fuel", StringComparison.Ordinal) && text.Contains("high", StringComparison.Ordinal);

    private static bool IsTyreWatchQuery(string text) =>
        text.Contains("watch with tyre", StringComparison.Ordinal)
            || text.Contains("watch with tire", StringComparison.Ordinal)
            || text.Contains("what should i watch", StringComparison.Ordinal) && (text.Contains("tyre", StringComparison.Ordinal) || text.Contains("tire", StringComparison.Ordinal));

    private static bool IsTyreDemandQuery(string text) =>
        text.Contains("hard on tyre", StringComparison.Ordinal)
            || text.Contains("hard on tire", StringComparison.Ordinal)
            || text.Contains("tyre wear", StringComparison.Ordinal)
            || text.Contains("tire wear", StringComparison.Ordinal);

    private static bool IsBrakeDemandQuery(string text) =>
        text.Contains("hard on brake", StringComparison.Ordinal)
            || text.Contains("brake demand", StringComparison.Ordinal);

    private static bool IsSetupQuery(string text) =>
        text.Contains("setup matter", StringComparison.Ordinal)
            || text.Contains("what setup", StringComparison.Ordinal)
            || text.Contains("setup focus", StringComparison.Ordinal)
            || text.Contains("setup should", StringComparison.Ordinal);

    private static bool IsOvertakingQuery(string text) =>
        text.Contains("where can i overtake", StringComparison.Ordinal)
            || text.Contains("overtake on", StringComparison.Ordinal)
            || text.Contains("pass on this track", StringComparison.Ordinal);
}

public static class TrackCarKnowledgeEvidenceFormatter
{
    public static string BuildSummary(TrackCarKnowledgeRecommendation recommendation) =>
        TrackCarKnowledgeService.BuildEvidenceSummary(recommendation);
}
