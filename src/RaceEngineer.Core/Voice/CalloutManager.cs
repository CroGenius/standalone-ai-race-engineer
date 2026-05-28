using RaceEngineer.Core.Events;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Voice;

public sealed class CalloutManager
{
    private readonly Dictionary<EventType, DateTimeOffset> lastSpokenAt = [];
    private readonly Dictionary<EventType, int> suppressedCounts = [];
    private readonly Dictionary<EventType, int> consecutiveSuppressions = [];
    private readonly Dictionary<EventType, EventSeverity> lastSpokenSeverity = [];
    private double cooldownMultiplier = 1.0;

    public string LastSuppressionState { get; private set; } = "No callouts suppressed.";

    public void ConfigureCalloutAggressiveness(string aggressiveness)
    {
        cooldownMultiplier = aggressiveness switch
        {
            "low" => 1.6,
            "high" => 0.75,
            _ => 1.0
        };
    }

    public void NoteExternalSuppression(string reason)
    {
        LastSuppressionState = reason;
    }

    public string? TryCreateCallout(
        TelemetryEvent item,
        SessionContextAssessment? context = null,
        CoachPreferencesRecord? preferences = null)
    {
        if (preferences?.MinimalEngineerEnabled == true && item.Severity != EventSeverity.Critical)
        {
            RecordSuppression(item.Type, "minimal-engineer");
            return null;
        }

        if (preferences?.QuietModeEnabled == true && IsTechniqueEvent(item.Type) && item.Severity != EventSeverity.Critical)
        {
            RecordSuppression(item.Type, "quiet-mode");
            return null;
        }

        if (!ShouldAllowCallout(item, context, preferences))
        {
            RecordSuppression(item.Type, "context");
            return null;
        }

        var text = CalloutText(item);
        if (text is null)
        {
            return null;
        }

        if (IsRepeatedSeverity(item))
        {
            RecordSuppression(item.Type, "unchanged-severity");
            return null;
        }

        if (IsInCooldown(item.Type, item.Timestamp, preferences))
        {
            RecordSuppression(item.Type, "cooldown");
            return null;
        }

        lastSpokenAt[item.Type] = item.Timestamp;
        lastSpokenSeverity[item.Type] = item.Severity;
        suppressedCounts[item.Type] = 0;
        consecutiveSuppressions[item.Type] = 0;
        LastSuppressionState = $"{item.Type}: ready.";
        return text;
    }

    public int GetSuppressedCount(EventType type)
    {
        return suppressedCounts.TryGetValue(type, out var count) ? count : 0;
    }

    private bool ShouldAllowCallout(
        TelemetryEvent item,
        SessionContextAssessment? context,
        CoachPreferencesRecord? preferences)
    {
        if (context is { AllowDrivingCallouts: false } && item.Type != EventType.LowFuel)
        {
            return false;
        }

        if (IsTechniqueEvent(item.Type) && context is not null)
        {
            var restrictedActivity = context.Activity is VehicleActivity.PitLane
                or VehicleActivity.OutLap
                or VehicleActivity.InLap
                or VehicleActivity.Stationary;
            var restrictedPhase = context.Phase is SessionPhase.Practice
                or SessionPhase.Qualifying
                or SessionPhase.Unknown;

            if (restrictedActivity || restrictedPhase)
            {
                if (item.Confidence < 0.80 || item.Severity < EventSeverity.Warning)
                {
                    return false;
                }
            }
        }

        if (item.Type == EventType.LowFuel)
        {
            if (context is { AllowLowFuelVoiceCallouts: false })
            {
                return false;
            }

            if (item.Evidence.TryGetValue("fuel", out var fuelValue)
                && fuelValue is IConvertible convertible
                && convertible.ToDouble(null) > 5.0)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsRepeatedSeverity(TelemetryEvent item)
    {
        if (!lastSpokenSeverity.TryGetValue(item.Type, out var previousSeverity))
        {
            return false;
        }

        return SeverityRank(item.Severity) <= SeverityRank(previousSeverity);
    }

    private void RecordSuppression(EventType type, string reason)
    {
        suppressedCounts[type] = GetSuppressedCount(type) + 1;
        consecutiveSuppressions[type] = consecutiveSuppressions.TryGetValue(type, out var count) ? count + 1 : 1;
        LastSuppressionState = $"{type}: suppressed ({reason}) x{suppressedCounts[type]}.";
    }

    private static bool IsTechniqueEvent(EventType type)
    {
        return type is EventType.AbruptBrakeRelease
            or EventType.UnstableBraking
            or EventType.EarlyThrottleWithSteering
            or EventType.TractionLoss
            or EventType.SteeringOveruse
            or EventType.ThrottleHesitation;
    }

    private static int SeverityRank(EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Critical => 3,
            EventSeverity.Warning => 2,
            _ => 1
        };
    }

    private static string? CalloutText(TelemetryEvent item)
    {
        return item.Type switch
        {
            EventType.LowFuel => "Fuel is low. Start saving.",
            EventType.InvalidLapOrFlags => "Race control active. Keep it clean.",
            EventType.TyreOverheating => "Tyres are overheating. Reduce entry slide.",
            EventType.BrakeOverheating => "Brakes are overheating. Avoid dragging the pedal.",
            EventType.AbruptBrakeRelease => "Brake release is unstable. Smooth the trail-off.",
            EventType.UnstableBraking => "Braking is unstable. Hold pressure steady.",
            EventType.EarlyThrottleWithSteering => "Throttle is early. Unwind steering first.",
            EventType.TractionLoss => "Traction is weak. Soften throttle pickup.",
            EventType.SteeringOveruse => "Steering angle is high. Reduce scrub.",
            _ => null
        };
    }

    private bool IsInCooldown(EventType type, DateTimeOffset timestamp, CoachPreferencesRecord? preferences)
    {
        if (!lastSpokenAt.TryGetValue(type, out var lastSpoken))
        {
            return false;
        }

        var multiplier = cooldownMultiplier;
        if (preferences?.QuietModeEnabled == true)
        {
            multiplier *= 2.5;
        }

        if (preferences?.MinimalEngineerEnabled == true)
        {
            multiplier *= 2.0;
        }

        if (consecutiveSuppressions.TryGetValue(type, out var consecutive) && consecutive > 0)
        {
            multiplier *= 1.0 + Math.Min(consecutive * 0.25, 1.5);
        }

        return timestamp - lastSpoken < CooldownFor(type) * multiplier;
    }

    private static TimeSpan CooldownFor(EventType type)
    {
        return type switch
        {
            EventType.LowFuel => TimeSpan.FromSeconds(30),
            EventType.InvalidLapOrFlags => TimeSpan.FromSeconds(24),
            EventType.TyreOverheating or EventType.BrakeOverheating => TimeSpan.FromSeconds(24),
            EventType.AbruptBrakeRelease or EventType.UnstableBraking => TimeSpan.FromSeconds(22),
            EventType.EarlyThrottleWithSteering or EventType.TractionLoss or EventType.SteeringOveruse => TimeSpan.FromSeconds(20),
            EventType.ThrottleHesitation => TimeSpan.FromSeconds(18),
            _ => TimeSpan.FromSeconds(20)
        };
    }
}
