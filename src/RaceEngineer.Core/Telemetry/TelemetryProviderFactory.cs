using RaceEngineer.Core;

namespace RaceEngineer.Core.Telemetry;

public sealed class TelemetryProviderFactory : ITelemetryProviderFactory
{
    public static TelemetryProviderFactory Instance { get; } = new();

    public TelemetryProviderFactoryResult Create(AppSettings settings)
    {
        var providerId = NormalizeProvider(settings.TelemetryProvider);
        return providerId switch
        {
            "simhub" => new TelemetryProviderFactoryResult(
                new SimHubTelemetryProvider(settings.UdpBindIp, settings.UdpPort),
                []),
            "iracing" => new TelemetryProviderFactoryResult(
                UnavailableTelemetryProvider.NotImplemented("iracing", "iRacing"),
                ["iRacing telemetry provider is not implemented yet."]),
            _ => new TelemetryProviderFactoryResult(
                new SimHubTelemetryProvider(settings.UdpBindIp, settings.UdpPort),
                [$"Unknown telemetry provider '{settings.TelemetryProvider}'. Using SimHub."])
        };
    }

    public static string NormalizeProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return "simhub";
        }

        return configuredProvider.Trim().ToLowerInvariant();
    }
}
