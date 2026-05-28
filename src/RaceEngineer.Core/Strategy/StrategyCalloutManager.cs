using RaceEngineer.Core.Profile;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Strategy;

public sealed class StrategyCalloutManager
{
    private TimeSpan cooldown = TimeSpan.FromSeconds(25);
    private string? lastStateSignature;
    private string? lastSpokenSignature;
    private DateTimeOffset lastSpokenAt;
    private int consecutiveSuppressions;

    public string LastSuppressionState { get; private set; } = "No strategy callouts suppressed.";

    public void ConfigureCooldown(TimeSpan configuredCooldown)
    {
        cooldown = configuredCooldown <= TimeSpan.Zero ? TimeSpan.FromSeconds(25) : configuredCooldown;
    }

    public string? TryCreateCallout(
        SessionStrategy strategy,
        DateTimeOffset timestamp,
        SessionContextAssessment? context = null,
        CoachPreferencesRecord? preferences = null)
    {
        if (strategy.CalloutSignal is not { } signal)
        {
            lastStateSignature = BuildSignature(strategy);
            LastSuppressionState = "Strategy stable.";
            return null;
        }

        if (context is { AllowUnsolicitedStrategyCallouts: false })
        {
            RecordSuppression(signal.ReasonCode, "context");
            return null;
        }

        if (context is { StrategyConfidence: StrategyConfidenceLevel.Low })
        {
            RecordSuppression(signal.ReasonCode, "low-confidence");
            return null;
        }

        if (preferences?.MinimalEngineerEnabled == true)
        {
            RecordSuppression(signal.ReasonCode, "minimal-engineer");
            return null;
        }

        var signature = BuildSignature(strategy);
        if (signature == lastStateSignature)
        {
            LastSuppressionState = $"{signal.ReasonCode}: unchanged.";
            return null;
        }

        if (signature == lastSpokenSignature)
        {
            RecordSuppression(signal.ReasonCode, "repeat");
            return null;
        }

        var effectiveCooldown = cooldown;
        if (preferences?.QuietModeEnabled == true)
        {
            effectiveCooldown += TimeSpan.FromSeconds(20);
        }

        if (consecutiveSuppressions > 0)
        {
            effectiveCooldown += TimeSpan.FromSeconds(Math.Min(consecutiveSuppressions * 5, 30));
        }

        if (timestamp - lastSpokenAt < effectiveCooldown)
        {
            RecordSuppression(signal.ReasonCode, "cooldown");
            return null;
        }

        lastStateSignature = signature;
        lastSpokenSignature = signature;
        lastSpokenAt = timestamp;
        consecutiveSuppressions = 0;
        LastSuppressionState = $"{signal.ReasonCode}: spoken.";
        return signal.Message;
    }

    private void RecordSuppression(string reasonCode, string reason)
    {
        consecutiveSuppressions++;
        LastSuppressionState = $"{reasonCode}: suppressed ({reason}).";
    }

    private static string BuildSignature(SessionStrategy strategy)
    {
        return $"{strategy.Fuel.RiskLevel}|{strategy.Pit.Recommendation}|{strategy.TyreRisk.RiskLevel}|{strategy.CalloutSignal?.ReasonCode}";
    }
}
