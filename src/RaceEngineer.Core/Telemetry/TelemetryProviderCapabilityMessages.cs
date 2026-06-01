namespace RaceEngineer.Core.Telemetry;

public static class TelemetryProviderCapabilityMessages
{
    public static string OpponentDataUnavailable(TelemetryProviderCapabilities? capabilities) =>
        capabilities is not null && !capabilities.OpponentGaps
            ? capabilities.ExplainUnavailable(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps)
            : "Opponent data is unavailable from telemetry.";

    public static string GapAheadUnavailable(TelemetryProviderCapabilities? capabilities) =>
        capabilities is not null && !capabilities.OpponentGaps
            ? capabilities.ExplainUnavailable(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps)
            : "Opponent gap ahead data is unavailable from telemetry.";

    public static string GapBehindUnavailable(TelemetryProviderCapabilities? capabilities) =>
        capabilities is not null && !capabilities.OpponentGaps
            ? capabilities.ExplainUnavailable(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps)
            : "Opponent gap behind data is unavailable from telemetry.";

    public static string GapTrendAheadUnavailable(TelemetryProviderCapabilities? capabilities) =>
        capabilities is not null && !capabilities.OpponentGaps
            ? $"Gap trend to the car ahead is unavailable because the current {capabilities.ProviderDisplayName} provider does not expose opponent gaps."
            : "Gap trend to the car ahead is unavailable from telemetry.";

    public static string GapTrendBehindUnavailable(TelemetryProviderCapabilities? capabilities) =>
        capabilities is not null && !capabilities.OpponentGaps
            ? $"Gap trend to the car behind is unavailable because the current {capabilities.ProviderDisplayName} provider does not expose opponent gaps."
            : "Gap trend to the car behind is unavailable from telemetry.";

    public static string GapTrendSummaryUnavailable(TelemetryProviderCapabilities? capabilities) =>
        capabilities is not null && !capabilities.OpponentGaps
            ? $"Gap trend is unavailable because the current {capabilities.ProviderDisplayName} provider does not expose opponent gaps."
            : "Gap trend is unavailable from telemetry.";
}
