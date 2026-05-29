using System.Globalization;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Strategy;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.RaceAwareness;

public static class OpponentIntelligenceService
{
    private const double StableThresholdSeconds = 0.05;
    private const double CloseGapSeconds = 1.5;
    private const double TrainGapSeconds = 1.0;
    private const double IsolatedGapSeconds = 3.0;

    public static OpponentIntelligenceRecommendation Build(OpponentIntelligenceInput input)
    {
        var current = BuildCurrentSnapshot(input.RaceContext);
        if (current is null || !current.HasOpponentData)
        {
            return OpponentIntelligenceRecommendation.Unavailable;
        }

        var historySnapshots = BuildHistorySnapshots(input.RecentSnapshots, input.RaceContext);
        var gapTrend = AnalyzeGapTrend(historySnapshots);
        var battle = DetectRaceBattle(current, gapTrend);
        var strategyInsight = BuildStrategyInsight(current, gapTrend, battle, input.Strategy);
        var attackZone = BuildAttackRecommendation(battle, gapTrend, input.TrackGuide);
        var defendZone = BuildDefendRecommendation(battle, gapTrend, input.TrackGuide);
        var storedOvertake = input.TrackMemory?.OvertakeSuccessZones ?? [];
        var storedDefensive = input.TrackMemory?.DefensiveWeaknesses ?? [];
        var storedOutcomes = input.TrackMemory?.CommonBattleOutcomes ?? [];
        var summary = BuildSummary(current, gapTrend, battle, strategyInsight);

        return new OpponentIntelligenceRecommendation(
            true,
            current,
            gapTrend,
            battle,
            strategyInsight,
            attackZone,
            defendZone,
            storedOvertake,
            storedDefensive,
            storedOutcomes,
            summary);
    }

    public static IReadOnlyList<string> BuildBattleMemoryUpdates(
        OpponentIntelligenceRecommendation recommendation,
        TrackGuide? trackGuide)
    {
        if (!recommendation.HasOpponentData)
        {
            return [];
        }

        var updates = new List<string>();
        if (recommendation.Battle.Type == RaceBattleType.AttackOpportunity
            && !string.IsNullOrWhiteSpace(recommendation.AttackZoneRecommendation))
        {
            updates.Add($"Attack opportunity: {TrimZone(recommendation.AttackZoneRecommendation)}");
        }

        if (recommendation.Battle.Type == RaceBattleType.DefensiveSituation
            && !string.IsNullOrWhiteSpace(recommendation.DefendZoneRecommendation))
        {
            updates.Add($"Defensive pressure: {TrimZone(recommendation.DefendZoneRecommendation)}");
        }

        if (trackGuide?.OvertakingZones.Count > 0
            && recommendation.GapTrend.AheadDirection == GapTrendDirection.Gaining)
        {
            updates.Add($"Overtake zone noted: {trackGuide.OvertakingZones[0]}");
        }

        return updates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    private static OpponentSnapshot? BuildCurrentSnapshot(LiveRaceContext? raceContext)
    {
        if (raceContext is null || raceContext.Confidence == RaceContextConfidence.Unavailable)
        {
            return null;
        }

        return new OpponentSnapshot(
            raceContext.Position,
            raceContext.TotalCars,
            raceContext.CarAhead,
            raceContext.CarBehind,
            raceContext.GapAheadSeconds,
            raceContext.GapBehindSeconds,
            raceContext.CurrentLap);
    }

    private static IReadOnlyList<OpponentSnapshot> BuildHistorySnapshots(
        IReadOnlyList<TelemetrySnapshot>? snapshots,
        LiveRaceContext? raceContext)
    {
        if (snapshots is not { Count: > 0 })
        {
            return raceContext is null
                ? []
                : [BuildCurrentSnapshot(raceContext)!];
        }

        return snapshots
            .Select(snapshot => new OpponentSnapshot(
                snapshot.RaceAwareness?.Position ?? snapshot.Race.Position,
                snapshot.RaceAwareness?.TotalCars,
                snapshot.RaceAwareness?.CarAhead,
                snapshot.RaceAwareness?.CarBehind,
                snapshot.RaceAwareness?.GapAheadSeconds,
                snapshot.RaceAwareness?.GapBehindSeconds,
                snapshot.RaceAwareness?.CurrentLap ?? snapshot.Lap.LapNumber))
            .Where(item => item.GapAheadSeconds.HasValue || item.GapBehindSeconds.HasValue)
            .TakeLast(40)
            .ToArray();
    }

    public static GapTrend AnalyzeGapTrend(IReadOnlyList<OpponentSnapshot> snapshots)
    {
        if (snapshots.Count < 4)
        {
            return GapTrend.Unavailable;
        }

        var aheadSeries = BuildLapGapSeries(snapshots, gap => gap.GapAheadSeconds);
        var behindSeries = BuildLapGapSeries(snapshots, gap => gap.GapBehindSeconds);
        var aheadChange = ComputeTrendChange(aheadSeries);
        var behindChange = ComputeTrendChange(behindSeries);
        var sampleCount = Math.Max(aheadSeries.Count, behindSeries.Count);
        var confidence = ResolveConfidence(sampleCount, aheadChange, behindChange);
        var aheadDirection = ClassifyGapDirection(aheadChange, invert: true);
        var behindDirection = ClassifyGapDirection(behindChange, invert: false);
        var summary = BuildGapTrendSummary(aheadDirection, behindDirection, aheadChange, behindChange, confidence);

        if (aheadDirection == GapTrendDirection.Unavailable
            && behindDirection == GapTrendDirection.Unavailable)
        {
            return GapTrend.Unavailable;
        }

        return new GapTrend(
            aheadDirection,
            behindDirection,
            aheadChange,
            behindChange,
            confidence,
            summary);
    }

    private static List<(int Lap, double Gap)> BuildLapGapSeries(
        IReadOnlyList<OpponentSnapshot> snapshots,
        Func<OpponentSnapshot, double?> selector)
    {
        return snapshots
            .Where(item => item.CurrentLap is { } lap && selector(item) is { } gap)
            .GroupBy(item => item.CurrentLap!.Value)
            .Select(group => (Lap: group.Key, Gap: Median(group.Select(selector).Where(value => value.HasValue).Select(value => value!.Value))))
            .OrderBy(item => item.Lap)
            .ToList();
    }

    private static double? ComputeTrendChange(IReadOnlyList<(int Lap, double Gap)> series)
    {
        if (series.Count < 2)
        {
            return null;
        }

        var recent = series.TakeLast(Math.Min(3, series.Count)).Select(item => item.Gap).ToArray();
        var previous = series
            .Skip(Math.Max(0, series.Count - recent.Length - 1))
            .Take(recent.Length)
            .Select(item => item.Gap)
            .ToArray();
        if (previous.Length == 0 || recent.Length == 0)
        {
            return null;
        }

        var recentAverage = recent.Average();
        var previousAverage = previous.Average();
        return recentAverage - previousAverage;
    }

    private static GapTrendDirection ClassifyGapDirection(double? changePerLap, bool invert)
    {
        if (changePerLap is not { } change)
        {
            return GapTrendDirection.Unavailable;
        }

        if (Math.Abs(change) < StableThresholdSeconds)
        {
            return GapTrendDirection.Stable;
        }

        var adjusted = invert ? -change : change;
        return adjusted > 0 ? GapTrendDirection.Gaining : GapTrendDirection.Losing;
    }

    private static BattleConfidence ResolveConfidence(int sampleCount, double? aheadChange, double? behindChange)
    {
        if (sampleCount >= 4
            && aheadChange.HasValue
            && behindChange.HasValue
            && Math.Abs(aheadChange.Value) >= StableThresholdSeconds
            && Math.Abs(behindChange.Value) >= StableThresholdSeconds)
        {
            return BattleConfidence.High;
        }

        if (sampleCount >= 3 && (aheadChange.HasValue || behindChange.HasValue))
        {
            return BattleConfidence.Medium;
        }

        return BattleConfidence.Low;
    }

    private static RaceBattle DetectRaceBattle(OpponentSnapshot current, GapTrend gapTrend)
    {
        var ahead = current.GapAheadSeconds;
        var behind = current.GapBehindSeconds;

        if (ahead is < TrainGapSeconds && behind is < TrainGapSeconds)
        {
            return new RaceBattle(
                RaceBattleType.TrainOfCars,
                BattleConfidence.High,
                "train",
                "You are in a train of cars with close gaps ahead and behind.");
        }

        if (gapTrend.AheadDirection == GapTrendDirection.Gaining
            && ahead is > 0 and < CloseGapSeconds)
        {
            return new RaceBattle(
                RaceBattleType.AttackOpportunity,
                gapTrend.Confidence,
                "attack",
                "You are closing on the car ahead.");
        }

        if (gapTrend.BehindDirection == GapTrendDirection.Losing
            && behind is > 0 and < CloseGapSeconds)
        {
            return new RaceBattle(
                RaceBattleType.DefensiveSituation,
                gapTrend.Confidence,
                "defend",
                "The car behind is closing.");
        }

        if ((ahead is null or > IsolatedGapSeconds) && (behind is null or > IsolatedGapSeconds))
        {
            return new RaceBattle(
                RaceBattleType.IsolatedRunning,
                BattleConfidence.Medium,
                "isolated",
                "You have clear space ahead and behind.");
        }

        return new RaceBattle(
            RaceBattleType.Unavailable,
            BattleConfidence.Low,
            "monitor",
            "No clear battle pattern is confirmed yet.");
    }

    private static OpponentStrategyInsight BuildStrategyInsight(
        OpponentSnapshot current,
        GapTrend gapTrend,
        RaceBattle battle,
        SessionStrategy? strategy)
    {
        if (strategy is null)
        {
            return new OpponentStrategyInsight(false, null, null, null);
        }

        string? undercut = null;
        string? overcut = null;
        string? risk = null;

        if (battle.Type == RaceBattleType.AttackOpportunity
            && gapTrend.AheadDirection == GapTrendDirection.Gaining
            && current.GapAheadSeconds is < CloseGapSeconds
            && strategy.Pit.Recommendation != PitRecommendation.StayOut)
        {
            undercut = "Possible undercut if the car ahead pits while you stay out and maintain pace.";
        }

        if (battle.Type == RaceBattleType.DefensiveSituation
            && gapTrend.BehindDirection == GapTrendDirection.Losing
            && strategy.Pit.Recommendation == PitRecommendation.StayOut)
        {
            overcut = "Possible overcut if you pit cleanly while the car behind stays out.";
        }

        if (!string.IsNullOrWhiteSpace(strategy.Summary))
        {
            risk = strategy.Summary;
        }
        else if (strategy.Fuel.RiskLevel != FuelRiskLevel.Unknown)
        {
            risk = $"Strategy risk: {strategy.Fuel.RiskLevel}.";
        }

        var hasData = undercut is not null || overcut is not null || risk is not null;
        return new OpponentStrategyInsight(hasData, undercut, overcut, risk);
    }

    private static string? BuildAttackRecommendation(
        RaceBattle battle,
        GapTrend gapTrend,
        TrackGuide? trackGuide)
    {
        if (battle.Type != RaceBattleType.AttackOpportunity
            && gapTrend.AheadDirection != GapTrendDirection.Gaining)
        {
            return null;
        }

        var zone = trackGuide?.OvertakingZones.FirstOrDefault()
            ?? trackGuide?.MajorBrakingZones.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(zone))
        {
            return gapTrend.AheadDirection == GapTrendDirection.Gaining
                ? "Gap is decreasing. Look for a clean pass where you have better exit speed."
                : null;
        }

        return gapTrend.AheadDirection == GapTrendDirection.Gaining
            ? $"Gap is decreasing. Best opportunity is into {zone}."
            : null;
    }

    private static string? BuildDefendRecommendation(
        RaceBattle battle,
        GapTrend gapTrend,
        TrackGuide? trackGuide)
    {
        if (battle.Type != RaceBattleType.DefensiveSituation
            && gapTrend.BehindDirection != GapTrendDirection.Losing)
        {
            return null;
        }

        var zone = trackGuide?.MajorBrakingZones.FirstOrDefault()
            ?? trackGuide?.TractionZones.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(zone))
        {
            return gapTrend.BehindDirection == GapTrendDirection.Losing
                ? "Car behind is closing. Protect the inside and prioritize exit traction."
                : null;
        }

        return gapTrend.BehindDirection == GapTrendDirection.Losing
            ? $"Defend on the approach to {zone}."
            : null;
    }

    private static string BuildSummary(
        OpponentSnapshot current,
        GapTrend gapTrend,
        RaceBattle battle,
        OpponentStrategyInsight strategyInsight)
    {
        var parts = new List<string>();
        if (current.Position is { } position)
        {
            parts.Add(current.TotalCars is { } total
                ? $"P{position}/{total}"
                : $"P{position}");
        }

        if (!string.IsNullOrWhiteSpace(current.CarAhead) && current.GapAheadSeconds is { } gapAhead)
        {
            parts.Add($"ahead {current.CarAhead} {FormatSeconds(gapAhead)}");
        }
        else if (current.GapAheadSeconds is { } aheadOnly)
        {
            parts.Add($"gap ahead {FormatSeconds(aheadOnly)}");
        }

        if (!string.IsNullOrWhiteSpace(current.CarBehind) && current.GapBehindSeconds is { } gapBehind)
        {
            parts.Add($"behind {current.CarBehind} {FormatSeconds(gapBehind)}");
        }
        else if (current.GapBehindSeconds is { } behindOnly)
        {
            parts.Add($"gap behind {FormatSeconds(behindOnly)}");
        }

        parts.Add(gapTrend.Summary);
        parts.Add($"{battle.StatusLabel} ({battle.Confidence.ToString().ToLowerInvariant()} confidence)");
        if (strategyInsight.HasData && !string.IsNullOrWhiteSpace(strategyInsight.RiskAssessment))
        {
            parts.Add(strategyInsight.RiskAssessment);
        }

        return string.Join("; ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildGapTrendSummary(
        GapTrendDirection aheadDirection,
        GapTrendDirection behindDirection,
        double? aheadChange,
        double? behindChange,
        BattleConfidence confidence)
    {
        var parts = new List<string>();
        if (aheadDirection != GapTrendDirection.Unavailable && aheadChange is { } aheadDelta)
        {
            parts.Add(aheadDirection switch
            {
                GapTrendDirection.Gaining =>
                    $"You are gaining {FormatSeconds(Math.Abs(aheadDelta))} per lap on the car ahead.",
                GapTrendDirection.Losing =>
                    $"You are losing {FormatSeconds(Math.Abs(aheadDelta))} per lap to the car ahead.",
                _ => "Gap ahead is stable."
            });
        }

        if (behindDirection != GapTrendDirection.Unavailable && behindChange is { } behindDelta)
        {
            parts.Add(behindDirection switch
            {
                GapTrendDirection.Gaining =>
                    $"You are pulling away by {FormatSeconds(Math.Abs(behindDelta))} per lap from the car behind.",
                GapTrendDirection.Losing =>
                    $"The car behind is gaining {FormatSeconds(Math.Abs(behindDelta))} per lap.",
                _ => "Gap behind is stable."
            });
        }

        if (parts.Count == 0)
        {
            return GapTrend.Unavailable.Summary;
        }

        return $"{string.Join(" ", parts)} ({confidence.ToString().ToLowerInvariant()} confidence)";
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(item => item).ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var mid = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[mid - 1] + ordered[mid]) / 2.0
            : ordered[mid];
    }

    private static string FormatSeconds(double value) =>
        $"{value.ToString("0.0", CultureInfo.InvariantCulture)}s";

    private static string TrimZone(string recommendation)
    {
        var trimmed = recommendation.Trim();
        return trimmed.Length <= 96 ? trimmed : trimmed[..93] + "...";
    }
}
