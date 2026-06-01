namespace RaceEngineer.Core.Telemetry.Iracing;

public static class IracingTelemetrySessionFactory
{
    public static IIracingTelemetrySession CreateDefault() => new IracingSdkTelemetrySession();
}
