using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Events;

public sealed class EventEngine
{
    private const double MinimumConfidence = 0.60;
    private readonly Queue<TelemetrySnapshot> history = new();
    private readonly Dictionary<EventType, DateTimeOffset> lastEmittedAt = [];
    private readonly Dictionary<EventType, int> suppressedCounts = [];
    private int? currentLap;
    private DateTimeOffset? lapStartTimestamp;
    private int throttleHesitationTicks;

    public IReadOnlyList<TelemetryEvent> Process(TelemetrySnapshot snapshot)
    {
        var events = new List<TelemetryEvent>();
        var previous = history.Count > 0 ? history.Last() : null;

        if (currentLap is null && snapshot.Lap.LapProgress is not null)
        {
            currentLap = snapshot.Lap.LapNumber ?? 1;
            lapStartTimestamp = snapshot.Timestamp;
        }

        if (previous?.Lap.LapProgress is { } previousProgress && snapshot.Lap.LapProgress is { } progress)
        {
            if (IsLapWrap(previousProgress, progress))
            {
                var completedLap = currentLap ?? snapshot.Lap.LapNumber ?? 1;
                var duration = LapDuration(snapshot, lapStartTimestamp);
                events.Add(Create(
                    snapshot,
                    EventType.LapEnd,
                    EventSeverity.Info,
                    0.95,
                    new Dictionary<string, object?>
                    {
                        ["lap_number"] = completedLap,
                        ["previous_progress"] = previousProgress,
                        ["lap_time_ms"] = snapshot.Lap.LapTimeMs,
                        ["lap_time_s"] = snapshot.Lap.LapTimeS,
                        ["duration_seconds"] = duration?.TotalSeconds,
                        ["lap_started_at"] = lapStartTimestamp?.ToString("O"),
                        ["wrap_previous_progress_threshold"] = 0.95,
                        ["wrap_current_progress_threshold"] = 0.10,
                        ["wrap_drop_threshold"] = 0.80
                    },
                    "Review the completed lap for repeatable gains.",
                    "High-to-low lap_progress wrap passed deterministic lap boundary thresholds.",
                    completedLap));

                currentLap = completedLap + 1;
                lapStartTimestamp = snapshot.Timestamp;
                events.Add(Create(
                    snapshot,
                    EventType.LapStart,
                    EventSeverity.Info,
                    0.95,
                    new Dictionary<string, object?>
                    {
                        ["lap_number"] = currentLap,
                        ["started_at"] = lapStartTimestamp.Value.ToString("O"),
                        ["previous_progress"] = previousProgress,
                        ["current_progress"] = progress
                    },
                    "Build the next lap cleanly from the entry phase.",
                    "Lap start follows a validated lap_progress wrap.",
                    currentLap));
            }
        }

        var brake = snapshot.Inputs.Brake;
        var throttle = snapshot.Inputs.Throttle;
        var steering = snapshot.Inputs.Steering;
        var speed = snapshot.Car.SpeedKmh;

        if (brake >= 0.82 && speed >= 80)
        {
            TryAddEvent(events, snapshot, EventType.HeavyBraking, EventSeverity.Info, 0.82, new Dictionary<string, object?> { ["brake"] = brake, ["brake_threshold"] = 0.82, ["speed_kmh"] = speed, ["speed_threshold_kmh"] = 80 }, "Keep the initial hit firm, then release progressively.", "Brake and speed crossed heavy braking thresholds.");
        }

        if (previous?.Inputs.Brake is { } previousBrake && brake is { } currentBrake)
        {
            var brakeDelta = currentBrake - previousBrake;
            if (previousBrake >= 0.55 && brakeDelta <= -0.45)
            {
                TryAddEvent(events, snapshot, EventType.AbruptBrakeRelease, EventSeverity.Warning, 0.82, new Dictionary<string, object?> { ["previous_brake"] = previousBrake, ["previous_brake_threshold"] = 0.55, ["brake"] = currentBrake, ["brake_delta"] = brakeDelta, ["brake_delta_threshold"] = -0.45 }, "Smooth the brake trail-off to keep the platform settled.", "Brake release delta crossed abrupt-release threshold.");
            }

            if (Math.Abs(brakeDelta) >= 0.28 && previousBrake >= 0.25 && currentBrake >= 0.25)
            {
                TryAddEvent(events, snapshot, EventType.UnstableBraking, EventSeverity.Warning, 0.72, new Dictionary<string, object?> { ["previous_brake"] = previousBrake, ["brake"] = currentBrake, ["brake_delta"] = brakeDelta, ["abs_brake_delta_threshold"] = 0.28, ["active_brake_threshold"] = 0.25 }, "Hold brake pressure steadier before turn-in.", "Brake pressure changed rapidly while still in an active braking phase.");
            }
        }

        if (throttle >= 0.25 && Math.Abs(steering ?? 0) >= 0.35 && brake <= 0.08)
        {
            TryAddEvent(events, snapshot, EventType.EarlyThrottleWithSteering, EventSeverity.Warning, 0.78, new Dictionary<string, object?> { ["throttle"] = throttle, ["throttle_threshold"] = 0.25, ["steering"] = steering, ["abs_steering_threshold"] = 0.35, ["brake"] = brake, ["brake_release_threshold"] = 0.08 }, "Wait for steering to unwind before adding more throttle.", "Throttle was applied while steering lock remained above threshold.");
        }

        if (Math.Abs(steering ?? 0) >= 0.72 && speed >= 90)
        {
            TryAddEvent(events, snapshot, EventType.SteeringOveruse, EventSeverity.Warning, 0.70, new Dictionary<string, object?> { ["steering"] = steering, ["abs_steering_threshold"] = 0.72, ["speed_kmh"] = speed, ["speed_threshold_kmh"] = 90 }, "Reduce steering angle and let the car rotate on entry.", "Steering angle was high at speed.");
        }

        if (previous?.Inputs.Throttle is { } previousThrottle && throttle is { } currentThrottle)
        {
            if (currentThrottle is >= 0.08 and <= 0.22 && Math.Abs(currentThrottle - previousThrottle) <= 0.04 && brake <= 0.05)
            {
                throttleHesitationTicks++;
            }
            else
            {
                throttleHesitationTicks = 0;
            }

            if (throttleHesitationTicks == 5)
            {
                TryAddEvent(events, snapshot, EventType.ThrottleHesitation, EventSeverity.Warning, 0.65, new Dictionary<string, object?> { ["throttle"] = currentThrottle, ["previous_throttle"] = previousThrottle, ["throttle_min_threshold"] = 0.08, ["throttle_max_threshold"] = 0.22, ["throttle_delta_threshold"] = 0.04, ["brake"] = brake, ["brake_threshold"] = 0.05, ["ticks"] = throttleHesitationTicks, ["ticks_threshold"] = 5 }, "Commit earlier once the car is pointed.", "Throttle stayed in the hesitation band for the configured sample count.");
            }
        }

        if (previous?.Car.SpeedKmh is { } previousSpeed && throttle >= 0.65 && Math.Abs(steering ?? 0) >= 0.28 && speed + 1.0 < previousSpeed)
        {
            TryAddEvent(events, snapshot, EventType.TractionLoss, EventSeverity.Warning, 0.62, new Dictionary<string, object?> { ["throttle"] = throttle, ["throttle_threshold"] = 0.65, ["steering"] = steering, ["abs_steering_threshold"] = 0.28, ["speed_kmh"] = speed, ["previous_speed_kmh"] = previousSpeed, ["speed_delta_kmh"] = speed - previousSpeed, ["speed_loss_threshold_kmh"] = -1.0 }, "Reduce throttle pickup until rear grip is stable.", "Heuristic: speed dropped while throttle and steering angle were both high.");
        }

        var maxTyreTemp = MaxOrNull(snapshot.Condition.TyreTempC);
        if (maxTyreTemp >= 105)
        {
            TryAddEvent(events, snapshot, EventType.TyreOverheating, EventSeverity.Warning, 0.90, new Dictionary<string, object?> { ["max_tyre_temp_c"] = maxTyreTemp, ["tyre_temp_threshold_c"] = 105, ["tyre_temp_c"] = FormatList(snapshot.Condition.TyreTempC) }, "Reduce sliding and protect the hot tyre.", "At least one tyre temperature exceeded the overheating threshold.");
        }

        var maxBrakeTemp = MaxOrNull(snapshot.Condition.BrakeTempC);
        if (maxBrakeTemp >= 850)
        {
            TryAddEvent(events, snapshot, EventType.BrakeOverheating, EventSeverity.Warning, 0.90, new Dictionary<string, object?> { ["max_brake_temp_c"] = maxBrakeTemp, ["brake_temp_threshold_c"] = 850, ["brake_temp_c"] = FormatList(snapshot.Condition.BrakeTempC) }, "Open the braking phase slightly and avoid dragging the pedal.", "At least one brake temperature exceeded the overheating threshold.");
        }

        if (snapshot.Condition.Fuel <= 3.0)
        {
            TryAddEvent(events, snapshot, EventType.LowFuel, EventSeverity.Critical, 0.86, new Dictionary<string, object?> { ["fuel"] = snapshot.Condition.Fuel, ["fuel_threshold"] = 3.0 }, "Fuel is tight; lift earlier into heavy braking zones.", "Fuel level crossed the low-fuel threshold.");
        }

        if (snapshot.Race.Flags is not null)
        {
            TryAddEvent(events, snapshot, EventType.InvalidLapOrFlags, EventSeverity.Warning, 0.80, new Dictionary<string, object?> { ["flags"] = snapshot.Race.Flags, ["flag_present_threshold"] = true }, "Respect race control and treat lap data as uncertain.", "Race-control flags are present in telemetry.");
        }

        history.Enqueue(snapshot);
        while (history.Count > 30)
        {
            history.Dequeue();
        }

        return events;
    }

    public int GetSuppressedCount(EventType type)
    {
        return suppressedCounts.TryGetValue(type, out var count) ? count : 0;
    }

    private void TryAddEvent(
        ICollection<TelemetryEvent> events,
        TelemetrySnapshot snapshot,
        EventType type,
        EventSeverity severity,
        double confidence,
        IReadOnlyDictionary<string, object?> evidence,
        string suggestedAction,
        string confidenceReason)
    {
        if (confidence < MinimumConfidence)
        {
            return;
        }

        if (IsInCooldown(type, snapshot.Timestamp))
        {
            suppressedCounts[type] = GetSuppressedCount(type) + 1;
            return;
        }

        events.Add(Create(snapshot, type, severity, confidence, evidence, suggestedAction, confidenceReason));
        lastEmittedAt[type] = snapshot.Timestamp;
        suppressedCounts[type] = 0;
    }

    private TelemetryEvent Create(
        TelemetrySnapshot snapshot,
        EventType type,
        EventSeverity severity,
        double confidence,
        IReadOnlyDictionary<string, object?> evidence,
        string suggestedAction,
        string confidenceReason,
        int? lapNumberOverride = null)
    {
        var lapNumber = lapNumberOverride ?? snapshot.Lap.LapNumber ?? currentLap;
        var enrichedEvidence = new Dictionary<string, object?>(evidence)
        {
            ["lap_progress"] = snapshot.Lap.LapProgress,
            ["lap_number"] = lapNumber,
            ["confidence"] = confidence,
            ["confidence_reason"] = confidenceReason,
            ["suppressed_count"] = GetSuppressedCount(type)
        };

        return new TelemetryEvent(
            Guid.NewGuid(),
            type,
            severity,
            confidence,
            snapshot.Timestamp,
            snapshot.Lap.LapProgress,
            lapNumber,
            enrichedEvidence,
            suggestedAction);
    }

    private bool IsInCooldown(EventType type, DateTimeOffset timestamp)
    {
        var cooldown = CooldownFor(type);
        if (cooldown == TimeSpan.Zero || !lastEmittedAt.TryGetValue(type, out var previousEmission))
        {
            return false;
        }

        return timestamp - previousEmission < cooldown;
    }

    private static TimeSpan CooldownFor(EventType type)
    {
        return type switch
        {
            EventType.HeavyBraking => TimeSpan.FromSeconds(2),
            EventType.UnstableBraking => TimeSpan.FromSeconds(4),
            EventType.AbruptBrakeRelease => TimeSpan.FromSeconds(4),
            EventType.ThrottleHesitation => TimeSpan.FromSeconds(5),
            EventType.EarlyThrottleWithSteering => TimeSpan.FromSeconds(5),
            EventType.SteeringOveruse => TimeSpan.FromSeconds(5),
            EventType.TractionLoss => TimeSpan.FromSeconds(5),
            EventType.TyreOverheating => TimeSpan.FromSeconds(10),
            EventType.BrakeOverheating => TimeSpan.FromSeconds(10),
            EventType.LowFuel => TimeSpan.FromSeconds(15),
            EventType.InvalidLapOrFlags => TimeSpan.FromSeconds(10),
            _ => TimeSpan.Zero
        };
    }

    private static bool IsLapWrap(double previousProgress, double progress)
    {
        return previousProgress >= 0.95 && progress <= 0.10 && previousProgress - progress >= 0.80;
    }

    private static TimeSpan? LapDuration(TelemetrySnapshot snapshot, DateTimeOffset? lapStart)
    {
        if (snapshot.Lap.LapTimeMs is { } lapTimeMs && lapTimeMs > 0)
        {
            return TimeSpan.FromMilliseconds(lapTimeMs);
        }

        if (snapshot.Lap.LapTimeS is { } lapTimeS && lapTimeS > 0)
        {
            return TimeSpan.FromSeconds(lapTimeS);
        }

        return lapStart.HasValue && snapshot.Timestamp > lapStart.Value
            ? snapshot.Timestamp - lapStart.Value
            : null;
    }

    private static double? MaxOrNull(IReadOnlyList<double?>? values)
    {
        return values?.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Max();
    }

    private static string? FormatList(IReadOnlyList<double?>? values)
    {
        return values is null
            ? null
            : string.Join(", ", values.Select(value => value?.ToString("0.###") ?? "null"));
    }
}
