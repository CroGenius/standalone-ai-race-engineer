namespace RaceEngineer.Core.Telemetry;

public sealed record TelemetryProviderCapabilities(
    string ProviderId,
    string ProviderDisplayName,
    bool SpeedRpmGear,
    bool ThrottleBrakeSteering,
    bool LapTiming,
    bool Fuel,
    bool Tyres,
    bool TrackCarIdentity,
    bool Position,
    bool OpponentGaps,
    bool Standings,
    bool PitStatus,
    bool Flags,
    bool Incidents,
    bool SessionInfo)
{
    public static TelemetryProviderCapabilities SimHub { get; } = new(
        "simhub",
        "SimHub",
        SpeedRpmGear: true,
        ThrottleBrakeSteering: true,
        LapTiming: true,
        Fuel: true,
        Tyres: true,
        TrackCarIdentity: true,
        Position: true,
        OpponentGaps: false,
        Standings: false,
        PitStatus: false,
        Flags: false,
        Incidents: false,
        SessionInfo: false);

    public static TelemetryProviderCapabilities Iracing { get; } = new(
        "iracing",
        "iRacing",
        SpeedRpmGear: true,
        ThrottleBrakeSteering: true,
        LapTiming: true,
        Fuel: true,
        Tyres: false,
        TrackCarIdentity: true,
        Position: true,
        OpponentGaps: true,
        Standings: true,
        PitStatus: true,
        Flags: true,
        Incidents: true,
        SessionInfo: true);

    [Obsolete("Use TelemetryProviderCapabilities.Iracing.")]
    public static TelemetryProviderCapabilities IracingPlanned => Iracing;

    public IReadOnlyList<string> SupportedCapabilityLabels =>
        AllCapabilityLabels.Where(label => Supports(label)).ToArray();

    public IReadOnlyList<string> MissingCapabilityLabels =>
        AllCapabilityLabels.Where(label => !Supports(label)).ToArray();

    public string Summary =>
        SupportedCapabilityLabels.Count == 0
            ? "No live telemetry capabilities available."
            : string.Join(", ", SupportedCapabilityLabels);

    public string MissingSummary =>
        MissingCapabilityLabels.Count == 0
            ? "None"
            : string.Join(", ", MissingCapabilityLabels);

    public bool Supports(string capabilityLabel) =>
        capabilityLabel switch
        {
            CapabilityLabels.SpeedRpmGear => SpeedRpmGear,
            CapabilityLabels.ThrottleBrakeSteering => ThrottleBrakeSteering,
            CapabilityLabels.LapTiming => LapTiming,
            CapabilityLabels.Fuel => Fuel,
            CapabilityLabels.Tyres => Tyres,
            CapabilityLabels.TrackCarIdentity => TrackCarIdentity,
            CapabilityLabels.Position => Position,
            CapabilityLabels.OpponentGaps => OpponentGaps,
            CapabilityLabels.Standings => Standings,
            CapabilityLabels.PitStatus => PitStatus,
            CapabilityLabels.Flags => Flags,
            CapabilityLabels.Incidents => Incidents,
            CapabilityLabels.SessionInfo => SessionInfo,
            _ => false
        };

    public string ExplainUnavailable(string capabilityLabel) =>
        $"{capabilityLabel} is unavailable because the current {ProviderDisplayName} provider does not expose {capabilityLabel.ToLowerInvariant()}.";

    public static class CapabilityLabels
    {
        public const string SpeedRpmGear = "Speed/RPM/Gear";
        public const string ThrottleBrakeSteering = "Throttle/Brake/Steering";
        public const string LapTiming = "Lap timing";
        public const string Fuel = "Fuel";
        public const string Tyres = "Tyres";
        public const string TrackCarIdentity = "Track/Car identity";
        public const string Position = "Position";
        public const string OpponentGaps = "Opponent gaps";
        public const string Standings = "Standings";
        public const string PitStatus = "Pit status";
        public const string Flags = "Flags";
        public const string Incidents = "Incidents";
        public const string SessionInfo = "Session info";

        public static IReadOnlyList<string> All { get; } =
        [
            SpeedRpmGear,
            ThrottleBrakeSteering,
            LapTiming,
            Fuel,
            Tyres,
            TrackCarIdentity,
            Position,
            OpponentGaps,
            Standings,
            PitStatus,
            Flags,
            Incidents,
            SessionInfo
        ];
    }

    private static IReadOnlyList<string> AllCapabilityLabels => CapabilityLabels.All;
}
