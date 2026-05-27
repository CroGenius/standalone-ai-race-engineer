using System.Text.Json;

namespace RaceEngineer.Core;

public sealed record AppSettings(
    string UdpBindIp,
    int UdpPort,
    string DatabasePath,
    bool VoiceEnabledDefault,
    string CaptureFolder,
    string ReplayFolder)
{
    public static AppSettings Default => new(
        "127.0.0.1",
        20999,
        @"%LOCALAPPDATA%\RaceEngineer\race_engineer.sqlite3",
        false,
        @"%LOCALAPPDATA%\RaceEngineer\Debug",
        @"%LOCALAPPDATA%\RaceEngineer\Replays");

    public static AppSettingsLoadResult Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettingsLoadResult(Default.ResolvePaths(), [$"Settings file not found. Using defaults: {path}"]);
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (settings is null)
            {
                return new AppSettingsLoadResult(Default.ResolvePaths(), ["Settings file was empty or invalid. Using defaults."]);
            }

            var warnings = Validate(settings);
            return new AppSettingsLoadResult((warnings.Count == 0 ? settings : Default).ResolvePaths(), warnings);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettingsLoadResult(Default.ResolvePaths(), [$"Settings file could not be loaded. Using defaults. {exception.Message}"]);
        }
    }

    public AppSettings ResolvePaths()
    {
        return this with
        {
            DatabasePath = ExpandPath(DatabasePath),
            CaptureFolder = ExpandPath(CaptureFolder),
            ReplayFolder = ExpandPath(ReplayFolder)
        };
    }

    private static List<string> Validate(AppSettings settings)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.UdpBindIp))
        {
            warnings.Add("UDP bind IP is missing.");
        }

        if (settings.UdpPort is <= 0 or > 65535)
        {
            warnings.Add("UDP port is outside the valid range.");
        }

        if (string.IsNullOrWhiteSpace(settings.DatabasePath))
        {
            warnings.Add("Database path is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.CaptureFolder))
        {
            warnings.Add("Capture folder is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.ReplayFolder))
        {
            warnings.Add("Replay folder is missing.");
        }

        if (warnings.Count > 0)
        {
            warnings.Add("Invalid settings file. Using defaults.");
        }

        return warnings;
    }

    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}

public sealed record AppSettingsLoadResult(AppSettings Settings, IReadOnlyList<string> Warnings);

