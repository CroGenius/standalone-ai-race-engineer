using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Telemetry.Iracing;

public static class IracingTelemetryMapper
{
    public static TelemetrySnapshot Map(IracingTelemetryFrame frame)
    {
        var lapTimeMs = frame.Race.LapCurrentTimeSeconds is { } lapTimeSeconds
            ? (int?)Math.Round(lapTimeSeconds * 1000d)
            : null;

        var pitState = BuildPitState(frame.Pit);
        var flags = FirstNonEmpty(frame.Flags.SessionFlags, frame.Flags.PlayerCarFlags);
        var trackName = FirstNonEmpty(frame.Session.TrackDisplayName, frame.Session.TrackName);

        return new TelemetrySnapshot(
            Guid.NewGuid(),
            frame.Timestamp,
            new SourceInfo("iracing", "iracing.telemetry", 1),
            new CarState(
                frame.Car.SpeedMps is { } speedMps ? speedMps * 3.6d : null,
                frame.Car.Rpm,
                frame.Car.Gear,
                frame.Race.Incidents),
            new DriverInputs(
                frame.Car.Throttle,
                frame.Car.Brake,
                frame.Car.Steering),
            new LapState(
                lapTimeMs,
                frame.Race.LapCurrentTimeSeconds,
                frame.Race.LapDistPct,
                frame.Race.Lap,
                null),
            new RaceState(
                frame.Race.Position ?? frame.Race.ClassPosition,
                flags),
            new TyreBrakeFuelState(
                frame.Car.FuelLevel,
                null,
                null,
                null,
                null),
            new RaceAwarenessState(
                trackName,
                frame.Session.TrackName,
                frame.Session.CarName,
                frame.Session.CarClass,
                frame.Session.SessionType,
                frame.Race.Lap,
                frame.Session.SessionLapsTotal,
                frame.Race.Position ?? frame.Race.ClassPosition,
                frame.Race.TotalCars,
                frame.Race.GapAheadSeconds,
                frame.Race.GapBehindSeconds,
                frame.Race.CarAheadName,
                frame.Race.CarBehindName,
                pitState,
                flags,
                null,
                frame.Session.SessionTimeRemainingSeconds,
                null));
    }

    private static string? BuildPitState(IracingPitTelemetry pit)
    {
        if (pit.InPitStall == true)
        {
            return "in pit stall";
        }

        if (pit.PitStopActive == true)
        {
            return "pit stop active";
        }

        if (pit.OnPitRoad == true)
        {
            return "on pit road";
        }

        if (pit.OnPitRoad == false && pit.InPitStall == false)
        {
            return "on track";
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
