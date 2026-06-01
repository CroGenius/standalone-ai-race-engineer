using RaceEngineer.Core;

namespace RaceEngineer.Core.Telemetry;

public sealed record TelemetryProviderFactoryResult(
    ITelemetryProvider Provider,
    IReadOnlyList<string> Warnings);

public interface ITelemetryProviderFactory
{
    TelemetryProviderFactoryResult Create(AppSettings settings);
}
