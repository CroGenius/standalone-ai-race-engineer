using System.IO;

namespace RaceEngineer.Desktop.Wpf;

internal static class BlockedFileStartupCheck
{
    public static IReadOnlyList<string> FindBlockedFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var blocked = new List<string>();
        foreach (var pattern in new[] { "*.dll", "*.exe" })
        {
            foreach (var path in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            {
                if (HasZoneIdentifier(path))
                {
                    blocked.Add(Path.GetFileName(path));
                }
            }
        }

        blocked.Sort(StringComparer.OrdinalIgnoreCase);
        return blocked;
    }

    public static string BuildWarningMessage(IReadOnlyList<string> blockedFiles)
    {
        if (blockedFiles.Count == 0)
        {
            return "";
        }

        var sample = string.Join(", ", blockedFiles.Take(4));
        if (blockedFiles.Count > 4)
        {
            sample += $", +{blockedFiles.Count - 4} more";
        }

        return
            $"Windows blocked {blockedFiles.Count} file(s) in the app folder ({sample}). " +
            "Voice/microphone features may fail with FileLoadException. " +
            $"Unblock with: Get-ChildItem -Path '{AppContext.BaseDirectory}' -Recurse -File | Unblock-File";
    }

    public static string? TryBuildFileLoadPolicyHint(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not FileLoadException fileLoad)
            {
                continue;
            }

            if (fileLoad.Message.Contains("Application Control policy", StringComparison.OrdinalIgnoreCase) ||
                fileLoad.Message.Contains("operation is not permitted by", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = string.IsNullOrWhiteSpace(fileLoad.FileName)
                    ? "a dependency DLL"
                    : Path.GetFileName(fileLoad.FileName);
                return
                    $"Windows blocked {fileName} (Application Control policy). " +
                    "Unblock the app folder: Get-ChildItem -Path '<app folder>' -Recurse -File | Unblock-File";
            }
        }

        return null;
    }

    private static bool HasZoneIdentifier(string path)
    {
        try
        {
            return File.Exists(path + ":Zone.Identifier");
        }
        catch
        {
            return false;
        }
    }
}
