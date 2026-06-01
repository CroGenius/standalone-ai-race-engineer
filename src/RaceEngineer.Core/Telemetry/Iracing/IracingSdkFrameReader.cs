using System.Globalization;
using IRSDKSharper;

namespace RaceEngineer.Core.Telemetry.Iracing;

public static class IracingSdkFrameReader
{
    private const int MaxCars = 64;

    public static IracingTelemetryFrame? TryRead(IRacingSdkData data)
    {
        if (data.SessionInfo is null)
        {
            return null;
        }

        var playerCarIdx = SafeInt(data, "PlayerCarIdx") ?? 0;
        if (playerCarIdx < 0 || playerCarIdx >= MaxCars)
        {
            return null;
        }

        var sessionInfo = data.SessionInfo;
        var weekend = sessionInfo.WeekendInfo;
        var driver = ResolveDriver(sessionInfo, playerCarIdx);
        var session = ResolveCurrentSession(sessionInfo);
        var position = SafeInt(data, "CarIdxPosition", playerCarIdx) ?? SafeInt(data, "Position");
        var classPosition = SafeInt(data, "CarIdxClassPosition", playerCarIdx) ?? SafeInt(data, "ClassPosition");
        var totalCars = CountActiveCars(data) ?? SafeInt(data, "SessionNumDrivers");
        var (gapAhead, carAheadName) = ResolveGapAhead(data, playerCarIdx, sessionInfo);
        var (gapBehind, carBehindName) = ResolveGapBehind(data, playerCarIdx, position, sessionInfo);
        var incidents = SumIncidents(data);
        var sessionFlagsValue = SafeUInt(data, "SessionFlags");
        var sessionFlags = FormatFlags(sessionFlagsValue);

        return new IracingTelemetryFrame(
            DateTimeOffset.UtcNow,
            new IracingCarTelemetry(
                SafeDouble(data, "Speed"),
                SafeDouble(data, "RPM"),
                SafeInt(data, "Gear"),
                SafeDouble(data, "Throttle"),
                SafeDouble(data, "Brake"),
                NormalizeSteering(SafeDouble(data, "SteeringWheelAngle")),
                SafeDouble(data, "FuelLevel")),
            new IracingSessionTelemetry(
                weekend?.TrackName,
                ResolveTrackDisplayName(sessionInfo),
                ResolveCarName(driver),
                (string?)driver?.CarClassShortName ?? (string?)driver?.CarClassRelSpeed,
                ResolveSessionType(session),
                SafeInt(data, "SessionNum"),
                (int?)session?.SessionLaps,
                SafeDouble(data, "SessionTimeRemain"),
                sessionFlagsValue is null ? null : (int?)sessionFlagsValue.Value),
            new IracingRaceTelemetry(
                position,
                classPosition,
                totalCars,
                SafeInt(data, "Lap"),
                SafeDouble(data, "LapCurrentLapTime"),
                SafeDouble(data, "LapDistPct"),
                gapAhead,
                gapBehind,
                carAheadName,
                carBehindName,
                incidents),
            new IracingPitTelemetry(
                SafeBool(data, "OnPitRoad"),
                SafeBool(data, "PlayerCarInPitStall"),
                SafeBool(data, "PitstopActive")),
            new IracingFlagsTelemetry(
                sessionFlags,
                FormatFlags(SafeUInt(data, "PlayerCarDriverAlert"))));
    }

    public static IracingConnectionDiagnostics BuildDiagnostics(IRacingSdkData? data, bool sdkConnected)
    {
        if (!sdkConnected || data is null)
        {
            return IracingConnectionDiagnostics.Disconnected(IracingProviderDiagnostics.SessionNotActive);
        }

        var sessionInfo = data.SessionInfo;
        var playerCarIdx = SafeInt(data, "PlayerCarIdx");
        var driver = playerCarIdx is >= 0 and < MaxCars
            ? ResolveDriver(sessionInfo, playerCarIdx.Value)
            : null;
        var weekend = sessionInfo?.WeekendInfo;
        var trackName = ResolveTrackDisplayName(sessionInfo) ?? weekend?.TrackName;
        var driverName = ResolveDriverName(driver);
        var carName = ResolveCarName(driver);
        var sessionActive = sessionInfo is not null
            && (!string.IsNullOrWhiteSpace(trackName) || SafeInt(data, "SessionNum").HasValue);

        return new IracingConnectionDiagnostics(
            SdkConnected: true,
            SessionActive: sessionActive,
            DriverDetected: !string.IsNullOrWhiteSpace(driverName),
            TrackDetected: !string.IsNullOrWhiteSpace(trackName),
            DriverName: driverName,
            TrackName: trackName,
            CarName: carName);
    }

    internal static string? ResolveTrackDisplayName(IRacingSdkSessionInfo? sessionInfo)
    {
        var weekend = sessionInfo?.WeekendInfo;
        if (weekend is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(weekend.TrackDisplayName))
        {
            return weekend.TrackDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(weekend.TrackDisplayShortName))
        {
            return weekend.TrackDisplayShortName.Trim();
        }

        var parts = new[] { weekend.TrackCity, weekend.TrackCountry }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    internal static dynamic? ResolveDriver(IRacingSdkSessionInfo? sessionInfo, int playerCarIdx)
    {
        var drivers = sessionInfo?.DriverInfo?.Drivers;
        if (drivers is null || drivers.Count == 0)
        {
            return null;
        }

        foreach (var driver in drivers)
        {
            if (driver.CarIdx == playerCarIdx)
            {
                return driver;
            }
        }

        return playerCarIdx >= 0 && playerCarIdx < drivers.Count
            ? drivers[playerCarIdx]
            : drivers[0];
    }

    private static dynamic? ResolveCurrentSession(IRacingSdkSessionInfo? sessionInfo)
    {
        var sessions = sessionInfo?.SessionInfo?.Sessions;
        if (sessions is null || sessions.Count == 0)
        {
            return null;
        }

        var currentSessionNum = sessionInfo?.SessionInfo?.CurrentSessionNum;
        if (currentSessionNum is >= 0)
        {
            foreach (var session in sessions)
            {
                if (session.SessionNum == currentSessionNum)
                {
                    return session;
                }
            }
        }

        return sessions[^1];
    }

    private static string? ResolveSessionType(dynamic? session) =>
        session is null ? null : (string?)session.SessionType;

    private static string? ResolveCarName(dynamic? driver)
    {
        if (driver is null)
        {
            return null;
        }

        return FirstNonEmpty(
            (string?)driver.CarScreenName,
            (string?)driver.CarPath,
            (string?)driver.CarDesignName,
            (string?)driver.CarClassShortName);
    }

    private static string? ResolveDriverName(dynamic? driver)
    {
        if (driver is null)
        {
            return null;
        }

        return FirstNonEmpty((string?)driver.UserName, (string?)driver.AbbrevName);
    }

    private static (double? GapSeconds, string? CarName) ResolveGapAhead(
        IRacingSdkData data,
        int playerCarIdx,
        IRacingSdkSessionInfo? sessionInfo)
    {
        var gapAhead = SafeDouble(data, "CarIdxF2Time", playerCarIdx);
        if (gapAhead is null or <= 0)
        {
            return (null, null);
        }

        var playerPosition = SafeInt(data, "CarIdxPosition", playerCarIdx);
        if (playerPosition is null or <= 0)
        {
            return (gapAhead, null);
        }

        for (var carIdx = 0; carIdx < MaxCars; carIdx++)
        {
            var position = SafeInt(data, "CarIdxPosition", carIdx);
            if (position == playerPosition - 1)
            {
                var driver = ResolveDriver(sessionInfo, carIdx);
                return (gapAhead, FirstNonEmpty(ResolveDriverName(driver), (string?)driver?.CarScreenName));
            }
        }

        return (gapAhead, null);
    }

    private static (double? GapSeconds, string? CarName) ResolveGapBehind(
        IRacingSdkData data,
        int playerCarIdx,
        int? playerPosition,
        IRacingSdkSessionInfo? sessionInfo)
    {
        if (playerPosition is null or <= 0)
        {
            return (null, null);
        }

        for (var carIdx = 0; carIdx < MaxCars; carIdx++)
        {
            if (carIdx == playerCarIdx)
            {
                continue;
            }

            var position = SafeInt(data, "CarIdxPosition", carIdx);
            if (position != playerPosition + 1)
            {
                continue;
            }

            var gapBehind = SafeDouble(data, "CarIdxF2Time", carIdx);
            var driver = ResolveDriver(sessionInfo, carIdx);
            return (gapBehind is > 0 ? gapBehind : null, FirstNonEmpty(ResolveDriverName(driver), (string?)driver?.CarScreenName));
        }

        return (null, null);
    }

    private static int? CountActiveCars(IRacingSdkData data)
    {
        var count = 0;
        for (var carIdx = 0; carIdx < MaxCars; carIdx++)
        {
            var position = SafeInt(data, "CarIdxPosition", carIdx);
            if (position is > 0)
            {
                count++;
            }
        }

        return count == 0 ? null : count;
    }

    private static int? SumIncidents(IRacingSdkData data)
    {
        var myIncidents = SafeInt(data, "PlayerCarMyIncidentCount") ?? 0;
        var teamIncidents = SafeInt(data, "PlayerCarTeamIncidentCount") ?? 0;
        var total = myIncidents + teamIncidents;
        return total == 0 ? SafeInt(data, "PlayerCarMyIncidentCount") : total;
    }

    private static double? NormalizeSteering(double? steeringWheelAngleRadians)
    {
        if (steeringWheelAngleRadians is null)
        {
            return null;
        }

        const double maxRadians = 2.5d;
        var normalized = steeringWheelAngleRadians.Value / maxRadians;
        return Math.Clamp(normalized, -1d, 1d);
    }

    private static string? FormatFlags(uint? flags)
    {
        if (flags is null)
        {
            return null;
        }

        return $"0x{flags.Value.ToString("X", CultureInfo.InvariantCulture)}";
    }

    private static double? SafeDouble(IRacingSdkData data, string name, int index = 0)
    {
        try
        {
            if (!data.TelemetryDataProperties.ContainsKey(name))
            {
                return null;
            }

            var value = data.GetFloat(name, index);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return null;
            }

            return value;
        }
        catch
        {
            return null;
        }
    }

    private static int? SafeInt(IRacingSdkData data, string name, int index = 0)
    {
        try
        {
            if (!data.TelemetryDataProperties.ContainsKey(name))
            {
                return null;
            }

            return data.GetInt(name, index);
        }
        catch
        {
            return null;
        }
    }

    private static uint? SafeUInt(IRacingSdkData data, string name, int index = 0)
    {
        try
        {
            if (!data.TelemetryDataProperties.ContainsKey(name))
            {
                return null;
            }

            return data.GetBitField(name, index);
        }
        catch
        {
            return null;
        }
    }

    private static bool? SafeBool(IRacingSdkData data, string name, int index = 0)
    {
        try
        {
            if (!data.TelemetryDataProperties.ContainsKey(name))
            {
                return null;
            }

            return data.GetBool(name, index);
        }
        catch
        {
            return null;
        }
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
