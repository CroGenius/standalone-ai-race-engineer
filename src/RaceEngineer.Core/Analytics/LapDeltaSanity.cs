namespace RaceEngineer.Core.Analytics;

public static class LapDeltaSanity
{
    public const double DefaultMaxPointDeltaSeconds = 45.0;

    public static bool IsReasonableLapDelta(double? deltaSeconds, double? referenceLapDurationSeconds = null)
    {
        if (deltaSeconds is not { } delta)
        {
            return false;
        }

        if (double.IsNaN(delta) || double.IsInfinity(delta))
        {
            return false;
        }

        var limit = referenceLapDurationSeconds is { } duration and > 5
            ? Math.Max(15, Math.Min(DefaultMaxPointDeltaSeconds, duration * 0.75))
            : DefaultMaxPointDeltaSeconds;

        return Math.Abs(delta) <= limit;
    }

    public static double? ClampOrNull(double? deltaSeconds, double? referenceLapDurationSeconds = null) =>
        IsReasonableLapDelta(deltaSeconds, referenceLapDurationSeconds) ? deltaSeconds : null;
}
