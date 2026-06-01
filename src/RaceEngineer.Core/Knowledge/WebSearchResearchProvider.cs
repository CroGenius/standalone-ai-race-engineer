namespace RaceEngineer.Core.Knowledge;

public sealed class WebSearchResearchProvider : IWebResearchProvider
{
    public string Name => "web-search-placeholder";

    public bool CanFetchFromRemote => true;

    public Task<WebResearchFetchResult> FetchTrackResearchAsync(string trackName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return Task.FromResult(new WebResearchFetchResult([], false, "Track name is unavailable."));
        }

        var canonicalTrack = TrackGuideCatalogIdentity.CanonicalName(trackName);
        if (string.IsNullOrWhiteSpace(canonicalTrack))
        {
            return Task.FromResult(new WebResearchFetchResult(
                [],
                false,
                $"No placeholder web research is available yet for {trackName.Trim()}."));
        }

        var now = DateTimeOffset.UtcNow;
        var items = new List<ResearchKnowledgeItem>();
        if (TrackGuideWebCatalog.TryGetGuide(canonicalTrack, out var guide))
        {
            items.Add(BuildTrackGuideItem(canonicalTrack, guide, now));
            items.Add(BuildBrakeDemandItem(canonicalTrack, guide, now));
            items.Add(BuildOvertakingZonesItem(canonicalTrack, guide, now));
            items.Add(BuildSetupPrioritiesItem(canonicalTrack, guide, now));
            items.Add(BuildTyreManagementItem(canonicalTrack, guide, now));
            items.Add(BuildFuelStrategyItem(canonicalTrack, guide, now));
        }

        return Task.FromResult(items.Count == 0
            ? new WebResearchFetchResult([], false, $"No placeholder web research is available yet for {canonicalTrack}.")
            : new WebResearchFetchResult(items, true, $"Cached placeholder web research saved for {canonicalTrack}."));
    }

    public Task<WebResearchFetchResult> FetchTrackCarStrategyAsync(
        string trackName,
        string? carName,
        string? carClass,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return Task.FromResult(new WebResearchFetchResult([], false, "Track name is unavailable."));
        }

        var canonicalTrack = TrackGuideCatalogIdentity.CanonicalName(trackName);
        var normalizedClass = TrackCarKnowledgeCatalog.NormalizeCarClass(carClass ?? carName);
        if (string.IsNullOrWhiteSpace(canonicalTrack))
        {
            return Task.FromResult(new WebResearchFetchResult(
                [],
                false,
                $"No placeholder track-car research is available yet for {trackName.Trim()}."));
        }

        if (!TrackCarKnowledgeCatalog.TryResolve(canonicalTrack, normalizedClass ?? carClass ?? carName, out var knowledge))
        {
            return Task.FromResult(new WebResearchFetchResult(
                [],
                false,
                $"No placeholder track-car research is available for {canonicalTrack} and {normalizedClass ?? carName ?? "unknown class"}."));
        }

        var now = DateTimeOffset.UtcNow;
        var items = new List<ResearchKnowledgeItem>
        {
            BuildTrackCarStrategyItem(canonicalTrack, carName, normalizedClass, knowledge, now),
            BuildCarClassBehaviorItem(canonicalTrack, carName, normalizedClass, knowledge, now),
            BuildSetupPrioritiesItem(canonicalTrack, knowledge, normalizedClass, now),
            BuildTyreManagementItem(canonicalTrack, knowledge, normalizedClass, now),
            BuildBrakeDemandItem(canonicalTrack, knowledge, normalizedClass, now),
            BuildOvertakingZonesItem(canonicalTrack, knowledge, normalizedClass, now),
            BuildFuelStrategyItem(canonicalTrack, knowledge, normalizedClass, now)
        };

        return Task.FromResult(new WebResearchFetchResult(
            items,
            true,
            $"Cached placeholder track-car research saved for {canonicalTrack} {normalizedClass ?? "baseline"}."));
    }

    private static ResearchKnowledgeItem BuildTrackGuideItem(string track, TrackGuide guide, DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.TrackGuide,
            track,
            null,
            null,
            $"Cached track research for {track}: focus on {ShortList(guide.MajorBrakingZones)} braking and {ShortList(guide.TractionZones)} traction.",
            BuildFacts(
                guide.SectorNotes,
                guide.MajorBrakingZones,
                guide.TractionZones,
                guide.HighSpeedSections),
            [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://track-guide")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildBrakeDemandItem(string track, TrackGuide guide, DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.BrakeDemand,
            track,
            null,
            null,
            $"Cached research: {track} rewards stable braking into {ShortList(guide.MajorBrakingZones)}.",
            guide.MajorBrakingZones.Take(4).ToArray(),
            [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://brake-demand")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildOvertakingZonesItem(string track, TrackGuide guide, DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.OvertakingZones,
            track,
            null,
            null,
            $"Cached research: key overtaking zones at {track} include {ShortList(guide.OvertakingZones)}.",
            guide.OvertakingZones.Take(4).ToArray(),
            [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://overtaking")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildSetupPrioritiesItem(string track, TrackGuide guide, DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.SetupPriorities,
            track,
            null,
            null,
            $"Cached research setup focus for {track}: {ShortList(guide.SetupPriorities)}.",
            guide.SetupPriorities.Take(4).ToArray(),
            [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://setup")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildTyreManagementItem(string track, TrackGuide guide, DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.TyreManagement,
            track,
            null,
            null,
            $"Cached research tyre notes for {track}: {ShortList(guide.TyreStressNotes)}.",
            BuildFacts(guide.TyreWarmupNotes, guide.TyreStressNotes, guide.TyreDegradationNotes),
            [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://tyres")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildFuelStrategyItem(string track, TrackGuide guide, DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.FuelStrategy,
            track,
            null,
            null,
            $"Cached research fuel strategy for {track}: {ShortList(guide.FuelStrategyNotes)}.",
            BuildFacts(guide.FuelCharacteristics, guide.FuelStrategyNotes),
            [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://fuel")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildTrackCarStrategyItem(
        string track,
        string? car,
        string? carClass,
        TrackCarKnowledge knowledge,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.TrackCarStrategy,
            track,
            car,
            carClass,
            $"Cached track-car strategy for {track} {carClass ?? "cars"}: {ShortList(knowledge.PitStrategyNotes)}.",
            knowledge.PitStrategyNotes.Take(4).ToArray(),
            [new ResearchSourceLabel("built-in catalog", "catalog://track-car-strategy")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildCarClassBehaviorItem(
        string track,
        string? car,
        string? carClass,
        TrackCarKnowledge knowledge,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.CarClassBehavior,
            track,
            car,
            carClass,
            $"Cached research: {carClass ?? "This class"} at {track} — fuel {ShortSentence(knowledge.FuelUsageExpectation)}; tyres {ShortSentence(knowledge.TyreWearExpectation)}.",
            BuildFactStrings(
                knowledge.FuelUsageExpectation,
                knowledge.TyreWearExpectation,
                knowledge.TyreWarmupExpectation,
                knowledge.TopSpeedSensitivity),
            [new ResearchSourceLabel("built-in catalog", "catalog://car-class-behavior")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildSetupPrioritiesItem(
        string track,
        TrackCarKnowledge knowledge,
        string? carClass,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.SetupPriorities,
            track,
            null,
            carClass,
            $"Cached research setup priorities for {track} {carClass ?? "cars"}: {ShortList(knowledge.SetupPriorities)}.",
            knowledge.SetupPriorities.Take(4).ToArray(),
            [new ResearchSourceLabel("built-in catalog", "catalog://setup")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildTyreManagementItem(
        string track,
        TrackCarKnowledge knowledge,
        string? carClass,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.TyreManagement,
            track,
            null,
            carClass,
            $"Cached research tyre management for {track} {carClass ?? "cars"}: wear {ShortSentence(knowledge.TyreWearExpectation)}; warmup {ShortSentence(knowledge.TyreWarmupExpectation)}.",
            BuildFactStrings(knowledge.TyreWearExpectation, knowledge.TyreWarmupExpectation),
            [new ResearchSourceLabel("built-in catalog", "catalog://tyres")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildBrakeDemandItem(
        string track,
        TrackCarKnowledge knowledge,
        string? carClass,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.BrakeDemand,
            track,
            null,
            carClass,
            $"Cached research: {track} is typically {ExtractDemand(knowledge.BrakeDemand)} brake demand for {carClass ?? "this class"}.",
            [knowledge.BrakeDemand],
            [new ResearchSourceLabel("built-in catalog", "catalog://brake-demand")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildOvertakingZonesItem(
        string track,
        TrackCarKnowledge knowledge,
        string? carClass,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.OvertakingZones,
            track,
            null,
            carClass,
            $"Cached research overtaking at {track}: difficulty {ShortSentence(knowledge.OvertakingDifficulty)}; zones {ShortList(knowledge.OvertakingZones)}.",
            knowledge.OvertakingZones.Take(4).ToArray(),
            [new ResearchSourceLabel("built-in catalog", "catalog://overtaking")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static ResearchKnowledgeItem BuildFuelStrategyItem(
        string track,
        TrackCarKnowledge knowledge,
        string? carClass,
        DateTimeOffset fetchedAt) =>
        new(
            Guid.NewGuid(),
            ResearchTopics.FuelStrategy,
            track,
            null,
            carClass,
            $"Cached research fuel strategy for {track} {carClass ?? "cars"}: {ShortSentence(knowledge.FuelUsageExpectation)}.",
            BuildFactStrings(knowledge.FuelUsageExpectation).Concat(knowledge.PitStrategyNotes.Take(4)).ToArray(),
            [new ResearchSourceLabel("built-in catalog", "catalog://fuel")],
            fetchedAt,
            "Medium",
            NameStatic);

    private static IReadOnlyList<string> BuildFacts(params IEnumerable<string>[] groups) =>
        groups
            .SelectMany(group => group)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(6)
            .ToArray();

    private static IReadOnlyList<string> BuildFactStrings(params string?[] values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Take(6)
            .ToArray();

    private static string ShortList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "no notes yet" : string.Join("; ", values.Take(3));

    private static string ShortSentence(string value) =>
        value.Length <= 120 ? value : value[..117] + "...";

    private static string ExtractDemand(string demand)
    {
        if (demand.StartsWith("Very high", StringComparison.OrdinalIgnoreCase))
        {
            return "very high";
        }

        if (demand.StartsWith("Medium-high", StringComparison.OrdinalIgnoreCase))
        {
            return "medium-high";
        }

        if (demand.StartsWith("Medium", StringComparison.OrdinalIgnoreCase))
        {
            return "medium";
        }

        if (demand.StartsWith("High", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        return "variable";
    }

    private const string NameStatic = "web-search-placeholder";
}
