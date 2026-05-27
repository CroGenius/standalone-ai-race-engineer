namespace RaceEngineer.Core.Strategy;

public sealed class StrategyCalloutManager
{
    private readonly TimeSpan cooldown = TimeSpan.FromSeconds(25);
    private string? lastStateSignature;
    private DateTimeOffset lastSpokenAt;

    public string LastSuppressionState { get; private set; } = "No strategy callouts suppressed.";

    public string? TryCreateCallout(SessionStrategy strategy, DateTimeOffset timestamp)
    {
        if (strategy.CalloutSignal is not { } signal)
        {
            LastSuppressionState = "Strategy stable.";
            return null;
        }

        var signature = BuildSignature(strategy);
        if (signature == lastStateSignature)
        {
            LastSuppressionState = $"{signal.ReasonCode}: unchanged.";
            return null;
        }

        if (timestamp - lastSpokenAt < cooldown)
        {
            LastSuppressionState = $"{signal.ReasonCode}: suppressed in cooldown.";
            return null;
        }

        lastStateSignature = signature;
        lastSpokenAt = timestamp;
        LastSuppressionState = $"{signal.ReasonCode}: spoken.";
        return signal.Message;
    }

    private static string BuildSignature(SessionStrategy strategy)
    {
        return $"{strategy.Fuel.RiskLevel}|{strategy.Pit.Recommendation}|{strategy.TyreRisk.RiskLevel}";
    }
}
