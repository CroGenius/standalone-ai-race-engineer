namespace RaceEngineer.Core.Voice;

public static class WhisperModelLocator
{
    public const string DefaultModelFileName = "ggml-base.bin";

    public static string ResolveModelPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "RaceEngineer", "Models", DefaultModelFileName);
    }

    public static bool IsModelAvailable(string? configuredPath)
    {
        var path = ResolveModelPath(configuredPath);
        return File.Exists(path);
    }
}
