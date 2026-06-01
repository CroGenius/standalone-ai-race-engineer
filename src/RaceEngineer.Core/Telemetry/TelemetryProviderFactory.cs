using RaceEngineer.Core;
using RaceEngineer.Core.Telemetry.Iracing;

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
            "iracing" =>
                CreateIracingProvider(),
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

    private static TelemetryProviderFactoryResult CreateIracingProvider()
    {
        var session = IracingTelemetrySessionFactory.CreateDefault();
        var provider = new IracingTelemetryProvider(session);
        var warnings = IracingTelemetryProvider.CreateStartupWarnings(session);
        return new TelemetryProviderFactoryResult(provider, warnings);
    }
}
