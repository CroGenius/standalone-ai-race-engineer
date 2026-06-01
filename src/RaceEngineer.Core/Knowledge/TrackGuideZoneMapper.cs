using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Knowledge;

public static class TrackGuideZoneMapper
{
    private static readonly Regex ZoneTokenPattern = new(
        @"\bzone\s*(?<num>\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static TrackGuide? ResolveGuide(
        TrackGuide? cachedGuide,
        IEnumerable<KnowledgeSource>? knowledgeSources = null,
        string? trackName = null)
    {
        if (cachedGuide?.Corners is { Count: > 0 })
        {
            return cachedGuide;
        }

        if (!string.IsNullOrWhiteSpace(trackName)
            && TrackGuideWebCatalog.TryGetGuide(trackName, out var catalogGuide))
        {
            return catalogGuide;
        }

        var fromSources = TrackResearchService.ResolveGuideFromSources(knowledgeSources ?? []);
        if (fromSources?.Corners is { Count: > 0 })
        {
            return fromSources;
        }

        return cachedGuide ?? fromSources;
    }

    public static string MapZoneLabel(PerformanceZone zone, TrackGuide? guide)
    {
        if (TryMapZoneReference(zone.ZoneNumber, guide, zone, out var mapped))
        {
            return mapped;
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
            && TryMapZoneReference(zoneNumber, guide, null, out var mapped))
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
            && TryMapZoneReference(zoneNumber, guide, null, out var mapped))
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
                || !TryMapZoneReference(zoneNumber, guide, null, out var mapped))
            {
                return match.Value;
            }

            return mapped;
        });
    }

    public static CoachMessage MapCoachMessage(CoachMessage message, TrackGuide? guide)
    {
        if (guide?.Corners is not { Count: > 0 })
        {
            return message;
        }

        return message with
        {
            Content = MapZoneReferences(message.Content, guide),
            Uncertainty = string.IsNullOrWhiteSpace(message.Uncertainty)
                ? message.Uncertainty
                : MapZoneReferences(message.Uncertainty, guide),
            EvidencePackets = message.EvidencePackets
                .Select(packet => packet with { Explanation = MapZoneReferences(packet.Explanation, guide) })
                .ToArray()
        };
    }

    public static DriverCoachingRecommendation MapDriverCoachingRecommendation(
        DriverCoachingRecommendation coaching,
        TrackGuide? guide)
    {
        if (!coaching.HasData || guide?.Corners is not { Count: > 0 })
        {
            return coaching;
        }

        return coaching with
        {
            BiggestWeakness = MapOptionalText(coaching.BiggestWeakness, guide),
            StrongestArea = MapOptionalText(coaching.StrongestArea, guide),
            WeakestArea = MapOptionalText(coaching.WeakestArea, guide),
            ConsistencySummary = MapOptionalText(coaching.ConsistencySummary, guide),
            PreviousSessionDeltaSummary = MapOptionalText(coaching.PreviousSessionDeltaSummary, guide),
            ProgressTrendSummary = MapOptionalText(coaching.ProgressTrendSummary, guide),
            Summary = MapOptionalText(coaching.Summary, guide) ?? coaching.Summary,
            RepeatedWeaknesses = MapTextItems(coaching.RepeatedWeaknesses, guide),
            TopCoachingTargets = MapTextItems(coaching.TopCoachingTargets, guide),
            CoachingInsights = MapTextItems(coaching.CoachingInsights, guide)
        };
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

        var mappedWeakness = MapOptionalText(performance.MainWeakness, guide);

        var mappedBiggest = performance.BiggestTimeLoss is null
            ? null
            : performance.BiggestTimeLoss with
            {
                Zone = performance.BiggestTimeLoss.Zone with
                {
                    Label = MapZoneLabel(performance.BiggestTimeLoss.Zone, guide)
                },
                Behaviors = MapTextItems(performance.BiggestTimeLoss.Behaviors, guide)
            };

        return performance with
        {
            ZoneMetrics = mappedMetrics,
            MistakeClusters = mappedClusters,
            CoachingMessages = mappedMessages,
            MainWeakness = mappedWeakness,
            BiggestTimeLoss = mappedBiggest,
            BrakingQuality = MapQualityMetric(performance.BrakingQuality, guide),
            ThrottleQuality = MapQualityMetric(performance.ThrottleQuality, guide),
            Consistency = MapQualityMetric(performance.Consistency, guide)
        };
    }

    public static IReadOnlyList<string> MapTextItems(IEnumerable<string> items, TrackGuide? guide) =>
        items.Select(item => MapZoneReferences(item, guide)).ToArray();

    private static PerformanceQualityMetric MapQualityMetric(PerformanceQualityMetric metric, TrackGuide? guide) =>
        metric with { Detail = MapZoneReferences(metric.Detail, guide) };

    private static string? MapOptionalText(string? text, TrackGuide? guide) =>
        string.IsNullOrWhiteSpace(text) ? text : MapZoneReferences(text, guide);

    private static bool TryMapZoneReference(
        int zoneNumber,
        TrackGuide? guide,
        PerformanceZone? zone,
        out string cornerName)
    {
        if (TryMapZoneNumber(zoneNumber, guide, out cornerName))
        {
            return true;
        }

        if (zone is not null && TryMapByProgress(zone, guide, out cornerName))
        {
            return true;
        }

        return TryMapByEstimatedProgress(zoneNumber, guide, out cornerName);
    }

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
        return TryMapProgressMidpoint(mid, guide, out cornerName);
    }

    private static bool TryMapByEstimatedProgress(int zoneNumber, TrackGuide? guide, out string cornerName)
    {
        cornerName = string.Empty;
        if (guide?.Corners is not { Count: > 0 } corners || zoneNumber < 1)
        {
            return false;
        }

        var mid = (zoneNumber - 0.5) / Math.Max(zoneNumber, corners.Count);
        return TryMapProgressMidpoint(mid, guide, out cornerName);
    }

    private static bool TryMapProgressMidpoint(double mid, TrackGuide? guide, out string cornerName)
    {
        cornerName = string.Empty;
        if (guide?.Corners is not { Count: > 0 } corners)
        {
            return false;
        }

        foreach (var corner in corners)
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

        TrackGuideCorner? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var corner in corners)
        {
            if (corner.LapProgressStart is not { } start || corner.LapProgressEnd is not { } end)
            {
                continue;
            }

            var cornerMid = (start + end) / 2.0;
            var distance = Math.Abs(mid - cornerMid);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearest = corner;
        }

        if (nearest is null)
        {
            return false;
        }

        cornerName = nearest.Name;
        return true;
    }
}
