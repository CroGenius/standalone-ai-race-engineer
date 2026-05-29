using System.Globalization;
using System.Text;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Strategy;

public static class StrategyKnowledgeService
{
    public static StrategyKnowledgeRecommendation Build(StrategyKnowledgeInput input) =>
        Build(input, query: null);

    public static StrategyKnowledgeRecommendation Build(StrategyKnowledgeInput input, string? query)
    {
        var track = input.TrackName?.Trim();
        if (string.IsNullOrWhiteSpace(track))
        {
            return StrategyKnowledgeRecommendation.Unavailable("unknown", input.CarName, input.CarClass, "Track is unavailable.");
        }

        var targetLaps = input.RequestedLapCount
            ?? StrategyLapCountParser.Parse(query, null, input.PrepPlan)
            ?? input.RaceLapCount;
        var (fuelPerLap, fuelSource, classBaselineNote) = ResolveFuelPerLap(input);
        var baseline = ResolveClassBaseline(input.CarClass, input.CarName);
        var overallSource = fuelSource;
        if (overallSource == StrategyKnowledgeDataSource.Unavailable && baseline is not null)
        {
            overallSource = StrategyKnowledgeDataSource.GenericClass;
        }

        var safetyMarginLaps = input.FuelSafetyMarginLaps > 0 ? input.FuelSafetyMarginLaps : 1.0;
        var safetyMarginLiters = fuelPerLap is { } burn
            ? (double?)(burn * safetyMarginLaps)
            : baseline is not null
                ? baseline.ExpectedFuelPerLap * baseline.FuelSafetyMarginLaps
                : null;
        var recommendedFuel = fuelPerLap is { } perLap && targetLaps is { } laps
            ? (double?)(perLap * laps + (safetyMarginLiters ?? 0))
            : null;

        var liveFuelNote = BuildLiveFuelNote(input, fuelPerLap, targetLaps, safetyMarginLiters);
        var fuelForLapsNote = BuildFuelForLapsNote(targetLaps, fuelPerLap, safetyMarginLiters, fuelSource, classBaselineNote);

        var pitWindow = input.Strategy?.Pit.EstimatedPitWindowStartLap is { } start && input.Strategy.Pit.EstimatedPitWindowEndLap is { } end
            ? $"Estimated pit window laps {start}-{end} from live strategy."
            : baseline?.PitWindowEstimate;
        var tyreWarmup = input.TyreIntelligence is { HasReliableData: true } tyre && !string.IsNullOrWhiteSpace(tyre.TyreConditionMessage)
            ? tyre.TyreConditionMessage
            : input.PreviousSessionMemory?.TyreWarmupNotes ?? input.TrackMemory?.TyreWarmupNotes ?? baseline?.TyreWarmupEstimate;
        var tyreRisk = input.TyreIntelligence is { HasReliableData: true } tyreIntel && !string.IsNullOrWhiteSpace(tyreIntel.OverheatingRisk)
            ? tyreIntel.OverheatingRisk
            : input.PreviousSessionMemory?.TyreDegradationNotes ?? baseline?.TyreDegradationRisk;
        var setupNotes = CollectSetupNotes(input, baseline);
        var drivingPriorities = CollectDrivingPriorities(input, baseline);

        return new StrategyKnowledgeRecommendation(
            track,
            input.CarName,
            input.CarClass,
            input.SessionType,
            targetLaps,
            fuelPerLap,
            recommendedFuel,
            safetyMarginLiters,
            tyreWarmup,
            tyreRisk,
            pitWindow,
            baseline?.OvertakingDifficulty,
            baseline?.BrakingStress ?? input.TrackGuide?.MajorBrakingZones.FirstOrDefault(),
            baseline?.TractionStress ?? input.TrackGuide?.TractionZones.FirstOrDefault(),
            setupNotes,
            drivingPriorities,
            fuelSource,
            overallSource,
            BuildConfidenceLabel(fuelSource, overallSource),
            StrategyKnowledgeFormatting.FormatSourceLabel(overallSource),
            classBaselineNote,
            liveFuelNote,
            fuelForLapsNote);
    }

    public static StrategyKnowledgeInput FromCoachContext(
        CoachContext? context,
        SessionState session,
        string? query = null,
        int? requestedLaps = null)
    {
        var race = context?.RaceContext;
        return new StrategyKnowledgeInput(
            race?.TrackName ?? context?.RacePrepPlan?.Track,
            race?.CarName ?? context?.RacePrepPlan?.Car,
            race?.CarClass,
            race?.SessionType ?? context?.RacePrepPlan?.SessionType,
            race?.TotalLaps ?? race?.LapsRemaining,
            session,
            context?.Analytics,
            context?.Strategy,
            context?.TyreIntelligence,
            context?.TrackMemory,
            context?.PreviousStoredSessionMemory,
            context?.CachedTrackGuide,
            context?.RacePrepPlan,
            StrategyPreferencesRecord.Default.FuelSafetyMarginLaps,
            requestedLaps ?? StrategyLapCountParser.Parse(query, race, context?.RacePrepPlan));
    }

    public static string BuildCoachAnswer(StrategyKnowledgeRecommendation recommendation, string query)
    {
        var text = query.ToLowerInvariant();
        var parts = new List<string>
        {
            $"Strategy knowledge for {recommendation.TrackName} ({recommendation.SourceLabel}, confidence {recommendation.ConfidenceLabel})."
        };

        if (!string.IsNullOrWhiteSpace(recommendation.ClassBaselineNote))
        {
            parts.Add(recommendation.ClassBaselineNote);
        }

        if (IsFuelPlanningQuery(text))
        {
            if (!string.IsNullOrWhiteSpace(recommendation.LiveFuelNote))
            {
                parts.Add(recommendation.LiveFuelNote);
            }
            else if (!string.IsNullOrWhiteSpace(recommendation.FuelForLapsNote))
            {
                parts.Add(recommendation.FuelForLapsNote);
            }
            else
            {
                parts.Add("Fuel planning is unavailable until track, car, and lap count are known.");
            }

            if (recommendation.ExpectedFuelPerLap is { } burn)
            {
                parts.Add($"Expected fuel per lap: {StrategyKnowledgeFormatting.FormatLiters(burn)} ({StrategyKnowledgeFormatting.FormatSourceLabel(recommendation.FuelPerLapSource)}).");
            }

            if (recommendation.RecommendedStartingFuelLiters is { } starting)
            {
                parts.Add($"Recommended starting fuel: {StrategyKnowledgeFormatting.FormatLiters(starting)}.");
            }

            return string.Join(" ", parts);
        }

        if (IsTyrePushQuery(text))
        {
            if (!string.IsNullOrWhiteSpace(recommendation.TyreDegradationRisk))
            {
                parts.Add($"Tyre degradation risk: {recommendation.TyreDegradationRisk}");
            }

            if (!string.IsNullOrWhiteSpace(recommendation.TyreWarmupEstimate))
            {
                parts.Add($"Tyre warmup: {recommendation.TyreWarmupEstimate}");
            }

            if (string.IsNullOrWhiteSpace(recommendation.TyreDegradationRisk) && string.IsNullOrWhiteSpace(recommendation.TyreWarmupEstimate))
            {
                parts.Add("Tyre push guidance is unavailable for this track and car combination.");
            }

            return string.Join(" ", parts);
        }

        if (recommendation.PitWindowEstimate is not null)
        {
            parts.Add($"Pit window: {recommendation.PitWindowEstimate}");
        }

        if (recommendation.DrivingPriorities.Count > 0)
        {
            parts.Add($"Driving priorities: {string.Join("; ", recommendation.DrivingPriorities)}.");
        }

        if (recommendation.SetupNotes.Count > 0)
        {
            parts.Add($"Setup notes: {string.Join("; ", recommendation.SetupNotes)}.");
        }

        if (recommendation.ExpectedFuelPerLap is { } fuelPerLap)
        {
            parts.Add($"Expected fuel per lap: {StrategyKnowledgeFormatting.FormatLiters(fuelPerLap)} ({StrategyKnowledgeFormatting.FormatSourceLabel(recommendation.FuelPerLapSource)}).");
        }

        return string.Join(" ", parts);
    }

    private static bool IsFuelPlanningQuery(string text) =>
        text.Contains("fuel should i take", StringComparison.Ordinal)
            || text.Contains("fuel for", StringComparison.Ordinal)
            || text.Contains("goriva za", StringComparison.Ordinal)
            || text.Contains("goriva trebam", StringComparison.Ordinal)
            || text.Contains("starting fuel", StringComparison.Ordinal);

    private static bool IsTyrePushQuery(string text) =>
        text.Contains("push tyre", StringComparison.Ordinal)
            || text.Contains("push tire", StringComparison.Ordinal)
            || text.Contains("push gume", StringComparison.Ordinal)
            || text.Contains("gume", StringComparison.Ordinal) && text.Contains("push", StringComparison.Ordinal);

    private static (double? FuelPerLap, StrategyKnowledgeDataSource Source, string? ClassBaselineNote) ResolveFuelPerLap(
        StrategyKnowledgeInput input)
    {
        var live = input.Session.FuelUsedPerLap ?? input.Analytics?.FuelTrend.FuelPerLap;
        if (live is > 0)
        {
            return (live, StrategyKnowledgeDataSource.LiveTelemetry, null);
        }

        if (input.TrackMemory?.FuelUsedPerLap is { } storedCarFuel && storedCarFuel > 0)
        {
            return (storedCarFuel, StrategyKnowledgeDataSource.StoredCar, null);
        }

        if (input.PreviousSessionMemory?.FuelUsedPerLap is { } previousFuel && previousFuel > 0)
        {
            return (previousFuel, StrategyKnowledgeDataSource.StoredCar, null);
        }

        if (StrategyClassBaselines.TryGet(input.CarClass ?? input.CarName, out var classBaseline))
        {
            return (
                classBaseline.ExpectedFuelPerLap,
                StrategyKnowledgeDataSource.GenericClass,
                $"Using {classBaseline.ClassName} baseline, not exact car history.");
        }

        if (input.TrackGuide?.FuelStrategyNotes.Count > 0)
        {
            return (null, StrategyKnowledgeDataSource.CachedTrackGuide, null);
        }

        return (null, StrategyKnowledgeDataSource.Unavailable, null);
    }

    private static StrategyClassBaseline? ResolveClassBaseline(string? carClass, string? carName) =>
        StrategyClassBaselines.TryGet(carClass ?? carName, out var baseline) ? baseline : null;

    private static string BuildConfidenceLabel(
        StrategyKnowledgeDataSource fuelSource,
        StrategyKnowledgeDataSource overallSource) =>
        fuelSource switch
        {
            StrategyKnowledgeDataSource.LiveTelemetry => "High",
            StrategyKnowledgeDataSource.StoredCar => "Medium",
            StrategyKnowledgeDataSource.StoredClass => "Medium",
            StrategyKnowledgeDataSource.CachedTrackGuide => "Low",
            StrategyKnowledgeDataSource.GenericClass => "Low",
            _ => "Unavailable"
        };

    private static string? BuildLiveFuelNote(
        StrategyKnowledgeInput input,
        double? fuelPerLap,
        int? targetLaps,
        double? safetyMarginLiters)
    {
        var liveBurn = input.Session.FuelUsedPerLap ?? input.Analytics?.FuelTrend.FuelPerLap;
        if (liveBurn is not > 0)
        {
            return "Need at least one clean lap for exact fuel burn.";
        }

        if (fuelPerLap is not > 0 || targetLaps is not > 0)
        {
            return null;
        }

        var total = fuelPerLap.Value * targetLaps.Value + (safetyMarginLiters ?? 0);
        return $"Based on your current burn, {targetLaps.Value} laps need {StrategyKnowledgeFormatting.FormatLiters(total)} plus margin.";
    }

    private static string? BuildFuelForLapsNote(
        int? targetLaps,
        double? fuelPerLap,
        double? safetyMarginLiters,
        StrategyKnowledgeDataSource fuelSource,
        string? classBaselineNote)
    {
        if (targetLaps is not > 0)
        {
            return null;
        }

        if (fuelPerLap is not > 0)
        {
            return classBaselineNote ?? "Fuel per lap is unavailable for this track and car combination.";
        }

        var total = fuelPerLap.Value * targetLaps.Value + (safetyMarginLiters ?? 0);
        var source = StrategyKnowledgeFormatting.FormatSourceLabel(fuelSource);
        var note = $"Strategy estimate ({source}): {targetLaps.Value} laps need about {StrategyKnowledgeFormatting.FormatLiters(total)} including margin.";
        return classBaselineNote is null ? note : $"{classBaselineNote} {note}";
    }

    private static IReadOnlyList<string> CollectSetupNotes(StrategyKnowledgeInput input, StrategyClassBaseline? baseline)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.Strategy?.Summary))
        {
            notes.Add(input.Strategy.Summary);
        }

        if (!string.IsNullOrWhiteSpace(input.PrepPlan?.SetupNotes))
        {
            notes.Add(input.PrepPlan.SetupNotes);
        }

        notes.AddRange(input.TrackGuide?.FuelStrategyNotes ?? []);
        notes.AddRange(baseline?.SetupNotes ?? []);
        return notes.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
    }

    private static IReadOnlyList<string> CollectDrivingPriorities(StrategyKnowledgeInput input, StrategyClassBaseline? baseline)
    {
        var priorities = new List<string>();
        priorities.AddRange(input.TrackGuide?.SetupPriorities ?? []);
        if (priorities.Count == 0)
        {
            priorities.AddRange(input.TrackGuide?.SectorNotes.Take(2) ?? []);
        }
        priorities.AddRange(baseline?.DrivingPriorities ?? []);
        priorities.AddRange(input.PreviousSessionMemory?.ImprovementTargets.Take(2) ?? []);
        return priorities.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
    }
}

public static class StrategyKnowledgeEvidenceFormatter
{
    public static string BuildSummary(StrategyKnowledgeRecommendation recommendation)
    {
        var builder = new StringBuilder();
        builder.Append($"Strategy knowledge ({recommendation.SourceLabel}, confidence {recommendation.ConfidenceLabel}).");
        if (recommendation.ExpectedFuelPerLap is { } burn)
        {
            builder.Append($" Expected fuel per lap {burn.ToString("0.00", CultureInfo.InvariantCulture)} L.");
        }

        if (recommendation.RecommendedStartingFuelLiters is { } starting)
        {
            builder.Append($" Recommended starting fuel {starting.ToString("0.0", CultureInfo.InvariantCulture)} L.");
        }

        if (!string.IsNullOrWhiteSpace(recommendation.PitWindowEstimate))
        {
            builder.Append($" {recommendation.PitWindowEstimate}");
        }

        return builder.ToString().Trim();
    }
}
