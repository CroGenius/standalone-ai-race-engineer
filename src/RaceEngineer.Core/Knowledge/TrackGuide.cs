using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RaceEngineer.Core.Knowledge;

public sealed record TrackGuideSourceRef(string Label, string? Url);

public sealed record TrackGuideCorner(
    string Name,
    double? LapProgressStart,
    double? LapProgressEnd,
    string? Notes = null);

public sealed record TrackGuide(
    Guid Id,
    string TrackName,
    IReadOnlyList<string> Aliases,
    string? CountryOrLocation,
    string? Length,
    int? CornerCount,
    IReadOnlyList<string> SectorNotes,
    IReadOnlyList<string> MajorBrakingZones,
    IReadOnlyList<string> TractionZones,
    IReadOnlyList<string> HighSpeedSections,
    IReadOnlyList<string> OvertakingZones,
    IReadOnlyList<string> SetupPriorities,
    IReadOnlyList<string> TyreStressNotes,
    IReadOnlyList<string> TyreWarmupNotes,
    IReadOnlyList<string> TyreDegradationNotes,
    IReadOnlyList<string> FuelCharacteristics,
    IReadOnlyList<string> FuelStrategyNotes,
    IReadOnlyList<TrackGuideCorner> Corners,
    IReadOnlyList<TrackGuideSourceRef> Sources,
    DateTimeOffset FetchedAt,
    DateTimeOffset? RefreshedAt,
    string ProviderName)
{
    public int SourceCount => Sources.Count;

    public DateTimeOffset LastUpdatedAt => RefreshedAt ?? FetchedAt;

    public static Guid IdForTrack(string trackName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"track-guide:{NormalizeTrackKey(trackName)}"));
        Span<byte> idBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(idBytes);
        idBytes[6] = (byte)((idBytes[6] & 0x0F) | 0x40);
        idBytes[8] = (byte)((idBytes[8] & 0x3F) | 0x80);
        return new Guid(idBytes);
    }

    public static string NormalizeTrackKey(string trackName) =>
        trackName.Trim().ToLowerInvariant();
}

public sealed record TrackGuideLookupResult(
    TrackGuide? Guide,
    bool FromCache,
    string? UnavailableReason);

public sealed record TrackGuideFetchResult(
    TrackGuide? Guide,
    bool Saved,
    string? Message);

public static class TrackGuideMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public const string TitlePrefix = "Track guide:";

    public static KnowledgeSource ToKnowledgeSource(TrackGuide guide, string sourceType)
    {
        return new KnowledgeSource(
            guide.Id,
            sourceType,
            $"{TitlePrefix} {guide.TrackName}",
            guide.Sources.FirstOrDefault()?.Url,
            guide.LastUpdatedAt,
            Serialize(guide),
            BuildConfidenceNote(guide, sourceType),
            null,
            guide.TrackName,
            null,
            "track");
    }

    public static bool TryParse(KnowledgeSource source, out TrackGuide guide)
    {
        guide = null!;
        if (!source.Title.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(source.Category, "track", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TrackGuide>(source.Content, JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            guide = TrackGuideNormalizer.Normalize(parsed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string Serialize(TrackGuide guide) => JsonSerializer.Serialize(guide, JsonOptions);

    private static string BuildConfidenceNote(TrackGuide guide, string sourceType) =>
        sourceType switch
        {
            KnowledgeSourceTypes.Web =>
                $"Cached web track guide from {guide.ProviderName}; verify against current sim/track version.",
            KnowledgeSourceTypes.ManualNote =>
                "Local/manual track guide; verify against current sim/track version.",
            _ => "Cached track guide; verify against current sim/track version."
        };
}

public static class TrackGuideFormatter
{
    public static string ToCoachSummary(TrackGuide guide, string? focus = null)
    {
        var parts = new List<string>
        {
            $"Cached track guide for {guide.TrackName} (updated {guide.LastUpdatedAt:yyyy-MM-dd}, sources: {guide.SourceCount})."
        };

        if (!string.IsNullOrWhiteSpace(guide.CountryOrLocation))
        {
            parts.Add($"Location: {guide.CountryOrLocation}.");
        }

        if (!string.IsNullOrWhiteSpace(guide.Length))
        {
            parts.Add($"Length: {guide.Length}.");
        }

        if (guide.CornerCount is { } corners)
        {
            parts.Add($"Corners: {corners}.");
        }

        AppendSection(parts, "Sector notes", guide.SectorNotes);
        AppendSection(parts, "Major braking zones", guide.MajorBrakingZones);
        AppendSection(parts, "Traction zones", guide.TractionZones);
        AppendSection(parts, "High-speed sections", guide.HighSpeedSections);
        AppendSection(parts, "Overtaking zones", guide.OvertakingZones);
        AppendSection(parts, "Setup priorities", guide.SetupPriorities);
        AppendSection(parts, "Tyre stress notes", guide.TyreStressNotes);
        AppendSection(parts, "Tyre warmup notes", guide.TyreWarmupNotes);
        AppendSection(parts, "Tyre degradation notes", guide.TyreDegradationNotes);
        AppendSection(parts, "Fuel characteristics", guide.FuelCharacteristics);
        AppendSection(parts, "Fuel/strategy notes", guide.FuelStrategyNotes);
        AppendCornerSection(parts, guide.Corners);

        if (!string.IsNullOrWhiteSpace(focus))
        {
            parts.Add($"Focus: {focus}.");
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static string BuildAiSummary(TrackGuide guide) =>
        ToCoachSummary(guide);

    public static string BuildWatchSummary(TrackGuide guide) =>
        ToCoachSummary(
            guide,
            string.Join("; ", guide.MajorBrakingZones
                .Concat(guide.TyreStressNotes)
                .Concat(guide.SectorNotes)
                .Take(4)));

    public static string BuildPushSummary(TrackGuide guide) =>
        ToCoachSummary(
            guide,
            string.Join("; ", guide.HighSpeedSections
                .Concat(guide.OvertakingZones)
                .Concat(guide.TractionZones)
                .Take(4)));

    public static string BuildDrivingSummary(TrackGuide guide) =>
        ToCoachSummary(guide, string.Join("; ", guide.SectorNotes.Take(4)));

    public static string BuildSetupSummary(TrackGuide guide) =>
        ToCoachSummary(
            guide,
            string.Join("; ", guide.SetupPriorities
                .Concat(guide.TyreStressNotes)
                .Concat(guide.FuelStrategyNotes)
                .Take(4)));

    public static string BuildKeyCornersSummary(TrackGuide guide)
    {
        if (guide.Corners.Count == 0)
        {
            return "No corner map is stored in the cached track guide.";
        }

        var formatted = guide.Corners
            .Select(FormatCorner)
            .ToArray();
        return $"Key corners: {string.Join("; ", formatted)}.";
    }

    public static string BuildTyreFuelAiNotes(TrackGuide guide)
    {
        var parts = new List<string>();
        AppendSection(parts, "Tyre stress", guide.TyreStressNotes);
        AppendSection(parts, "Tyre warmup", guide.TyreWarmupNotes);
        AppendSection(parts, "Tyre degradation", guide.TyreDegradationNotes);
        AppendSection(parts, "Fuel characteristics", guide.FuelCharacteristics);
        AppendSection(parts, "Fuel strategy", guide.FuelStrategyNotes);
        return parts.Count == 0
            ? "No tyre or fuel notes are stored in the cached track guide."
            : string.Join(" ", parts);
    }

    public static string BuildSetupPrioritiesSummary(TrackGuide guide) =>
        guide.SetupPriorities.Count == 0
            ? "No setup priorities are stored in the cached track guide."
            : $"Setup priorities: {string.Join("; ", guide.SetupPriorities)}.";

    public static string FormatCornerList(IReadOnlyList<TrackGuideCorner> corners) =>
        corners.Count == 0
            ? "-"
            : string.Join("; ", corners.Select(FormatCorner));

    private static string FormatCorner(TrackGuideCorner corner)
    {
        var range = corner.LapProgressStart is { } start && corner.LapProgressEnd is { } end
            ? $" ({start:0.00}-{end:0.00})"
            : string.Empty;
        var notes = string.IsNullOrWhiteSpace(corner.Notes) ? string.Empty : $": {corner.Notes}";
        return $"{corner.Name}{range}{notes}";
    }

    private static void AppendSection(List<string> parts, string label, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        parts.Add($"{label}: {string.Join("; ", items)}.");
    }

    private static void AppendCornerSection(List<string> parts, IReadOnlyList<TrackGuideCorner> corners)
    {
        if (corners.Count == 0)
        {
            return;
        }

        parts.Add($"Corner names: {FormatCornerList(corners)}.");
    }
}
