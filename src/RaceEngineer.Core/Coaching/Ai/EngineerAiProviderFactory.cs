using RaceEngineer.Core;

namespace RaceEngineer.Core.Coaching.Ai;

public static class EngineerAiProviderFactory
{
    public static IEngineerAiProvider Create(AppSettings settings)
    {
        if (!settings.AiEngineerEnabled)
        {
            return DisabledEngineerAiProvider.Instance;
        }

        return NormalizeProvider(settings.AiProvider) switch
        {
            "mock" => new MockEngineerAiProvider(),
            "openai" => new OpenAiCompatibleEngineerAiProvider(
                settings.AiEndpoint,
                settings.AiModel,
                settings.AiTimeoutSeconds),
            _ => DisabledEngineerAiProvider.Instance
        };
    }

    private static string NormalizeProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return "disabled";
        }

        var normalized = configuredProvider.Trim().ToLowerInvariant();
        return normalized is "mock" or "openai" or "disabled"
            ? normalized
            : "disabled";
    }
}
