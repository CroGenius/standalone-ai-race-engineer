using System.Text.Json;

namespace RaceEngineer.Core;

public sealed record AppSettings(
    string UdpBindIp,
    int UdpPort,
    string DatabasePath,
    bool VoiceEnabledDefault,
    string CaptureFolder,
    string ReplayFolder,
    string PushToTalkHotkey = "F6",
    bool VoiceInputConfirmationsEnabled = true,
    int VoiceInputCooldownSeconds = 3,
    string SpeechRecognitionCulture = "",
    string SpeechRecognitionProvider = "auto",
    string WhisperModelPath = "")
{
    public static AppSettings Default => new(
        "127.0.0.1",
        20999,
        @"%LOCALAPPDATA%\RaceEngineer\race_engineer.sqlite3",
        false,
        @"%LOCALAPPDATA%\RaceEngineer\Debug",
        @"%LOCALAPPDATA%\RaceEngineer\Replays",
        "F6",
        true,
        3,
        "",
        "auto",
        "");

    public static AppSettingsLoadResult Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettingsLoadResult(NormalizeVoiceInput(Default.ResolvePaths()), [$"Settings file not found. Using defaults: {path}"]);
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (settings is null)
            {
                return new AppSettingsLoadResult(Default.ResolvePaths(), ["Settings file was empty or invalid. Using defaults."]);
            }

            var warnings = Validate(settings);
            var resolved = (warnings.Count == 0 ? settings : Default).ResolvePaths();
            return new AppSettingsLoadResult(NormalizeVoiceInput(resolved), warnings);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettingsLoadResult(NormalizeVoiceInput(Default.ResolvePaths()), [$"Settings file could not be loaded. Using defaults. {exception.Message}"]);
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

        if (settings.VoiceInputCooldownSeconds is < 0 or > 120)
        {
            warnings.Add("Voice input cooldown is outside the valid range.");
        }

        if (string.IsNullOrWhiteSpace(settings.PushToTalkHotkey))
        {
            warnings.Add("Push-to-talk hotkey is missing.");
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

    private static AppSettings NormalizeVoiceInput(AppSettings settings)
    {
        return settings with
        {
            PushToTalkHotkey = string.IsNullOrWhiteSpace(settings.PushToTalkHotkey) ? Default.PushToTalkHotkey : settings.PushToTalkHotkey.Trim(),
            VoiceInputCooldownSeconds = settings.VoiceInputCooldownSeconds is < 0 or > 120
                ? Default.VoiceInputCooldownSeconds
                : settings.VoiceInputCooldownSeconds,
            SpeechRecognitionCulture = string.IsNullOrWhiteSpace(settings.SpeechRecognitionCulture)
                ? Default.SpeechRecognitionCulture
                : settings.SpeechRecognitionCulture.Trim(),
            SpeechRecognitionProvider = NormalizeSpeechRecognitionProvider(settings.SpeechRecognitionProvider),
            WhisperModelPath = string.IsNullOrWhiteSpace(settings.WhisperModelPath)
                ? Default.WhisperModelPath
                : settings.WhisperModelPath.Trim()
        };
    }

    private static string NormalizeSpeechRecognitionProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return Default.SpeechRecognitionProvider;
        }

        var normalized = configuredProvider.Trim();
        if (normalized.Equals("auto", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("windows", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("whisper", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.ToLowerInvariant();
        }

        return Default.SpeechRecognitionProvider;
    }
}

public sealed record AppSettingsLoadResult(AppSettings Settings, IReadOnlyList<string> Warnings);

