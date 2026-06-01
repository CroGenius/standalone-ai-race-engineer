namespace RaceEngineer.Core.Knowledge;

public enum TrackGuideCatalogTrack
{
    None,
    Monza,
    Spa,
    RedBullRing,
    NurburgringGp
}

public static class TrackGuideCatalogIdentity
{
    public static TrackGuideCatalogTrack Identify(string? trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return TrackGuideCatalogTrack.None;
        }

        var key = TrackGuide.NormalizeTrackKey(trackName);
        if (MatchesRedBullRing(key))
        {
            return TrackGuideCatalogTrack.RedBullRing;
        }

        if (MatchesNurburgringGp(key))
        {
            return TrackGuideCatalogTrack.NurburgringGp;
        }

        if (MatchesMonza(key))
        {
            return TrackGuideCatalogTrack.Monza;
        }

        if (MatchesSpa(key))
        {
            return TrackGuideCatalogTrack.Spa;
        }

        return TrackGuideCatalogTrack.None;
    }

    public static string? CanonicalName(TrackGuideCatalogTrack track) =>
        track switch
        {
            TrackGuideCatalogTrack.Monza => "Monza",
            TrackGuideCatalogTrack.Spa => "Spa",
            TrackGuideCatalogTrack.RedBullRing => "Red Bull Ring",
            TrackGuideCatalogTrack.NurburgringGp => "Nürburgring GP",
            _ => null
        };

    public static string? CanonicalName(string? trackName)
    {
        var track = Identify(trackName);
        return track == TrackGuideCatalogTrack.None ? NullIfWhitespace(trackName) : CanonicalName(track);
    }

    public static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftId = Identify(left);
        var rightId = Identify(right);
        if (leftId != TrackGuideCatalogTrack.None && rightId != TrackGuideCatalogTrack.None)
        {
            return leftId == rightId;
        }

        var leftKey = TrackGuide.NormalizeTrackKey(left);
        var rightKey = TrackGuide.NormalizeTrackKey(right);
        return leftKey == rightKey
            || leftKey.Contains(rightKey, StringComparison.Ordinal)
            || rightKey.Contains(leftKey, StringComparison.Ordinal);
    }

    private static bool MatchesRedBullRing(string key) =>
        key.Contains("redbullring", StringComparison.Ordinal)
        || key.Contains("red bull ring", StringComparison.Ordinal)
        || key.Contains("red-bull-ring", StringComparison.Ordinal)
        || key.Contains("redbull_ring", StringComparison.Ordinal)
        || key.Contains("rb_ring", StringComparison.Ordinal)
        || key.Contains("spielberg", StringComparison.Ordinal)
        || key == "rbr"
        || key.EndsWith("_rbr", StringComparison.Ordinal)
        || key.EndsWith("-rbr", StringComparison.Ordinal)
        || key.Contains("redbullring_gp", StringComparison.Ordinal)
        || key.Contains("red_bull_ring", StringComparison.Ordinal);

    private static bool MatchesNurburgringGp(string key) =>
        key.Contains("nurburgring gp", StringComparison.Ordinal)
        || key.Contains("nürburgring gp", StringComparison.Ordinal)
        || key.Contains("nuerburgring gp", StringComparison.Ordinal)
        || key.Contains("nurburgring-gp", StringComparison.Ordinal)
        || key.Contains("gp_strecke", StringComparison.Ordinal)
        || key.Contains("nurburgring grand prix", StringComparison.Ordinal)
        || (key.Contains("nurburgring", StringComparison.Ordinal) && !key.Contains("nordschleife", StringComparison.Ordinal))
        || (key.Contains("nürburgring", StringComparison.Ordinal) && !key.Contains("nordschleife", StringComparison.Ordinal));

    private static bool MatchesMonza(string key) =>
        key.Contains("monza", StringComparison.Ordinal)
        || key.Contains("autodromo nazionale", StringComparison.Ordinal);

    private static bool MatchesSpa(string key) =>
        key.Contains("spa", StringComparison.Ordinal)
        || key.Contains("francorchamps", StringComparison.Ordinal);

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
