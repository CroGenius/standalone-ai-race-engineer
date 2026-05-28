using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.SessionContext;

public enum SessionPhase
{
    Unknown,
    Practice,
    Qualifying,
    Race,
    Review
}

public enum VehicleActivity
{
    Unknown,
    OnTrack,
    PitLane,
    OutLap,
    InLap,
    Stationary
}

public enum StrategyConfidenceLevel
{
    Low,
    Medium,
    High
}

public sealed record SessionContextInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? RecentSnapshots = null,
    RacePrepPlan? PrepPlan = null,
    bool IsReviewMode = false);

public sealed record SessionContextAssessment(
    SessionPhase Phase,
    VehicleActivity Activity,
    string SessionModeLabel,
    StrategyConfidenceLevel StrategyConfidence,
    string StrategyConfidenceLabel,
    bool HasStableLapSamples,
    bool AllowFuelRiskCallouts,
    bool AllowPitStrategyCallouts,
    bool AllowTyreWarningCallouts,
    bool AllowUnsolicitedStrategyCallouts,
    bool AllowLowFuelVoiceCallouts,
    bool AllowDrivingCallouts,
    string SuppressionNote)
{
    public static SessionContextAssessment InitialLive { get; } = new(
        SessionPhase.Unknown,
        VehicleActivity.Unknown,
        "Live / Waiting",
        StrategyConfidenceLevel.Low,
        "Low",
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        "Waiting for telemetry.");
}
