using System.Text;
using System.Text.Json;
using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.Knowledge;

public static class ResearchKnowledgeMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public const string CategoryPrefix = "web_research";

    public static KnowledgeSource ToKnowledgeSource(ResearchKnowledgeItem item) =>
        new(
            item.Id,
            KnowledgeSourceTypes.Web,
            BuildTitle(item),
            item.Sources.FirstOrDefault()?.Url,
            item.FetchedAt,
            Serialize(item),
            $"Cached research ({item.ProviderName}); confidence {item.ConfidenceLabel}.",
            item.Car,
            item.Track,
            null,
            BuildCategory(item.Topic));

    public static bool TryParse(KnowledgeSource source, out ResearchKnowledgeItem item)
    {
        item = null!;
        if (!IsResearchSource(source))
        {
            return false;
        }

        try
        {
            item = Deserialize(source.Content);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsResearchSource(KnowledgeSource source) =>
        string.Equals(source.SourceType, KnowledgeSourceTypes.Web, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(source.Category)
        && source.Category.StartsWith(CategoryPrefix, StringComparison.OrdinalIgnoreCase);

    public static string BuildCategory(string topic) => $"{CategoryPrefix}:{topic}";

    public static string? ExtractTopic(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        var prefix = CategoryPrefix + ":";
        return category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? category[prefix.Length..]
            : null;
    }

    private static string BuildTitle(ResearchKnowledgeItem item)
    {
        var builder = new StringBuilder("Research: ");
        builder.Append(item.Topic.Replace('_', ' '));
        if (!string.IsNullOrWhiteSpace(item.Track))
        {
            builder.Append(" — ").Append(item.Track);
        }

        if (!string.IsNullOrWhiteSpace(item.CarClass))
        {
            builder.Append(" / ").Append(item.CarClass);
        }

        return builder.ToString();
    }

    private static string Serialize(ResearchKnowledgeItem item) =>
        JsonSerializer.Serialize(item, JsonOptions);

    private static ResearchKnowledgeItem Deserialize(string json) =>
        JsonSerializer.Deserialize<ResearchKnowledgeItem>(json, JsonOptions)
        ?? throw new JsonException("Research knowledge payload was empty.");
}
