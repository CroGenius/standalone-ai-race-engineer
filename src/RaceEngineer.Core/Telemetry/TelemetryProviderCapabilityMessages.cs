namespace RaceEngineer.Core.Telemetry;

public static class TelemetryProviderCapabilityMessages
{
    public static bool IsProviderUnavailable(TelemetryProviderStatus? status) =>
        status is TelemetryProviderStatus.Offline or TelemetryProviderStatus.Error;

    public static string ProviderUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status,
        string? diagnostics)
    {
        if (!IsProviderUnavailable(status) || capabilities is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(diagnostics)
            ? $"{capabilities.ProviderDisplayName} telemetry provider is unavailable."
            : $"{capabilities.ProviderDisplayName} telemetry provider is unavailable: {diagnostics}";
    }

    public static string LiveTelemetryUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status,
        string? diagnostics)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        return string.IsNullOrWhiteSpace(providerMessage)
            ? "Live telemetry is unavailable."
            : providerMessage;
    }

    public static string OpponentDataUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status = null,
        string? diagnostics = null)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return providerMessage;
        }

        return capabilities is not null && !capabilities.OpponentGaps
            ? capabilities.ExplainUnavailable(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps)
            : "Opponent data is unavailable from telemetry.";
    }

    public static string GapAheadUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status = null,
        string? diagnostics = null)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return providerMessage;
        }

        return capabilities is not null && !capabilities.OpponentGaps
            ? capabilities.ExplainUnavailable(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps)
            : "Opponent gap ahead data is unavailable from telemetry.";
    }

    public static string GapBehindUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status = null,
        string? diagnostics = null)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return providerMessage;
        }

        return capabilities is not null && !capabilities.OpponentGaps
            ? capabilities.ExplainUnavailable(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps)
            : "Opponent gap behind data is unavailable from telemetry.";
    }

    public static string GapTrendAheadUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status = null,
        string? diagnostics = null)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return providerMessage;
        }

        return capabilities is not null && !capabilities.OpponentGaps
            ? $"Gap trend to the car ahead is unavailable because the current {capabilities.ProviderDisplayName} provider does not expose opponent gaps."
            : "Gap trend to the car ahead is unavailable from telemetry.";
    }

    public static string GapTrendBehindUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status = null,
        string? diagnostics = null)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return providerMessage;
        }

        return capabilities is not null && !capabilities.OpponentGaps
            ? $"Gap trend to the car behind is unavailable because the current {capabilities.ProviderDisplayName} provider does not expose opponent gaps."
            : "Gap trend to the car behind is unavailable from telemetry.";
    }

    public static string GapTrendSummaryUnavailable(
        TelemetryProviderCapabilities? capabilities,
        TelemetryProviderStatus? status = null,
        string? diagnostics = null)
    {
        var providerMessage = ProviderUnavailable(capabilities, status, diagnostics);
        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return providerMessage;
        }

        return capabilities is not null && !capabilities.OpponentGaps
            ? $"Gap trend is unavailable because the current {capabilities.ProviderDisplayName} provider does not expose opponent gaps."
            : "Gap trend is unavailable from telemetry.";
    }
}
