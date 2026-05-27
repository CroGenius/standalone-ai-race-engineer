using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Strategy;

public sealed class StrategyEngine
{
    private readonly StrategyEngineOptions options;

    public StrategyEngine(StrategyEngineOptions? options = null)
    {
        this.options = options ?? new StrategyEngineOptions();
    }

    public SessionStrategy Analyze(StrategyInput input)
    {
        var activeOptions = input.Preferences is not null
            ? Profile.StrategyPreferencesMapper.ToEngineOptions(input.Preferences)
            : options;
        var session = input.Session;
        var analytics = input.Analytics ?? SessionTelemetryAnalytics.Empty;
        var lapIntelligence = input.LapIntelligence ?? SessionLapIntelligence.Empty;
        var events = input.Events ?? session.Events;

        var fuelPerLap = session.FuelUsedPerLap ?? analytics.FuelTrend.FuelPerLap;
        var lapsRemaining = session.EstimatedLapsRemaining ?? analytics.FuelTrend.EstimatedLapsRemaining;
        var latestFuel = session.LatestFuelLevel;
        var targetStintLaps = ParseTargetStintLaps(input.PrepPlan?.TargetStintLength) ?? activeOptions.DefaultTargetStintLaps;
        var completedLaps = session.CompletedLaps.Count;
        var lapsLeftInStint = Math.Max(0, targetStintLaps - completedLaps);

        var fuel = BuildFuelPrediction(latestFuel, fuelPerLap, lapsRemaining, lapsLeftInStint, events, activeOptions);
        var tyreRisk = BuildTyreRisk(analytics, lapIntelligence, events, activeOptions);
        var pit = BuildPitStrategy(
            fuel,
            tyreRisk,
            completedLaps,
            targetStintLaps,
            lapsLeftInStint,
            fuelPerLap,
            latestFuel,
            activeOptions);
        var summary = BuildSummary(fuel, pit, tyreRisk, completedLaps, targetStintLaps);
        var callout = BuildCalloutSignal(fuel, pit, tyreRisk);

        return new SessionStrategy(fuel, pit, tyreRisk, summary, callout);
    }

    private FuelPredictionMetric BuildFuelPrediction(
        double? latestFuel,
        double? fuelPerLap,
        double? lapsRemaining,
        int lapsLeftInStint,
        IReadOnlyList<TelemetryEvent> events,
        StrategyEngineOptions activeOptions)
    {
        if (latestFuel is null)
        {
            return new FuelPredictionMetric(null, null, null, FuelRiskLevel.Unknown, "Latest fuel level unavailable.");
        }

        if (fuelPerLap is null or <= 0)
        {
            return new FuelPredictionMetric(
                null,
                lapsRemaining,
                latestFuel,
                FuelRiskLevel.Unknown,
                "Fuel per lap unavailable until enough valid laps are completed.");
        }

        var estimatedFinishFuel = latestFuel.Value - (fuelPerLap.Value * lapsLeftInStint);
        var lowFuelActive = events.Any(item => item.Type == EventType.LowFuel);
        var risk = ClassifyFuelRisk(latestFuel.Value, lapsRemaining, lowFuelActive, activeOptions);

        return new FuelPredictionMetric(
            fuelPerLap,
            lapsRemaining,
            estimatedFinishFuel,
            risk,
            "Available");
    }

    private FuelRiskLevel ClassifyFuelRisk(
        double latestFuel,
        double? lapsRemaining,
        bool lowFuelActive,
        StrategyEngineOptions activeOptions)
    {
        if (lowFuelActive || latestFuel <= activeOptions.FuelCriticalLevel)
        {
            return FuelRiskLevel.Critical;
        }

        if (latestFuel <= activeOptions.FuelHighLevel
            || (lapsRemaining.HasValue && lapsRemaining.Value <= activeOptions.FuelCriticalLapsRemaining))
        {
            return FuelRiskLevel.High;
        }

        if (lapsRemaining.HasValue && lapsRemaining.Value <= activeOptions.FuelHighLapsRemaining)
        {
            return FuelRiskLevel.High;
        }

        if (lapsRemaining.HasValue && lapsRemaining.Value <= activeOptions.FuelModerateLapsRemaining)
        {
            return FuelRiskLevel.Moderate;
        }

        return FuelRiskLevel.Low;
    }

    private PitStrategyMetric BuildPitStrategy(
        FuelPredictionMetric fuel,
        TyreRiskMetric tyreRisk,
        int completedLaps,
        int targetStintLaps,
        int lapsLeftInStint,
        double? fuelPerLap,
        double? latestFuel,
        StrategyEngineOptions activeOptions)
    {
        if (fuel.FuelUsedPerLap is null)
        {
            return new PitStrategyMetric(
                null,
                null,
                completedLaps > 0 ? completedLaps : null,
                PitRecommendation.Unknown,
                null,
                "Need fuel-per-lap data before pit guidance is available.",
                "Need fuel-per-lap data.");
        }

        var windowStart = Math.Max(1, targetStintLaps - activeOptions.PitWindowLeadLaps + activeOptions.PitWindowShiftLaps);
        var windowEnd = Math.Max(windowStart, targetStintLaps + activeOptions.PitWindowShiftLaps);
        var stintEstimate = completedLaps + (int)Math.Floor(fuel.LapsRemaining ?? 0);
        var minimumFuelToFinish = fuelPerLap * lapsLeftInStint;
        var recommendation = ResolvePitRecommendation(
            fuel.RiskLevel,
            tyreRisk.RiskLevel,
            completedLaps,
            windowStart,
            windowEnd,
            fuel.LapsRemaining,
            activeOptions);
        var reason = DescribePitRecommendation(recommendation, fuel, tyreRisk, completedLaps, windowStart, windowEnd);

        return new PitStrategyMetric(
            windowStart,
            windowEnd,
            stintEstimate,
            recommendation,
            minimumFuelToFinish,
            reason,
            "Available");
    }

    private PitRecommendation ResolvePitRecommendation(
        FuelRiskLevel fuelRisk,
        TyreRiskLevel tyreRisk,
        int completedLaps,
        int windowStart,
        int windowEnd,
        double? lapsRemaining,
        StrategyEngineOptions activeOptions)
    {
        if (fuelRisk == FuelRiskLevel.Critical
            || (lapsRemaining.HasValue && lapsRemaining.Value <= activeOptions.FuelCriticalLapsRemaining))
        {
            return PitRecommendation.PitNow;
        }

        if (fuelRisk == FuelRiskLevel.High
            || tyreRisk >= TyreRiskLevel.Critical
            || completedLaps >= windowEnd)
        {
            return PitRecommendation.PitNow;
        }

        if (tyreRisk >= TyreRiskLevel.High
            || fuelRisk == FuelRiskLevel.Moderate
            || completedLaps >= windowStart)
        {
            return PitRecommendation.PrepareToPit;
        }

        return PitRecommendation.StayOut;
    }

    private static string DescribePitRecommendation(
        PitRecommendation recommendation,
        FuelPredictionMetric fuel,
        TyreRiskMetric tyreRisk,
        int completedLaps,
        int windowStart,
        int windowEnd)
    {
        return recommendation switch
        {
            PitRecommendation.PitNow =>
                $"Pit now: fuel risk {fuel.RiskLevel}, tyre risk {tyreRisk.RiskLevel}, completed lap {completedLaps}.",
            PitRecommendation.PrepareToPit =>
                $"Prepare to pit: window {windowStart}-{windowEnd}, fuel risk {fuel.RiskLevel}, tyre risk {tyreRisk.RiskLevel}.",
            PitRecommendation.StayOut =>
                $"Stay out: fuel risk {fuel.RiskLevel}, tyre risk {tyreRisk.RiskLevel}, pit window opens around lap {windowStart}.",
            _ => "Pit recommendation unavailable."
        };
    }

    private TyreRiskMetric BuildTyreRisk(
        SessionTelemetryAnalytics analytics,
        SessionLapIntelligence lapIntelligence,
        IReadOnlyList<TelemetryEvent> events,
        StrategyEngineOptions activeOptions)
    {
        if (lapIntelligence.PaceDecay.Availability != "Available"
            && analytics.BrakeStability.Score0To100 is null
            && analytics.ThrottleSmoothness.Score0To100 is null)
        {
            return new TyreRiskMetric(TyreRiskLevel.Unknown, 0, [], "Need completed laps and pace trends.");
        }

        var score = 0d;
        var factors = new List<string>();

        if (lapIntelligence.PaceDecay.DeltaSeconds is { } paceDelta && paceDelta > 0.150)
        {
            score += Math.Min(30, paceDelta * 40);
            factors.Add($"Pace decay {paceDelta.ToString("0.000", CultureInfo.InvariantCulture)}s in stint.");
        }
        else if (lapIntelligence.PaceDecay.TrendLabel.Contains("Decaying", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
            factors.Add("Stint pace is slowing.");
        }

        if (analytics.PaceTrend.TrendLabel.Contains("Slowing", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            factors.Add("Recent lap pace trend is slowing.");
        }

        if (analytics.BrakeStability.Score0To100 is { } brakeScore && brakeScore < 55)
        {
            score += 20;
            factors.Add($"Brake instability score {brakeScore.ToString("0", CultureInfo.InvariantCulture)}/100.");
        }

        if (analytics.ThrottleSmoothness.Score0To100 is { } throttleScore && throttleScore < 55)
        {
            score += 15;
            factors.Add($"Throttle roughness score {throttleScore.ToString("0", CultureInfo.InvariantCulture)}/100.");
        }

        if (analytics.Incidents.IncidentsPerLap >= 1.0)
        {
            score += 15;
            factors.Add($"Incident rate {analytics.Incidents.IncidentsPerLap.ToString("0.00", CultureInfo.InvariantCulture)}/lap.");
        }

        var tyreEvents = events.Count(item => item.Type == EventType.TyreOverheating);
        if (tyreEvents > 0)
        {
            score += Math.Min(20, tyreEvents * 8);
            factors.Add($"{tyreEvents} tyre overheating event(s).");
        }

        score = Math.Clamp(score, 0, 100);
        var risk = score >= activeOptions.TyreRiskCriticalScore
            ? TyreRiskLevel.Critical
            : score >= activeOptions.TyreRiskHighScore
                ? TyreRiskLevel.High
                : score >= activeOptions.TyreRiskModerateScore
                    ? TyreRiskLevel.Moderate
                    : TyreRiskLevel.Low;

        return new TyreRiskMetric(
            risk,
            score,
            factors,
            factors.Count == 0 ? "Low inferred tyre risk." : "Available");
    }

    private static string BuildSummary(
        FuelPredictionMetric fuel,
        PitStrategyMetric pit,
        TyreRiskMetric tyreRisk,
        int completedLaps,
        int targetStintLaps)
    {
        if (fuel.Availability != "Available" || pit.Availability != "Available")
        {
            return SessionStrategy.Empty.Summary;
        }

        var fuelPart = fuel.LapsRemaining is { } laps
            ? $"Fuel {fuel.RiskLevel}, ~{laps.ToString("0.0", CultureInfo.InvariantCulture)} laps left"
            : $"Fuel {fuel.RiskLevel}";
        var pitPart = $"{pit.Recommendation} (window L{pit.EstimatedPitWindowStartLap}-L{pit.EstimatedPitWindowEndLap})";
        var tyrePart = $"Tyre risk {tyreRisk.RiskLevel}";
        return $"{fuelPart}. {pitPart}. {tyrePart}. Stint {completedLaps}/{targetStintLaps} laps.";
    }

    private static StrategyCalloutSignal? BuildCalloutSignal(
        FuelPredictionMetric fuel,
        PitStrategyMetric pit,
        TyreRiskMetric tyreRisk)
    {
        if (pit.Recommendation == PitRecommendation.PitNow)
        {
            return new StrategyCalloutSignal("Box this lap.", "pit-now");
        }

        if (fuel.RiskLevel == FuelRiskLevel.Critical)
        {
            return new StrategyCalloutSignal("Fuel critical. Box soon.", "fuel-critical");
        }

        if (fuel.RiskLevel == FuelRiskLevel.High && pit.Recommendation == PitRecommendation.PrepareToPit)
        {
            return new StrategyCalloutSignal("Fuel tight. Prepare to pit.", "fuel-prepare-pit");
        }

        if (tyreRisk.RiskLevel >= TyreRiskLevel.High)
        {
            return new StrategyCalloutSignal("Pace is fading. Watch the pit window.", "tyre-risk");
        }

        if (pit.Recommendation == PitRecommendation.PrepareToPit)
        {
            return new StrategyCalloutSignal("Prepare to pit soon.", "prepare-pit");
        }

        return null;
    }

    private static int? ParseTargetStintLaps(string? targetStintLength)
    {
        if (string.IsNullOrWhiteSpace(targetStintLength))
        {
            return null;
        }

        var match = Regex.Match(targetStintLength, @"\d+");
        return match.Success && int.TryParse(match.Value, out var laps) && laps > 0 ? laps : null;
    }
}
