using System.Globalization;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.SessionContext;

public sealed class SessionContextClassifier
{
    private const double StationarySpeedKmh = 8.0;
    private const double PitLaneSpeedKmh = 75.0;
    private const int StableLapSampleMinimum = 2;

    public SessionContextAssessment Classify(SessionContextInput input)
    {
        if (input.IsReviewMode)
        {
            return BuildReviewAssessment(input);
        }

        var phase = InferPhase(input.PrepPlan?.SessionType);
        var activity = InferActivity(input.Session, input.RecentSnapshots);
        var hasStableLapSamples = HasStableLapSamples(input.Session);
        var confidence = ScoreConfidence(input.Session, phase, activity, hasStableLapSamples);
        var onTrackRaceReady = phase == SessionPhase.Race
            && activity == VehicleActivity.OnTrack
            && confidence >= StrategyConfidenceLevel.Medium;

        return new SessionContextAssessment(
            phase,
            activity,
            BuildSessionModeLabel(phase, activity),
            confidence,
            confidence.ToString(),
            hasStableLapSamples,
            AllowFuelRiskCallouts: onTrackRaceReady && activity is not VehicleActivity.Stationary and not VehicleActivity.PitLane,
            AllowPitStrategyCallouts: onTrackRaceReady && confidence >= StrategyConfidenceLevel.Medium,
            AllowTyreWarningCallouts: onTrackRaceReady,
            AllowUnsolicitedStrategyCallouts: onTrackRaceReady && confidence >= StrategyConfidenceLevel.Medium,
            AllowLowFuelVoiceCallouts: onTrackRaceReady && activity is VehicleActivity.OnTrack or VehicleActivity.InLap,
            AllowDrivingCallouts: activity is VehicleActivity.OnTrack or VehicleActivity.OutLap or VehicleActivity.InLap,
            SuppressionNote: BuildSuppressionNote(phase, activity, hasStableLapSamples, confidence));
    }

    private static SessionContextAssessment BuildReviewAssessment(SessionContextInput input)
    {
        var hasStableLapSamples = HasStableLapSamples(input.Session);
        var confidence = hasStableLapSamples && input.Session.FuelUsedPerLap.HasValue
            ? StrategyConfidenceLevel.Medium
            : StrategyConfidenceLevel.Low;

        return new SessionContextAssessment(
            SessionPhase.Review,
            VehicleActivity.Unknown,
            "Review",
            confidence,
            confidence.ToString(),
            hasStableLapSamples,
            AllowFuelRiskCallouts: false,
            AllowPitStrategyCallouts: true,
            AllowTyreWarningCallouts: true,
            AllowUnsolicitedStrategyCallouts: false,
            AllowLowFuelVoiceCallouts: false,
            AllowDrivingCallouts: false,
            SuppressionNote: "Review mode — strategy answers only when asked.");
    }

    private static SessionPhase InferPhase(string? sessionType)
    {
        if (string.IsNullOrWhiteSpace(sessionType))
        {
            return SessionPhase.Unknown;
        }

        var normalized = sessionType.Trim();
        if (ContainsAny(normalized, "race", "stint", "endurance", "sprint"))
        {
            return SessionPhase.Race;
        }

        if (ContainsAny(normalized, "qual", "q1", "q2", "q3", "hotlap", "hot lap"))
        {
            return SessionPhase.Qualifying;
        }

        if (ContainsAny(normalized, "practice", "practise", "test", "warmup", "warm-up", "training"))
        {
            return SessionPhase.Practice;
        }

        return SessionPhase.Unknown;
    }

    private static VehicleActivity InferActivity(SessionState session, IReadOnlyList<TelemetrySnapshot>? recentSnapshots)
    {
        var snapshot = session.LatestSnapshot;
        if (snapshot is null)
        {
            return VehicleActivity.Unknown;
        }

        var speed = snapshot.Car.SpeedKmh ?? 0;
        if (speed < StationarySpeedKmh)
        {
            return VehicleActivity.Stationary;
        }

        var lapProgress = NormalizeLapProgress(snapshot.Lap.LapProgress);
        var recent = (recentSnapshots ?? []).TakeLast(6).ToArray();
        if (recent.Length == 0 && session.LatestSnapshot is not null)
        {
            recent = [session.LatestSnapshot];
        }

        if (WasRecentlyStationary(recent) && lapProgress < 0.30 && speed < 130)
        {
            return VehicleActivity.OutLap;
        }

        if (lapProgress > 0.88 && speed < 110)
        {
            return VehicleActivity.InLap;
        }

        if (IsPitLaneLike(snapshot, recent, speed))
        {
            return VehicleActivity.PitLane;
        }

        return VehicleActivity.OnTrack;
    }

    private static bool IsPitLaneLike(TelemetrySnapshot snapshot, IReadOnlyList<TelemetrySnapshot> recent, double speed)
    {
        if (speed >= PitLaneSpeedKmh)
        {
            return false;
        }

        var throttle = snapshot.Inputs.Throttle ?? 0;
        var brake = snapshot.Inputs.Brake ?? 0;
        if (speed < PitLaneSpeedKmh && throttle < 0.35 && brake < 0.45)
        {
            return true;
        }

        if (recent.Count >= 3)
        {
            var averageSpeed = recent.Average(item => item.Car.SpeedKmh ?? 0);
            if (averageSpeed < 55)
            {
                return true;
            }
        }

        return FlagsIndicatePit(snapshot.Race.Flags);
    }

    private static bool WasRecentlyStationary(IReadOnlyList<TelemetrySnapshot> recent)
    {
        return recent.TakeLast(4).Any(item => (item.Car.SpeedKmh ?? 0) < StationarySpeedKmh);
    }

    private static bool HasStableLapSamples(SessionState session)
    {
        return session.CompletedLaps.Count >= StableLapSampleMinimum
            && session.FuelUsedPerLap.HasValue;
    }

    private static StrategyConfidenceLevel ScoreConfidence(
        SessionState session,
        SessionPhase phase,
        VehicleActivity activity,
        bool hasStableLapSamples)
    {
        if (activity is VehicleActivity.Stationary or VehicleActivity.PitLane or VehicleActivity.Unknown)
        {
            return StrategyConfidenceLevel.Low;
        }

        if (!hasStableLapSamples)
        {
            return StrategyConfidenceLevel.Low;
        }

        if (phase is SessionPhase.Practice or SessionPhase.Qualifying or SessionPhase.Unknown)
        {
            return StrategyConfidenceLevel.Medium;
        }

        return session.CompletedLaps.Count >= 3 && session.FuelUsedPerLap.HasValue
            ? StrategyConfidenceLevel.High
            : StrategyConfidenceLevel.Medium;
    }

    private static string BuildSessionModeLabel(SessionPhase phase, VehicleActivity activity)
    {
        return $"{FormatPhase(phase)} / {FormatActivity(activity)}";
    }

    private static string BuildSuppressionNote(
        SessionPhase phase,
        VehicleActivity activity,
        bool hasStableLapSamples,
        StrategyConfidenceLevel confidence)
    {
        if (activity is VehicleActivity.Stationary or VehicleActivity.PitLane)
        {
            return "Stationary or pit-lane — race fuel and pit callouts paused.";
        }

        if (!hasStableLapSamples)
        {
            return "Waiting for stable lap samples.";
        }

        if (phase is SessionPhase.Practice or SessionPhase.Qualifying or SessionPhase.Unknown)
        {
            return "Non-race session — unsolicited strategy callouts suppressed.";
        }

        if (confidence == StrategyConfidenceLevel.Low)
        {
            return "Not enough race data yet.";
        }

        return "Race context active.";
    }

    private static string FormatPhase(SessionPhase phase)
    {
        return phase switch
        {
            SessionPhase.Practice => "Practice",
            SessionPhase.Qualifying => "Qualifying",
            SessionPhase.Race => "Race",
            SessionPhase.Review => "Review",
            _ => "Session"
        };
    }

    private static string FormatActivity(VehicleActivity activity)
    {
        return activity switch
        {
            VehicleActivity.OnTrack => "On track",
            VehicleActivity.PitLane => "Pit lane",
            VehicleActivity.OutLap => "Out lap",
            VehicleActivity.InLap => "In lap",
            VehicleActivity.Stationary => "Stationary",
            _ => "Unknown"
        };
    }

    private static double NormalizeLapProgress(double? progress)
    {
        if (progress is not { } value)
        {
            return 0;
        }

        if (value > 1.0 && value <= 100.0)
        {
            return value / 100.0;
        }

        return Math.Clamp(value, 0, 1);
    }

    private static bool FlagsIndicatePit(object? flags)
    {
        var text = flags?.ToString();
        return !string.IsNullOrWhiteSpace(text)
            && text.Contains("pit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
