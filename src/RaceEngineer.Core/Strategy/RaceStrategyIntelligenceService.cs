using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Strategy;

public static class RaceStrategyIntelligenceService
{
    private const double FuelStabilityToleranceRatio = 0.15;

    public static RaceStrategyIntelligenceRecommendation Build(RaceStrategyIntelligenceInput input) =>
        Build(input, query: null);

    public static RaceStrategyIntelligenceRecommendation Build(RaceStrategyIntelligenceInput input, string? query)
    {
        var requestedLaps = input.RequestedLapCount
            ?? StrategyLapCountParser.Parse(query, input.RaceContext, null);
        var fuelSamples = CollectFuelSamples(input.Session);
        var confidence = ResolveOverallConfidence(fuelSamples, input);
        var supportingNotes = BuildSupportingNotes(input);

        var fuel = BuildFuelRecommendation(input, fuelSamples, requestedLaps, confidence, supportingNotes);
        var tyre = BuildTyreRecommendation(input, confidence, supportingNotes);
        var pit = BuildPitRecommendation(input, fuel, tyre, confidence, supportingNotes);
        var stintRisk = BuildStintRisk(fuel, tyre, pit, confidence);
        var summary = BuildStrategySummary(fuel, tyre, pit, stintRisk);
        var evidence = BuildEvidenceLines(fuel, tyre, pit, supportingNotes);
        var hasData = fuel.HasData || tyre.HasData || pit.HasData;
        var availability = hasData ? "Available" : fuel.Availability;

        return new RaceStrategyIntelligenceRecommendation(
            hasData,
            availability,
            fuel,
            tyre,
            pit,
            stintRisk,
            summary,
            confidence,
            confidence.ToString(),
            evidence);
    }

    public static RaceStrategyIntelligenceRecommendation FromCoachContext(
        SessionState session,
        CoachContext? context,
        string? query = null)
    {
        if (context?.RaceStrategyIntelligence is { } cached)
        {
            return cached;
        }

        return Build(CreateInput(session, context, query), query);
    }

    public static RaceStrategyIntelligenceInput CreateInput(
        SessionState session,
        CoachContext? context,
        string? query = null) =>
        new(
            session,
            session.LatestSnapshot,
            context?.RecentSnapshots,
            context?.Analytics,
            context?.LapIntelligence,
            context?.Strategy,
            context?.TyreIntelligence,
            context?.StrategyKnowledge,
            context?.TrackCarKnowledge,
            context?.PreviousStoredSessionMemory,
            context?.CachedWebResearch,
            context?.SessionContext,
            context?.RaceContext,
            StrategyLapCountParser.Parse(query, context?.RaceContext, context?.RacePrepPlan)
                ?? ParseRequestedLapCount(query ?? string.Empty));

    public static string FormatLiters(double? value) =>
        value is { } liters
            ? $"{liters.ToString("0.0", CultureInfo.InvariantCulture)} L"
            : "unavailable";

    public static string FormatBurnRate(double? value) =>
        value is { } burn
            ? $"{burn.ToString("0.0", CultureInfo.InvariantCulture)} L/lap"
            : "unavailable";

    private static IReadOnlyList<double> CollectFuelSamples(SessionState session) =>
        session.CompletedLaps
            .Where(lap => lap.IsValid && lap.FuelUsed.HasValue && lap.FuelUsed.Value > 0)
            .Select(lap => lap.FuelUsed!.Value)
            .ToArray();

    private static RaceStrategyConfidenceLevel ResolveOverallConfidence(
        IReadOnlyList<double> fuelSamples,
        RaceStrategyIntelligenceInput input)
    {
        if (input.SessionContext is { HasStableLapSamples: false })
        {
            return RaceStrategyConfidenceLevel.Low;
        }

        if (fuelSamples.Count < 2)
        {
            return RaceStrategyConfidenceLevel.Low;
        }

        var fuelStable = IsFuelStable(fuelSamples);
        var tyreReliable = input.TyreIntelligence is { HasReliableData: true };

        if (fuelSamples.Count >= 3 && fuelStable && tyreReliable)
        {
            return RaceStrategyConfidenceLevel.High;
        }

        if (fuelSamples.Count >= 2)
        {
            return RaceStrategyConfidenceLevel.Medium;
        }

        return RaceStrategyConfidenceLevel.Low;
    }

    private static bool IsFuelStable(IReadOnlyList<double> samples)
    {
        if (samples.Count < 2)
        {
            return false;
        }

        var recent = samples.TakeLast(3).ToArray();
        var average = recent.Average();
        if (average <= 0)
        {
            return false;
        }

        return recent.All(sample => Math.Abs(sample - average) / average <= FuelStabilityToleranceRatio);
    }

    private static FuelStrategyRecommendation BuildFuelRecommendation(
        RaceStrategyIntelligenceInput input,
        IReadOnlyList<double> fuelSamples,
        int? requestedLaps,
        RaceStrategyConfidenceLevel confidence,
        IReadOnlyList<string> supportingNotes)
    {
        if (fuelSamples.Count < 2)
        {
            return FuelStrategyRecommendation.Unavailable(
                RaceStrategyIntelligenceRecommendation.InsufficientFuelSamplesMessage);
        }

        var currentBurn = input.Session.FuelUsedPerLap ?? fuelSamples.TakeLast(3).Average();
        var conservativeBurn = Math.Max(fuelSamples.TakeLast(3).Max(), currentBurn * 1.02);
        var fuelRemaining = input.Session.LatestFuelLevel;
        double? projectedLaps = fuelRemaining is { } fuelLevel && conservativeBurn > 0
            ? fuelLevel / conservativeBurn
            : null;
        var lapsToFinish = input.RaceContext?.LapsRemaining ?? input.RaceContext?.TotalLaps;
        double? projectedFinishFuel = fuelRemaining is { } currentFuel && lapsToFinish is { } remainingLaps
            ? currentFuel - (conservativeBurn * remainingLaps)
            : input.Strategy?.Fuel.EstimatedFinishFuel;
        var safetyMargin = conservativeBurn * ResolveSafetyMarginLaps(input);
        double? fuelMargin = fuelRemaining is { } liters && lapsToFinish is { } finishLaps
            ? liters - (conservativeBurn * finishLaps)
            : projectedLaps is not null && requestedLaps is { } targetLaps && fuelRemaining is { } availableFuel
                ? availableFuel - (conservativeBurn * targetLaps)
                : null;
        var canFinish = (projectedFinishFuel is { } finishFuel && finishFuel >= safetyMargin)
            || fuelMargin is >= 0;
        var fuelSavingRequired = (projectedFinishFuel is { } finishEstimate && finishEstimate < 0)
            || fuelMargin is < 0
            || (lapsToFinish is { } requiredLaps
                && projectedLaps is { } availableLaps
                && availableLaps + 0.25 < requiredLaps)
            || input.Strategy?.Fuel.RiskLevel == FuelRiskLevel.Critical;
        var fuelTarget = fuelSavingRequired ? currentBurn * 0.97 : currentBurn;
        var recommendation = BuildFuelRecommendationText(
            fuelRemaining,
            currentBurn,
            conservativeBurn,
            projectedLaps,
            projectedFinishFuel,
            fuelSavingRequired,
            requestedLaps,
            canFinish);

        var evidence = new List<string>
        {
            $"Current burn: {FormatBurnRate(currentBurn)}.",
            $"Conservative burn: {FormatBurnRate(conservativeBurn)}.",
            projectedLaps is { } projectedLapCount
                ? $"Projected laps: {projectedLapCount.ToString("0", CultureInfo.InvariantCulture)}."
                : "Projected laps: unavailable.",
            fuelRemaining is { } level
                ? $"Fuel remaining: {FormatLiters(level)}."
                : "Fuel remaining: unavailable."
        };
        evidence.AddRange(supportingNotes);

        return new FuelStrategyRecommendation(
            true,
            "Available",
            currentBurn,
            conservativeBurn,
            fuelRemaining,
            projectedLaps,
            projectedFinishFuel,
            fuelTarget,
            fuelMargin,
            canFinish,
            fuelSavingRequired,
            recommendation,
            confidence,
            confidence.ToString(),
            evidence);
    }

    private static string BuildFuelRecommendationText(
        double? fuelRemaining,
        double currentBurn,
        double conservativeBurn,
        double? projectedLaps,
        double? projectedFinishFuel,
        bool fuelSavingRequired,
        int? requestedLaps,
        bool canFinish)
    {
        if (requestedLaps is { } targetLaps)
        {
            var required = conservativeBurn * targetLaps;
            if (fuelRemaining is not { } fuel)
            {
                return $"Need {FormatLiters(required)} for {targetLaps} laps once fuel level is available.";
            }

            return fuel >= required
                ? $"Yes. {FormatLiters(fuel)} covers {targetLaps} laps at {FormatBurnRate(conservativeBurn)}."
                : $"No. Need {FormatLiters(required)} for {targetLaps} laps; you have {FormatLiters(fuel)}.";
        }

        if (fuelSavingRequired)
        {
            return $"Fuel saving recommended. Target {FormatBurnRate(currentBurn * 0.97)} to protect finish margin.";
        }

        if (canFinish && projectedFinishFuel is >= 0)
        {
            return "No fuel saving required.";
        }

        if (projectedLaps is { } laps)
        {
            return $"No fuel saving required. About {laps.ToString("0", CultureInfo.InvariantCulture)} laps projected on current fuel.";
        }

        return "Monitor fuel burn over the next laps before committing to a save.";
    }

    private static double ResolveSafetyMarginLaps(RaceStrategyIntelligenceInput input) =>
        input.StrategyKnowledge?.FuelSafetyMarginLiters is { } marginLiters
            && input.Session.FuelUsedPerLap is { } burn
            && burn > 0
            ? Math.Max(1.0, marginLiters / burn)
            : 1.0;

    private static TyreStrategyRecommendation BuildTyreRecommendation(
        RaceStrategyIntelligenceInput input,
        RaceStrategyConfidenceLevel confidence,
        IReadOnlyList<string> supportingNotes)
    {
        var snapshots = input.RecentSnapshots ?? [];
        var latest = input.LatestSnapshot ?? input.Session.LatestSnapshot;
        var tyreIntel = input.TyreIntelligence;
        var strategyTyre = input.Strategy?.TyreRisk;
        var lapIntelligence = input.LapIntelligence;

        if (tyreIntel is not { HasReliableData: true }
            && snapshots.Count < 4
            && strategyTyre?.RiskLevel == TyreRiskLevel.Unknown)
        {
            return TyreStrategyRecommendation.Unavailable("Tyre strategy unavailable. Need tyre temperature history.");
        }

        var wearTrend = DescribeWearTrend(latest, lapIntelligence, strategyTyre, input.TrackCarKnowledge);
        var temperatureTrend = DescribeTemperatureTrend(snapshots, tyreIntel);
        var pressureTrend = DescribePressureTrend(latest);
        var degradationRisk = DescribeDegradationRisk(strategyTyre, tyreIntel, lapIntelligence, input.TrackCarKnowledge);
        var (dropMin, dropMax) = EstimatePerformanceDropWindow(strategyTyre, lapIntelligence, tyreIntel);
        var stintViable = strategyTyre?.RiskLevel is not TyreRiskLevel.Critical
            && tyreIntel?.Readiness is not TyreReadiness.Fading and not TyreReadiness.Overheated;
        var outlook = BuildTyreOutlookSummary(temperatureTrend, degradationRisk, dropMin, dropMax, stintViable);

        var evidence = new List<string>
        {
            $"Wear trend: {wearTrend}.",
            $"Temperature trend: {temperatureTrend}.",
            $"Pressure trend: {pressureTrend}.",
            $"Degradation risk: {degradationRisk}."
        };
        if (dropMin is { } min && dropMax is { } max)
        {
            evidence.Add($"Estimated tyre performance drop in {min}-{max} laps.");
        }

        evidence.AddRange(supportingNotes);

        return new TyreStrategyRecommendation(
            true,
            "Available",
            wearTrend,
            temperatureTrend,
            pressureTrend,
            degradationRisk,
            outlook,
            dropMin,
            dropMax,
            stintViable,
            confidence,
            confidence.ToString(),
            evidence);
    }

    private static string DescribeWearTrend(
        TelemetrySnapshot? latest,
        SessionLapIntelligence? lapIntelligence,
        TyreRiskMetric? strategyTyre,
        TrackCarKnowledgeRecommendation? trackCarKnowledge)
    {
        if (latest?.Condition.TyreWear is { Count: >= 4 } wear)
        {
            var rearAverage = AverageNullable(wear[2], wear[3]);
            if (rearAverage is { } value)
            {
                return value >= 0.75
                    ? "Rear wear elevated."
                    : value >= 0.45
                        ? "Rear wear building."
                        : "Wear stable.";
            }
        }

        if (lapIntelligence?.PaceDecay.TrendLabel.Contains("Decaying", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Wear inferred from pace decay.";
        }

        if (strategyTyre?.RiskLevel >= TyreRiskLevel.Moderate)
        {
            return "Wear risk inferred from stint pace drop.";
        }

        if (!string.IsNullOrWhiteSpace(trackCarKnowledge?.TyreWearExpectation))
        {
            return $"Track expectation: {trackCarKnowledge.TyreWearExpectation}.";
        }

        return "Wear trend unavailable.";
    }

    private static string DescribeTemperatureTrend(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        SessionTyreIntelligence? tyreIntel)
    {
        var samples = snapshots
            .Where(snapshot => snapshot.Condition.TyreTempC is { Count: >= 4 })
            .TakeLast(20)
            .ToArray();
        if (samples.Length >= 4)
        {
            var firstHalf = AverageRearTemp(samples.Take(samples.Length / 2));
            var secondHalf = AverageRearTemp(samples.Skip(samples.Length / 2));
            if (firstHalf is { } start && secondHalf is { } end)
            {
                if (end >= start + 3)
                {
                    return "Rear tyres show increasing temperature trend.";
                }

                if (end <= start - 3)
                {
                    return "Rear tyre temperatures are cooling.";
                }

                return "Rear tyre temperatures stable.";
            }
        }

        if (!string.IsNullOrWhiteSpace(tyreIntel?.OverheatingRisk)
            && !tyreIntel.OverheatingRisk.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return tyreIntel.OverheatingRisk;
        }

        return tyreIntel?.WarmupState switch
        {
            TyreWarmupState.Overheating => "Tyres overheating.",
            TyreWarmupState.Fading => "Tyres fading.",
            TyreWarmupState.OptimalWindow => "Tyres in optimal window.",
            _ => "Temperature trend unavailable."
        };
    }

    private static string DescribePressureTrend(TelemetrySnapshot? latest)
    {
        if (latest?.Condition.TyrePressure is not { Count: >= 4 } pressure)
        {
            return "Pressure trend unavailable.";
        }

        var spread = pressure.Max() - pressure.Min();
        return spread >= 4
            ? "Pressure spread widening."
            : spread >= 2
                ? "Pressure stable with minor spread."
                : "Pressure stable.";
    }

    private static string DescribeDegradationRisk(
        TyreRiskMetric? strategyTyre,
        SessionTyreIntelligence? tyreIntel,
        SessionLapIntelligence? lapIntelligence,
        TrackCarKnowledgeRecommendation? trackCarKnowledge)
    {
        if (strategyTyre?.RiskLevel is TyreRiskLevel.High or TyreRiskLevel.Critical)
        {
            return $"{strategyTyre.RiskLevel} ({strategyTyre.RiskScore0To100:0}/100).";
        }

        if (tyreIntel?.Readiness is TyreReadiness.Fading or TyreReadiness.Overheated)
        {
            return tyreIntel.Readiness.ToString();
        }

        if (lapIntelligence?.PaceDecay.DeltaSeconds is > 0.15)
        {
            return "Moderate from pace decay.";
        }

        if (!string.IsNullOrWhiteSpace(trackCarKnowledge?.TyreWearExpectation))
        {
            return $"Track baseline: {trackCarKnowledge.TyreWearExpectation}.";
        }

        return strategyTyre?.RiskLevel.ToString() ?? "Low";
    }

    private static (int? Min, int? Max) EstimatePerformanceDropWindow(
        TyreRiskMetric? strategyTyre,
        SessionLapIntelligence? lapIntelligence,
        SessionTyreIntelligence? tyreIntel)
    {
        if (strategyTyre?.RiskLevel >= TyreRiskLevel.High
            || tyreIntel?.Readiness is TyreReadiness.Fading or TyreReadiness.Overheated)
        {
            return (3, 5);
        }

        if (lapIntelligence?.PaceDecay.DeltaSeconds is > 0.12
            || strategyTyre?.RiskLevel == TyreRiskLevel.Moderate)
        {
            return (4, 6);
        }

        return (null, null);
    }

    private static string BuildTyreOutlookSummary(
        string temperatureTrend,
        string degradationRisk,
        int? dropMin,
        int? dropMax,
        bool stintViable)
    {
        var builder = new StringBuilder(temperatureTrend);
        if (dropMin is { } min && dropMax is { } max)
        {
            builder.Append(' ')
                .Append("Estimated tyre performance drop in ")
                .Append(min)
                .Append('-')
                .Append(max)
                .Append(" laps.");
        }
        else
        {
            builder.Append(" Degradation risk: ").Append(degradationRisk).Append('.');
        }

        if (!stintViable)
        {
            builder.Append(" Stint viability is low.");
        }

        return builder.ToString();
    }

    private static PitStrategyRecommendation BuildPitRecommendation(
        RaceStrategyIntelligenceInput input,
        FuelStrategyRecommendation fuel,
        TyreStrategyRecommendation tyre,
        RaceStrategyConfidenceLevel confidence,
        IReadOnlyList<string> supportingNotes)
    {
        if (!fuel.HasData || confidence == RaceStrategyConfidenceLevel.Low)
        {
            return PitStrategyRecommendation.Unavailable(
                "Pit window unavailable. Need stable fuel samples and completed laps.");
        }

        var strategy = input.Strategy;
        if (strategy?.Pit.Availability != "Available"
            || strategy.Pit.Recommendation == PitRecommendation.Unknown
            || strategy.Pit.EstimatedPitWindowStartLap is null
            || strategy.Pit.EstimatedPitWindowEndLap is null)
        {
            return PitStrategyRecommendation.Unavailable(
                "Pit window unavailable. Need fuel-per-lap data and stable stint telemetry.");
        }

        var currentRisk = DescribeCurrentPitRisk(strategy.Fuel.RiskLevel, strategy.TyreRisk.RiskLevel);
        var expectedGainLoss = DescribeExpectedGainLoss(strategy.Pit.Recommendation, fuel, tyre);
        var summary = strategy.Pit.Recommendation switch
        {
            PitRecommendation.PitNow => "Pit now.",
            PitRecommendation.PrepareToPit => "Prepare to pit within the current window.",
            _ => "Stay out unless tyre or fuel risk rises."
        };

        var evidence = new List<string>
        {
            $"Earliest sensible stop: lap {strategy.Pit.EstimatedPitWindowStartLap}.",
            $"Latest sensible stop: lap {strategy.Pit.EstimatedPitWindowEndLap}.",
            $"Recommendation: {strategy.Pit.Recommendation}.",
            strategy.Pit.RecommendationReason
        };
        evidence.AddRange(supportingNotes);

        return new PitStrategyRecommendation(
            true,
            "Available",
            strategy.Pit.EstimatedPitWindowStartLap,
            strategy.Pit.EstimatedPitWindowEndLap,
            strategy.Pit.Recommendation,
            currentRisk,
            expectedGainLoss,
            summary,
            confidence,
            confidence.ToString(),
            evidence);
    }

    private static string DescribeCurrentPitRisk(FuelRiskLevel fuelRisk, TyreRiskLevel tyreRisk) =>
        fuelRisk >= FuelRiskLevel.High || tyreRisk >= TyreRiskLevel.High
            ? "High"
            : fuelRisk == FuelRiskLevel.Moderate || tyreRisk == TyreRiskLevel.Moderate
                ? "Medium"
                : "Low";

    private static string DescribeExpectedGainLoss(
        PitRecommendation recommendation,
        FuelStrategyRecommendation fuel,
        TyreStrategyRecommendation tyre)
    {
        return recommendation switch
        {
            PitRecommendation.PitNow =>
                fuel.FuelSavingRequired
                    ? "Expected gain: recover fuel margin and fresh tyres."
                    : "Expected gain: protect stint length and tyre performance.",
            PitRecommendation.PrepareToPit =>
                tyre.EstimatedPerformanceDropLapsMin is { } min
                    ? $"Staying out may cost pace within {min} laps."
                    : "Staying out preserves track position for now.",
            _ => "Staying out preserves track position while data remains stable."
        };
    }

    private static StintRiskAssessment BuildStintRisk(
        FuelStrategyRecommendation fuel,
        TyreStrategyRecommendation tyre,
        PitStrategyRecommendation pit,
        RaceStrategyConfidenceLevel confidence)
    {
        var fuelRisk = fuel.HasData
            ? fuel.FuelSavingRequired ? "Moderate" : "Low"
            : "Unknown";
        var tyreRisk = tyre.HasData ? tyre.DegradationRisk : "Unknown";
        var overall = pit.CurrentRisk != "Unknown"
            ? pit.CurrentRisk
            : fuelRisk == "Moderate" || tyreRisk.Contains("High", StringComparison.OrdinalIgnoreCase)
                ? "Medium"
                : fuel.HasData || tyre.HasData
                    ? "Low"
                    : "Unknown";
        var summary = $"{overall} overall stint risk. Fuel {fuelRisk.ToLowerInvariant()}, tyre {tyreRisk.ToLowerInvariant()}.";
        return new StintRiskAssessment(overall, fuelRisk, tyreRisk, summary, confidence, confidence.ToString());
    }

    private static string BuildStrategySummary(
        FuelStrategyRecommendation fuel,
        TyreStrategyRecommendation tyre,
        PitStrategyRecommendation pit,
        StintRiskAssessment stintRisk)
    {
        var parts = new List<string>();
        if (fuel.HasData)
        {
            parts.Add($"Fuel burn {FormatBurnRate(fuel.CurrentBurnRatePerLap)}");
            if (fuel.ProjectedLapsRemaining is { } laps)
            {
                parts.Add($"~{laps.ToString("0", CultureInfo.InvariantCulture)} laps projected");
            }

            parts.Add(fuel.Recommendation);
        }

        if (tyre.HasData)
        {
            parts.Add(tyre.OutlookSummary);
        }

        if (pit.HasData)
        {
            parts.Add(pit.RecommendationSummary);
        }

        return parts.Count == 0 ? stintRisk.Summary : string.Join(". ", parts) + ".";
    }

    private static IReadOnlyList<string> BuildEvidenceLines(
        FuelStrategyRecommendation fuel,
        TyreStrategyRecommendation tyre,
        PitStrategyRecommendation pit,
        IReadOnlyList<string> supportingNotes)
    {
        var lines = new List<string>();
        lines.AddRange(fuel.EvidenceLines);
        lines.AddRange(tyre.EvidenceLines.Where(line => !lines.Contains(line, StringComparer.OrdinalIgnoreCase)));
        lines.AddRange(pit.EvidenceLines.Where(line => !lines.Contains(line, StringComparer.OrdinalIgnoreCase)));
        lines.AddRange(supportingNotes.Where(line => !lines.Contains(line, StringComparer.OrdinalIgnoreCase)));
        return lines;
    }

    private static IReadOnlyList<string> BuildSupportingNotes(RaceStrategyIntelligenceInput input)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.StrategyKnowledge?.FuelForLapsNote))
        {
            notes.Add($"Catalog/supporting: {input.StrategyKnowledge.FuelForLapsNote}");
        }

        if (!string.IsNullOrWhiteSpace(input.TrackCarKnowledge?.FuelUsageExpectation))
        {
            notes.Add($"Track/car baseline: {input.TrackCarKnowledge.FuelUsageExpectation}.");
        }

        if (input.CachedWebResearch?.HasResearch == true)
        {
            notes.Add("Cached research available as supporting context only.");
        }

        return notes;
    }

    private static double? AverageRearTemp(IEnumerable<TelemetrySnapshot> snapshots)
    {
        var values = snapshots
            .Select(snapshot => AverageNullable(
                snapshot.Condition.TyreTempC?.ElementAtOrDefault(2),
                snapshot.Condition.TyreTempC?.ElementAtOrDefault(3)))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private static double? AverageNullable(double? first, double? second)
    {
        if (first is { } a && second is { } b)
        {
            return (a + b) / 2.0;
        }

        return first ?? second;
    }

    public static int? ParseRequestedLapCount(string query)
    {
        var match = Regex.Match(
            query.ToLowerInvariant(),
            @"(?:fuel for|for)\s*(\d{1,3})\s*(?:laps?|krugova|krug|kola)?");
        return match.Success && int.TryParse(match.Groups[1].Value, out var laps) ? laps : null;
    }
}
