namespace RaceEngineer.Core.Knowledge;

public static class ResearchTopics
{
    public const string TrackGuide = "track_guide";
    public const string TrackCarStrategy = "track_car_strategy";
    public const string CarClassBehavior = "car_class_behavior";
    public const string SetupPriorities = "setup_priorities";
    public const string TyreManagement = "tyre_management";
    public const string BrakeDemand = "brake_demand";
    public const string OvertakingZones = "overtaking_zones";
    public const string FuelStrategy = "fuel_strategy";

    public static readonly IReadOnlyList<string> TrackTopics =
    [
        TrackGuide,
        SetupPriorities,
        TyreManagement,
        BrakeDemand,
        OvertakingZones,
        FuelStrategy
    ];

    public static readonly IReadOnlyList<string> TrackCarTopics =
    [
        TrackCarStrategy,
        CarClassBehavior,
        SetupPriorities,
        TyreManagement,
        BrakeDemand,
        OvertakingZones,
        FuelStrategy
    ];

    public static bool IsTrackTopic(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && TrackTopics.Contains(topic, StringComparer.OrdinalIgnoreCase);

    public static bool MatchesQuestion(string? topic, string question)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var text = question.ToLowerInvariant();
        return topic switch
        {
            TrackGuide => text.Contains("track guide", StringComparison.Ordinal)
                || text.Contains("track advice", StringComparison.Ordinal)
                || text.Contains("about this track", StringComparison.Ordinal)
                || text.Contains("key corner", StringComparison.Ordinal),
            SetupPriorities => text.Contains("setup", StringComparison.Ordinal),
            TyreManagement => text.Contains("tyre", StringComparison.Ordinal) || text.Contains("tire", StringComparison.Ordinal),
            BrakeDemand => text.Contains("brake", StringComparison.Ordinal),
            OvertakingZones => text.Contains("overtake", StringComparison.Ordinal) || text.Contains("pass", StringComparison.Ordinal),
            FuelStrategy => text.Contains("fuel", StringComparison.Ordinal) || text.Contains("strategy", StringComparison.Ordinal),
            TrackCarStrategy => text.Contains("strategy", StringComparison.Ordinal) || text.Contains("plan", StringComparison.Ordinal),
            CarClassBehavior => text.Contains("class", StringComparison.Ordinal) || text.Contains("car behavior", StringComparison.Ordinal),
            _ => false
        };
    }
}

public sealed record ResearchSourceLabel(string Label, string? Url = null);

public sealed record ResearchKnowledgeItem(
    Guid Id,
    string Topic,
    string? Track,
    string? Car,
    string? CarClass,
    string Summary,
    IReadOnlyList<string> KeyFacts,
    IReadOnlyList<ResearchSourceLabel> Sources,
    DateTimeOffset FetchedAt,
    string ConfidenceLabel,
    string ProviderName)
{
    public int SourceCount => Sources.Count;

    public DateTimeOffset LastUpdatedAt => FetchedAt;
}

public sealed record WebResearchLookupResult(
    IReadOnlyList<ResearchKnowledgeItem> Items,
    bool FromCache,
    string? Message);

public sealed record WebResearchFetchResult(
    IReadOnlyList<ResearchKnowledgeItem> Items,
    bool RefreshedFromRemote,
    string Message,
    string ProviderName = "unknown",
    string CacheStatus = "unknown",
    string Status = "unknown",
    string? ErrorMessage = null)
{
    public int ItemsReturned => Items.Count;

    public static WebResearchFetchResult Disabled(string reason) =>
        new([], false, reason, DisabledWebResearchProvider.Instance.Name, "miss", "failed", reason);

    public static WebResearchFetchResult Failed(
        string reason,
        string providerName,
        string cacheStatus = "miss") =>
        new([], false, reason, providerName, cacheStatus, "failed", reason);

    public static WebResearchFetchResult Succeeded(
        IReadOnlyList<ResearchKnowledgeItem> items,
        string message,
        string providerName,
        string cacheStatus,
        bool refreshedFromRemote = true) =>
        new(items, refreshedFromRemote, message, providerName, cacheStatus, "success", null);
}

public sealed record WebResearchBundle(
    string? Track,
    string? Car,
    string? CarClass,
    IReadOnlyList<ResearchKnowledgeItem> Items)
{
    public bool HasResearch => Items.Count > 0;

    public DateTimeOffset? LastFetchedAt =>
        Items.Count == 0 ? null : Items.Max(item => item.FetchedAt);

    public int SourceCount => Items.Sum(item => item.SourceCount);

    public static WebResearchBundle Empty(string? track = null, string? car = null, string? carClass = null) =>
        new(track, car, carClass, []);
}
