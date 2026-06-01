using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

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
        return ResolveGuideWithSource(cachedGuide, knowledgeSources, trackName).Guide;
    }

    public static (TrackGuide? Guide, string Source) ResolveGuideWithSource(
        TrackGuide? cachedGuide,
        IEnumerable<KnowledgeSource>? knowledgeSources = null,
        string? trackName = null)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return (null, "none");
        }

        TrackGuide? catalogGuide = null;
        if (TrackGuideWebCatalog.TryGetGuide(trackName, out var catalog))
        {
            catalogGuide = catalog;
        }

        if (cachedGuide?.Corners is { Count: > 0 }
            && !UsesZonePlaceholderCorners(cachedGuide)
            && TrackGuideMatcher.Matches(cachedGuide, trackName))
        {
            return (cachedGuide, "cached");
        }

        if (catalogGuide is not null)
        {
            return (catalogGuide, cachedGuide?.Corners is { Count: > 0 } ? "catalog-over-cache" : "catalog");
        }

        if (cachedGuide?.Corners is { Count: > 0 } && TrackGuideMatcher.Matches(cachedGuide, trackName))
        {
            return (cachedGuide, "cached-zone-placeholders");
        }

        var fromSources = TrackResearchService.ResolveGuideFromSources(knowledgeSources ?? [], trackName);
        if (fromSources?.Corners is { Count: > 0 } && !UsesZonePlaceholderCorners(fromSources))
        {
            return (fromSources, "knowledge");
        }

        if (cachedGuide is not null && TrackGuideMatcher.Matches(cachedGuide, trackName))
        {
            return (cachedGuide, "cached-empty");
        }

        return (null, "none");
    }

    public static bool UsesZonePlaceholderCorners(TrackGuide? guide) =>
        guide?.Corners is { Count: > 0 } corners
        && corners.Any(corner => ZoneTokenPattern.IsMatch(corner.Name));

    public static bool ContainsZoneToken(string? text) =>
        !string.IsNullOrWhiteSpace(text) && ZoneTokenPattern.IsMatch(text);

    public static void LogMappingAudit(
        string path,
        string? trackName,
        TrackGuide? guide,
        string guideSource,
        string original,
        string mapped)
    {
        var mapperExecuted = guide?.Corners is { Count: > 0 };
        TrackGuideZoneMappingDiagnosticLog.RaiseAudit(new TrackGuideZoneMappingAuditTrace(
            path,
            trackName,
            guideSource,
            guide?.TrackName,
            guide?.Corners?.Count ?? 0,
            UsesZonePlaceholderCorners(guide),
            original,
            mapped,
            mapperExecuted,
            ContainsZoneToken(mapped)));
    }

    public static string? ResolveTrackNameFromSnapshots(IEnumerable<TelemetrySnapshot>? snapshots)
    {
        if (snapshots is null)
        {
            return null;
        }

        foreach (var snapshot in snapshots.Reverse())
        {
            var trackName = FirstNonEmpty(
                NullIfWhitespace(snapshot.RaceAwareness?.TrackName),
                NullIfWhitespace(snapshot.RaceAwareness?.CircuitId));
            if (trackName is not null)
            {
                return TrackGuideCatalogIdentity.CanonicalName(trackName);
            }
        }

        return null;
    }

    public static string? ResolveReviewSessionTrack(
        string? bundleTrack,
        string? browserTrack,
        IEnumerable<TelemetrySnapshot>? snapshots) =>
        CanonicalizeTrackName(FirstNonEmpty(
            NullIfWhitespace(bundleTrack),
            NullIfWhitespace(string.Equals(browserTrack, "-", StringComparison.Ordinal) ? null : browserTrack),
            ResolveTrackNameFromSnapshots(snapshots)));

    public static string? CanonicalizeTrackName(string? trackName) =>
        TrackGuideCatalogIdentity.CanonicalName(trackName);

    public static string? ResolveTrackName(
        LiveRaceContext? raceContext = null,
        RacePrepPlan? prepPlan = null,
        SessionState? session = null,
        SessionMemorySummary? storedMemory = null,
        string? reviewSessionTrack = null,
        IEnumerable<TelemetrySnapshot>? snapshots = null) =>
        ResolveTrackNameWithTrace(
            raceContext,
            prepPlan,
            session,
            storedMemory,
            reviewSessionTrack,
            snapshots).TrackName;

    public static (string? TrackName, TrackGuideResolutionTrace Trace) ResolveTrackNameWithTrace(
        LiveRaceContext? raceContext = null,
        RacePrepPlan? prepPlan = null,
        SessionState? session = null,
        SessionMemorySummary? storedMemory = null,
        string? reviewSessionTrack = null,
        IEnumerable<TelemetrySnapshot>? snapshots = null,
        TrackGuide? cachedGuide = null,
        IEnumerable<KnowledgeSource>? knowledgeSources = null,
        bool raiseDiagnostic = false)
    {
        var raceContextTrack = CanonicalizeTrackName(raceContext?.TrackName);
        var prepTrack = CanonicalizeTrackName(prepPlan?.Track);
        var reviewTrack = CanonicalizeTrackName(reviewSessionTrack);
        var storedMemoryTrack = CanonicalizeTrackName(storedMemory?.TrackName);
        var snapshotTrack = ResolveTrackNameFromSnapshots(snapshots);
        var sessionTrack = CanonicalizeTrackName(session?.LatestSnapshot?.RaceAwareness?.TrackName)
            ?? CanonicalizeTrackName(session?.LatestSnapshot?.RaceAwareness?.CircuitId);

        var trackName = FirstNonEmpty(
            reviewTrack,
            raceContextTrack,
            prepTrack,
            storedMemoryTrack,
            snapshotTrack,
            sessionTrack);

        var (guide, guideSource) = ResolveGuideWithSource(cachedGuide, knowledgeSources, trackName);
        var guideLookup = string.IsNullOrWhiteSpace(trackName)
            ? "no-track-name"
            : TrackGuideWebCatalog.TryGetGuide(trackName, out _)
                ? $"catalog-match:{trackName}"
                : $"catalog-miss:{trackName}";
        var trackConfidence = ComputeTrackConfidence(
            reviewTrack,
            raceContextTrack,
            prepTrack,
            storedMemoryTrack,
            snapshotTrack,
            sessionTrack,
            trackName);
        var guideConfidence = ComputeGuideConfidence(guide, guideSource, trackName);
        var guideStatus = string.IsNullOrWhiteSpace(trackName)
            ? "track-unresolved"
            : guide is null
                ? "guide-unavailable"
                : "available";

        var trace = new TrackGuideResolutionTrace(
            trackName,
            prepTrack,
            reviewTrack,
            raceContextTrack,
            storedMemoryTrack,
            snapshotTrack,
            sessionTrack,
            guideLookup,
            guide?.TrackName,
            guideSource,
            trackConfidence,
            guideConfidence,
            guideStatus);

        if (raiseDiagnostic)
        {
            TrackGuideZoneMappingDiagnosticLog.RaiseResolution(trace);
        }

        return (trackName, trace);
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

    public static string MapZoneReferences(string text, TrackGuide? guide, string? diagnosticField = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (guide?.Corners is not { Count: > 0 })
        {
            if (diagnosticField is not null && ZoneTokenPattern.IsMatch(text))
            {
                LogMapping(diagnosticField, text, text, guide, "none");
            }

            return text;
        }

        var mapped = ZoneTokenPattern.Replace(text, match =>
        {
            if (!int.TryParse(match.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoneNumber)
                || !TryMapZoneReference(zoneNumber, guide, null, out var corner))
            {
                return match.Value;
            }

            return corner;
        });

        if (diagnosticField is not null && !string.Equals(text, mapped, StringComparison.Ordinal))
        {
            LogMapping(diagnosticField, text, mapped, guide, DescribeGuideSource(guide));
        }

        return mapped;
    }

    public static CoachMessage MapCoachMessage(CoachMessage message, TrackGuide? guide, string? auditPath = null)
    {
        var mappedContent = MapZoneReferences(message.Content, guide, auditPath is null ? null : $"{auditPath}.Content");
        var mapped = message with
        {
            Content = mappedContent,
            Uncertainty = string.IsNullOrWhiteSpace(message.Uncertainty)
                ? message.Uncertainty
                : MapZoneReferences(message.Uncertainty, guide),
            EvidencePackets = message.EvidencePackets
                .Select((packet, index) => packet with
                {
                    Explanation = MapZoneReferences(
                        packet.Explanation,
                        guide,
                        auditPath is null ? null : $"{auditPath}.Evidence[{index}]")
                })
                .ToArray()
        };

        if (auditPath is not null)
        {
            LogMappingAudit(auditPath, null, guide, DescribeGuideSource(guide), message.Content, mapped.Content);
        }

        return mapped;
    }

    public static DriverCoachingRecommendation MapDriverCoachingRecommendation(
        DriverCoachingRecommendation coaching,
        TrackGuide? guide,
        string diagnosticPrefix = "DriverCoaching")
    {
        if (!coaching.HasData)
        {
            return coaching;
        }

        if (guide?.Corners is not { Count: > 0 })
        {
            var probe = coaching.WeakestArea
                ?? coaching.Summary
                ?? coaching.TopCoachingTargets.FirstOrDefault();
            if (ContainsZoneToken(probe))
            {
                LogMapping(
                    $"{diagnosticPrefix}.NoGuide",
                    probe ?? string.Empty,
                    probe ?? string.Empty,
                    guide,
                    "none");
            }

            return coaching;
        }

        var mapped = coaching with
        {
            BiggestWeakness = MapOptionalField($"{diagnosticPrefix}.BiggestWeakness", coaching.BiggestWeakness, guide),
            StrongestArea = MapOptionalField($"{diagnosticPrefix}.StrongestArea", coaching.StrongestArea, guide),
            WeakestArea = MapOptionalField($"{diagnosticPrefix}.WeakestArea", coaching.WeakestArea, guide),
            ConsistencySummary = MapOptionalField($"{diagnosticPrefix}.ConsistencySummary", coaching.ConsistencySummary, guide),
            PreviousSessionDeltaSummary = MapOptionalField($"{diagnosticPrefix}.PreviousSessionDeltaSummary", coaching.PreviousSessionDeltaSummary, guide),
            ProgressTrendSummary = MapOptionalField($"{diagnosticPrefix}.ProgressTrendSummary", coaching.ProgressTrendSummary, guide),
            Summary = MapOptionalField($"{diagnosticPrefix}.Summary", coaching.Summary, guide) ?? coaching.Summary,
            RepeatedWeaknesses = MapTextItems(coaching.RepeatedWeaknesses, guide, $"{diagnosticPrefix}.RepeatedWeakness"),
            TopCoachingTargets = MapTextItems(coaching.TopCoachingTargets, guide, $"{diagnosticPrefix}.TopCoachingTarget"),
            CoachingInsights = MapTextItems(coaching.CoachingInsights, guide, $"{diagnosticPrefix}.CoachingInsight")
        };

        LogMappingAudit(
            diagnosticPrefix,
            guide.TrackName,
            guide,
            DescribeGuideSource(guide),
            coaching.WeakestArea ?? coaching.Summary ?? string.Empty,
            mapped.WeakestArea ?? mapped.Summary ?? string.Empty);

        return mapped;
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

    public static IReadOnlyList<string> MapTextItems(
        IEnumerable<string> items,
        TrackGuide? guide,
        string? diagnosticFieldPrefix = null) =>
        items.Select((item, index) => MapZoneReferences(
            item,
            guide,
            diagnosticFieldPrefix is null ? null : $"{diagnosticFieldPrefix}[{index}]"))
            .ToArray();

    private static PerformanceQualityMetric MapQualityMetric(PerformanceQualityMetric metric, TrackGuide? guide) =>
        metric with { Detail = MapZoneReferences(metric.Detail, guide) };

    private static string? MapOptionalField(string field, string? text, TrackGuide? guide) =>
        string.IsNullOrWhiteSpace(text) ? text : MapZoneReferences(text, guide, field);

    private static string? MapOptionalText(string? text, TrackGuide? guide) =>
        string.IsNullOrWhiteSpace(text) ? text : MapZoneReferences(text, guide);

    private static string DescribeGuideSource(TrackGuide? guide) =>
        guide?.ProviderName ?? guide?.TrackName ?? "none";

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static string ComputeTrackConfidence(
        string? reviewTrack,
        string? raceContextTrack,
        string? prepTrack,
        string? storedMemoryTrack,
        string? snapshotTrack,
        string? sessionTrack,
        string? resolvedTrack)
    {
        if (string.IsNullOrWhiteSpace(resolvedTrack))
        {
            return "none";
        }

        if (!string.IsNullOrWhiteSpace(reviewTrack)
            || !string.IsNullOrWhiteSpace(snapshotTrack)
            || !string.IsNullOrWhiteSpace(raceContextTrack))
        {
            return "high";
        }

        if (!string.IsNullOrWhiteSpace(prepTrack) || !string.IsNullOrWhiteSpace(storedMemoryTrack))
        {
            return "medium";
        }

        return !string.IsNullOrWhiteSpace(sessionTrack) ? "low" : "none";
    }

    private static string ComputeGuideConfidence(TrackGuide? guide, string guideSource, string? trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName) || guide is null)
        {
            return "none";
        }

        if (!TrackGuideMatcher.Matches(guide, trackName))
        {
            return "none";
        }

        return guideSource switch
        {
            "catalog" or "catalog-over-cache" or "cached" => "high",
            "knowledge" => "medium",
            _ => "low"
        };
    }

    private static void LogMapping(
        string field,
        string original,
        string mapped,
        TrackGuide? guide,
        string guideSource)
    {
        TrackGuideZoneMappingDiagnosticLog.Raise(new TrackGuideZoneMappingTrace(
            field,
            original,
            mapped,
            guide?.TrackName,
            guideSource));
    }

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
