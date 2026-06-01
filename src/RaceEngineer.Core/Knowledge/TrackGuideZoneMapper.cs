using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Analytics;

namespace RaceEngineer.Core.Knowledge;

public static class TrackGuideZoneMapper
{
    private static readonly Regex ZoneTokenPattern = new(
        @"\bzone\s*(?<num>\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string MapZoneLabel(PerformanceZone zone, TrackGuide? guide)
    {
        if (TryMapZoneNumber(zone.ZoneNumber, guide, out var byIndex))
        {
            return byIndex;
        }

        if (TryMapByProgress(zone, guide, out var byProgress))
        {
            return byProgress;
        }

        return zone.Label;
    }

    public static string MapZoneLabel(string zoneLabel, TrackGuide? guide)
    {
        if (string.IsNullOrWhiteSpace(zoneLabel))
        {
            return zoneLabel;
        }

        var match = ZoneTokenPattern.Match(zoneLabel);
        if (match.Success
            && int.TryParse(match.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoneNumber)
            && TryMapZoneNumber(zoneNumber, guide, out var mapped))
        {
            return mapped;
        }

        return zoneLabel;
    }

    public static string MapLocationReference(string location, TrackGuide? guide)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return string.Empty;
        }

        var trimmed = location.Trim();
        var zoneMatch = ZoneTokenPattern.Match(trimmed);
        if (zoneMatch.Success
            && int.TryParse(zoneMatch.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoneNumber)
            && TryMapZoneNumber(zoneNumber, guide, out var mapped))
        {
            return mapped;
        }

        var turnMatch = Regex.Match(trimmed, @"\b(?:turn|t)\s*(?<num>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (turnMatch.Success
            && int.TryParse(turnMatch.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turnNumber)
            && guide?.Corners is { Count: > 0 } corners)
        {
            var namedTurn = corners.FirstOrDefault(corner =>
                corner.Name.Contains($"Turn {turnNumber}", StringComparison.OrdinalIgnoreCase)
                || corner.Name.Contains($"T{turnNumber}", StringComparison.OrdinalIgnoreCase));
            if (namedTurn is not null)
            {
                return namedTurn.Name;
            }
        }

        if (guide?.Corners is { Count: > 0 })
        {
            var directCorner = guide.Corners.FirstOrDefault(corner =>
                trimmed.Contains(corner.Name, StringComparison.OrdinalIgnoreCase));
            if (directCorner is not null)
            {
                return directCorner.Name;
            }
        }

        return trimmed;
    }

    public static string MapZoneReferences(string text, TrackGuide? guide)
    {
        if (string.IsNullOrWhiteSpace(text) || guide?.Corners is not { Count: > 0 })
        {
            return text;
        }

        return ZoneTokenPattern.Replace(text, match =>
        {
            if (!int.TryParse(match.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoneNumber)
                || !TryMapZoneNumber(zoneNumber, guide, out var mapped))
            {
                return match.Value;
            }

            return mapped;
        });
    }

    public static SessionDriverPerformance MapPerformance(SessionDriverPerformance performance, TrackGuide? guide)
    {
        if (guide?.Corners is not { Count: > 0 })
        {
            return performance;
        }

        var mappedMetrics = performance.ZoneMetrics
            .Select(metric => metric with
            {
                Zone = metric.Zone with { Label = MapZoneLabel(metric.Zone, guide) }
            })
            .ToArray();

        var mappedClusters = performance.MistakeClusters
            .Select(cluster => cluster with { ZoneLabel = MapZoneReferences(cluster.ZoneLabel, guide) })
            .ToArray();

        var mappedMessages = performance.CoachingMessages
            .Select(message => MapZoneReferences(message, guide))
            .ToArray();

        var mappedWeakness = string.IsNullOrWhiteSpace(performance.MainWeakness)
            ? performance.MainWeakness
            : MapZoneReferences(performance.MainWeakness, guide);

        var mappedBiggest = performance.BiggestTimeLoss is null
            ? null
            : performance.BiggestTimeLoss with
            {
                Zone = performance.BiggestTimeLoss.Zone with
                {
                    Label = MapZoneLabel(performance.BiggestTimeLoss.Zone, guide)
                }
            };

        return performance with
        {
            ZoneMetrics = mappedMetrics,
            MistakeClusters = mappedClusters,
            CoachingMessages = mappedMessages,
            MainWeakness = mappedWeakness,
            BiggestTimeLoss = mappedBiggest
        };
    }

    public static IReadOnlyList<string> MapTextItems(IEnumerable<string> items, TrackGuide? guide) =>
        items.Select(item => MapZoneReferences(item, guide)).ToArray();

    private static bool TryMapZoneNumber(int zoneNumber, TrackGuide? guide, out string cornerName)
    {
        cornerName = string.Empty;
        if (guide?.Corners is not { Count: > 0 } corners)
        {
            return false;
        }

        if (zoneNumber < 1 || zoneNumber > corners.Count)
        {
            return false;
        }

        cornerName = corners[zoneNumber - 1].Name;
        return true;
    }

    private static bool TryMapByProgress(PerformanceZone zone, TrackGuide? guide, out string cornerName)
    {
        cornerName = string.Empty;
        if (guide?.Corners is not { Count: > 0 })
        {
            return false;
        }

        var mid = (zone.ProgressStart + zone.ProgressEnd) / 2.0;
        foreach (var corner in guide.Corners)
        {
            if (corner.LapProgressStart is { } start
                && corner.LapProgressEnd is { } end
                && mid >= start
                && mid <= end)
            {
                cornerName = corner.Name;
                return true;
            }
        }

        return false;
    }
}
