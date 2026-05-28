namespace RaceEngineer.Core.Voice;

public sealed class VoiceInteractionGate
{
    public static TimeSpan DefaultQuietWindow { get; } = TimeSpan.FromSeconds(10);

    private DateTimeOffset quietUntil = DateTimeOffset.MinValue;
    private string lastDiagnostic = "Voice interaction ready.";

    public string LastDiagnostic => lastDiagnostic;

    public void BeginUserQuestionQuietWindow(DateTimeOffset now, TimeSpan? duration = null)
    {
        quietUntil = now + (duration ?? DefaultQuietWindow);
        lastDiagnostic = $"User question quiet window active until {quietUntil:HH:mm:ss}.";
    }

    public bool IsAutomaticCalloutAllowed(DateTimeOffset now)
    {
        return now >= quietUntil;
    }

    public string AutomaticSuppressionReason(DateTimeOffset now)
    {
        if (IsAutomaticCalloutAllowed(now))
        {
            return "";
        }

        var remaining = quietUntil - now;
        return $"post-question quiet window ({remaining.TotalSeconds:0}s remaining)";
    }

    public void LogDirectAnswerSpoken(string spokenText)
    {
        lastDiagnostic = $"Direct voice answer spoken: {spokenText}";
    }

    public void LogDirectAnswerNotSpoken(string reason)
    {
        lastDiagnostic = $"Direct voice answer not spoken: {reason}";
    }

    public void LogAutomaticCalloutSuppressed(string reason)
    {
        lastDiagnostic = $"Automatic callout suppressed: {reason}";
    }

    public void LogAutomaticCalloutSpoken(string callout)
    {
        lastDiagnostic = $"Automatic callout spoken: {callout}";
    }
}
