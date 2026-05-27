using System.Globalization;
using RaceEngineer.Core.Storage;

namespace RaceEngineer.Desktop.Wpf;

public sealed class SessionBrowserItem
{
    public SessionBrowserItem(SessionBrowserRow row)
    {
        SessionId = row.SessionId;
        Date = row.StartedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        Car = row.Car ?? "-";
        Track = row.Track ?? "-";
        Duration = FormatDuration(row.Duration);
        BestLap = FormatDuration(row.BestLap);
        EventCount = row.EventCount.ToString(CultureInfo.InvariantCulture);
    }

    public Guid SessionId { get; }
    public string Date { get; }
    public string Car { get; }
    public string Track { get; }
    public string Duration { get; }
    public string BestLap { get; }
    public string EventCount { get; }
    public string Display => $"{Date} | {Track} | {Car} | Best {BestLap} | Events {EventCount}";

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            return "-";
        }

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.Value.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }
}

