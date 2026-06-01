namespace RaceEngineer.Core.Knowledge;

public sealed record TrackGuideZoneMappingTrace(
    string Field,
    string Original,
    string Mapped,
    string? GuideTrackName,
    string GuideSource);

public sealed record TrackGuideZoneMappingAuditTrace(
    string Path,
    string? TrackName,
    string GuideSource,
    string? GuideTrackName,
    int CornerCount,
    bool UsesZonePlaceholderCorners,
    string Original,
    string Mapped,
    bool MapperExecuted,
    bool StillContainsZoneToken);

public sealed record TrackGuideResolutionTrace(
    string? TrackName,
    string? PrepTrack,
    string? ReviewTrack,
    string? RaceContextTrack,
    string? StoredMemoryTrack,
    string? SnapshotTrack,
    string? SessionTrack,
    string GuideLookup,
    string? GuideResolved,
    string GuideSource,
    string TrackConfidence,
    string GuideConfidence,
    string GuideStatus);

public static class TrackGuideZoneMappingDiagnosticLog
{
    public static event Action<TrackGuideZoneMappingTrace>? TraceRaised;
    public static event Action<TrackGuideZoneMappingAuditTrace>? AuditRaised;
    public static event Action<TrackGuideResolutionTrace>? ResolutionRaised;

    public static void Raise(TrackGuideZoneMappingTrace trace) => TraceRaised?.Invoke(trace);

    public static void RaiseAudit(TrackGuideZoneMappingAuditTrace trace) => AuditRaised?.Invoke(trace);

    public static void RaiseResolution(TrackGuideResolutionTrace trace) => ResolutionRaised?.Invoke(trace);
}
