using RaceEngineer.Core.Events;

namespace RaceEngineer.Core.Voice;

public sealed class CalloutManager
{
    private readonly Dictionary<EventType, DateTimeOffset> lastSpokenAt = [];
    private readonly Dictionary<EventType, int> suppressedCounts = [];

    public string LastSuppressionState { get; private set; } = "No callouts suppressed.";

    public string? TryCreateCallout(TelemetryEvent item)
    {
        var text = CalloutText(item);
        if (text is null)
        {
            return null;
        }

        if (IsInCooldown(item.Type, item.Timestamp))
        {
            suppressedCounts[item.Type] = GetSuppressedCount(item.Type) + 1;
            LastSuppressionState = $"{item.Type}: suppressed {suppressedCounts[item.Type]} in cooldown.";
            return null;
        }

        lastSpokenAt[item.Type] = item.Timestamp;
        suppressedCounts[item.Type] = 0;
        LastSuppressionState = $"{item.Type}: ready.";
        return text;
    }

    public int GetSuppressedCount(EventType type)
    {
        return suppressedCounts.TryGetValue(type, out var count) ? count : 0;
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

    private bool IsInCooldown(EventType type, DateTimeOffset timestamp)
    {
        if (!lastSpokenAt.TryGetValue(type, out var lastSpoken))
        {
            return false;
        }

        return timestamp - lastSpoken < CooldownFor(type);
    }

    private static TimeSpan CooldownFor(EventType type)
    {
        return type switch
        {
            EventType.LowFuel => TimeSpan.FromSeconds(25),
            EventType.InvalidLapOrFlags => TimeSpan.FromSeconds(20),
            EventType.TyreOverheating or EventType.BrakeOverheating => TimeSpan.FromSeconds(20),
            EventType.AbruptBrakeRelease or EventType.UnstableBraking => TimeSpan.FromSeconds(12),
            EventType.EarlyThrottleWithSteering or EventType.TractionLoss or EventType.SteeringOveruse => TimeSpan.FromSeconds(15),
            _ => TimeSpan.FromSeconds(20)
        };
    }
}

