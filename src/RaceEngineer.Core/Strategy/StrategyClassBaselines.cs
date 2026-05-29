namespace RaceEngineer.Core.Strategy;

public sealed record StrategyClassBaseline(
    string ClassName,
    double ExpectedFuelPerLap,
    double FuelSafetyMarginLaps,
    string TyreWarmupEstimate,
    string TyreDegradationRisk,
    string PitWindowEstimate,
    string OvertakingDifficulty,
    string BrakingStress,
    string TractionStress,
    IReadOnlyList<string> SetupNotes,
    IReadOnlyList<string> DrivingPriorities);

public static class StrategyClassBaselines
{
    private static readonly IReadOnlyList<StrategyClassBaseline> Baselines =
    [
        new(
            "GT3",
            2.4,
            1.0,
            "Plan one out lap before pushing; front grip builds through lap 2.",
            "Moderate rear degradation on long stints; overheating follows heavy traction zones.",
            "Typical first stop between laps 14 and 18 depending on fuel target.",
            "Moderate — draft helps on long straights but defending is costly.",
            "High in heavy braking zones; pad and ABS stability matter.",
            "Medium — traction exits decide lap time more than top speed.",
            ["Start with stable brake bias and conservative rear wing if tyre temps spike early."],
            ["Protect tyres in traffic", "Prioritize clean exits over one-lap quali pace", "Match fuel target before pushing"]),
        new(
            "GTE",
            2.2,
            1.0,
            "Tyres need one clean lap; avoid sliding on out lap.",
            "Front degradation rises under long braking if you overwork entry speed.",
            "One-stop races often target laps 16-22 depending on track fuel use.",
            "High — class battles are common and overlap is frequent.",
            "Very high under heavy braking with long fuel stints.",
            "Medium-high on traction-limited slow corners.",
            ["Use conservative camber if front inside temps climb quickly."],
            ["Manage front tyre temperature", "Protect fuel for final stint", "Plan passes early in braking zones"]),
        new(
            "LMP2",
            2.8,
            0.75,
            "Warm tyres gently; peak grip arrives after one flying lap.",
            "Rear degradation accelerates if you push before tyres are ready.",
            "Two-stop races often split around laps 12-15 and 28-32.",
            "Low-moderate — prototype pace reduces side-by-side time.",
            "High at high speed with aero load; stability under braking is key.",
            "Medium — traction on slow corners still costs lap time.",
            ["Check brake migration if fronts fade after long stints."],
            ["Build tyre temperature before push", "Use clean air for fuel burn reference", "Protect stint average over one lap"])
    ];

    public static bool TryGet(string? carClassOrName, out StrategyClassBaseline baseline)
    {
        baseline = null!;
        if (string.IsNullOrWhiteSpace(carClassOrName))
        {
            return false;
        }

        var key = carClassOrName.Trim();
        baseline = Baselines.FirstOrDefault(item =>
                     key.Contains(item.ClassName, StringComparison.OrdinalIgnoreCase))
                 ?? Baselines.FirstOrDefault(item =>
                     string.Equals(item.ClassName, key, StringComparison.OrdinalIgnoreCase))
                 ?? null!;
        return baseline is not null;
    }
}
