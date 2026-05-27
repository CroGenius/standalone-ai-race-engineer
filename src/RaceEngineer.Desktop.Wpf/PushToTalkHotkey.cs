using System.Windows.Input;

namespace RaceEngineer.Desktop.Wpf;

public sealed record PushToTalkHotkey(Key Key, ModifierKeys Modifiers)
{
    public string DisplayName => Modifiers == ModifierKeys.None
        ? Key.ToString()
        : $"{Modifiers}+{Key}";

    public static PushToTalkHotkey Default { get; } = new(Key.F6, ModifierKeys.None);

    public static bool TryParse(string? value, out PushToTalkHotkey hotkey)
    {
        hotkey = Default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var segments = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        var keySegment = segments[^1];
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!Enum.TryParse(segments[index], true, out ModifierKeys parsedModifier))
            {
                return false;
            }

            modifiers |= parsedModifier;
        }

        if (!Enum.TryParse(keySegment, true, out Key parsedKey))
        {
            return false;
        }

        hotkey = new PushToTalkHotkey(parsedKey, modifiers);
        return true;
    }

    public bool Matches(KeyEventArgs args)
    {
        if (args is null)
        {
            return false;
        }

        return args.Key == this.Key && Keyboard.Modifiers == this.Modifiers;
    }
}
