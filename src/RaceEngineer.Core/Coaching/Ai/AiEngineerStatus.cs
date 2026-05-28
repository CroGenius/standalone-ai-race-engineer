using RaceEngineer.Core;

namespace RaceEngineer.Core.Coaching.Ai;

public static class AiEngineerStatusResolver
{
    public static string ResolveLabel(AppSettings settings, EngineerAiDiagnosticTrace? lastDiagnostic = null)
    {
        if (!settings.AiEngineerEnabled)
        {
            return "Disabled";
        }

        var provider = NormalizeProvider(settings.AiProvider);
        if (provider == "mock")
        {
            return ResolveRuntimeLabel(lastDiagnostic, "Mock");
        }

        if (provider == "openai")
        {
            if (!OpenAiCompatibleEngineerAiProvider.HasConfiguredApiKey())
            {
                return "Missing API key";
            }

            return ResolveRuntimeLabel(lastDiagnostic, "OpenAI ready");
        }

        return "Disabled";
    }

    private static string ResolveRuntimeLabel(EngineerAiDiagnosticTrace? lastDiagnostic, string readyLabel)
    {
        if (lastDiagnostic is null)
        {
            return readyLabel;
        }

        if (lastDiagnostic.TimedOut)
        {
            return "Timeout";
        }

        if (lastDiagnostic.UsedFallback)
        {
            return "Fallback";
        }

        return lastDiagnostic.UsedAi ? readyLabel : readyLabel;
    }

    private static string NormalizeProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return "disabled";
        }

        return configuredProvider.Trim().ToLowerInvariant();
    }
}
