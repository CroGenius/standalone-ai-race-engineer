namespace RaceEngineer.Core.Analytics;

public enum TyreWarmupState
{
    Unknown,
    Cold,
    Warming,
    OptimalWindow,
    Overheating,
    Fading
}

public enum TyreReadiness
{
    Unknown,
    NotReady,
    Building,
    ReadyToPush,
    PushNow,
    Overheated,
    Fading
}

public enum GripConfidenceLevel
{
    Low,
    Medium,
    High
}

public sealed record TyreAxleAssessment(
    string Label,
    double? AverageTempC,
    double? MinTempC,
    double? MaxTempC,
    TyreWarmupState WarmupState,
    string Summary);

public sealed record SessionTyreIntelligence(
    bool HasReliableData,
    string Availability,
    TyreWarmupState WarmupState,
    TyreReadiness Readiness,
    GripConfidenceLevel GripConfidence,
    string GripConfidenceLabel,
    string OverheatingRisk,
    string CoachingMessage,
    string PushGuidance,
    int? EstimatedCornersUntilReady,
    bool AvoidHeavyInputs,
    bool PushSafe,
    IReadOnlyList<TyreAxleAssessment> Axles,
    IReadOnlyList<string> EvidenceLines)
{
    public static SessionTyreIntelligence Unavailable(string reason) => new(
        false,
        reason,
        TyreWarmupState.Unknown,
        TyreReadiness.Unknown,
        GripConfidenceLevel.Low,
        "Low",
        "Unknown",
        "Tyre data is not reliable yet.",
        "Wait for tyre temperature telemetry before pushing.",
        null,
        true,
        false,
        [],
        []);
}

public sealed record TyreIntelligenceInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? RecentSnapshots = null,
    IReadOnlyList<Events.TelemetryEvent>? Events = null,
    SessionContext.VehicleActivity Activity = SessionContext.VehicleActivity.Unknown,
    SessionContext.SessionPhase Phase = SessionContext.SessionPhase.Unknown,
    SessionTelemetryAnalytics? Analytics = null,
    SessionLapIntelligence? LapIntelligence = null);

public sealed class TyreIntelligenceOptions
{
    public double ColdTempThresholdC { get; init; } = 65.0;
    public double OptimalTempThresholdC { get; init; } = 85.0;
    public double OverheatTempThresholdC { get; init; } = 105.0;
    public double AggressiveBrakeThreshold { get; init; } = 0.75;
    public double AggressiveThrottleThreshold { get; init; } = 0.80;
}
