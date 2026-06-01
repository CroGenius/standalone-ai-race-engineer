namespace RaceEngineer.Core.Knowledge;

public static class TrackGuideWebCatalog
{
    public static bool TryGetGuide(string trackName, out TrackGuide guide)
    {
        guide = null!;
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return false;
        }

        var key = TrackGuide.NormalizeTrackKey(trackName);
        if (key.Contains("monza", StringComparison.Ordinal))
        {
            guide = BuildMonza();
            return true;
        }

        if (key.Contains("spa", StringComparison.Ordinal))
        {
            guide = BuildSpa();
            return true;
        }

        return false;
    }

    private static TrackGuide BuildMonza()
    {
        var fetchedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        return new TrackGuide(
            TrackGuide.IdForTrack("Monza"),
            "Monza",
            ["Autodromo Nazionale Monza", "Monza GP"],
            "Italy",
            "5.793 km",
            11,
            [
                "Sector 1 rewards strong braking stability into Turn 1 and clean exits through Lesmo.",
                "Sector 2 is about momentum through Ascari and preparing for the long straight.",
                "Sector 3 is a low-grip stop-start section; traction and tyre temperature matter."
            ],
            [
                "Turn 1 after the start/finish straight",
                "Turn 4 / Lesmo 1",
                "Turn 8 / Ascari chicane",
                "Turn 11 / Parabolica entry"
            ],
            [
                "Lesmo 2 exit",
                "Ascari exit",
                "Parabolica exit onto the main straight"
            ],
            [
                "Main straight",
                "Approach to Ascari",
                "Curva Grande"
            ],
            [
                "Main straight with slipstream",
                "Into Turn 1 after a draft"
            ],
            [
                "Prioritize stable braking and front support for Turn 1 and Ascari.",
                "Manage rear tyre life for repeated Parabolica exits.",
                "Low downforce can expose kerb instability at Lesmo."
            ],
            [
                "Heavy braking into Turn 1 loads the front-left.",
                "Ascari and Parabolica punish overheated rears on long runs."
            ],
            [
                "Front tyres need temperature before pushing into Lesmo.",
                "Rear grip builds after one clean traction lap."
            ],
            [
                "Rear overheating shows up under repeated Ascari exits.",
                "Front-left can fade if Turn 1 is over-driven early in a stint."
            ],
            [
                "High full-throttle percentage raises consumption.",
                "Drafting reduces load but can tempt extra lap time and burn."
            ],
            [
                "Fuel use is high due to full-throttle time; plan stops around high fuel burn stints.",
                "Slipstream battles can push you over fuel target if you stay in dirty air too long."
            ],
            [
                new("Rettifilo", 0.06, 0.14, "Heavy braking from top speed into Turn 1."),
                new("Lesmo 1", 0.20, 0.30, "Trail-brake and protect inside front."),
                new("Lesmo 2", 0.34, 0.44, "Rhythm corner; keep rear settled on exit."),
                new("Ascari", 0.50, 0.62, "Two-part chicane; traction on exit matters."),
                new("Parabolica", 0.85, 0.96, "Commit to exit for straight speed.")
            ],
            [
                new("Built-in track guide catalog", "catalog://track/monza"),
                new("Community reference", "https://en.wikipedia.org/wiki/autodromo_nazionale_monza")
            ],
            fetchedAt,
            null,
            "web-catalog");
    }

    private static TrackGuide BuildSpa()
    {
        var fetchedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        return new TrackGuide(
            TrackGuide.IdForTrack("Spa"),
            "Spa",
            ["Spa-Francorchamps", "Circuit de Spa-Francorchamps"],
            "Belgium",
            "7.004 km",
            19,
            [
                "Sector 1 is about Eau Rouge/Raidillon commitment and Kemmel straight speed.",
                "Sector 2 mixes fast sweepers with blind entries at Pouhon and Stavelot.",
                "Sector 3 rewards traction through low-grip final sector."
            ],
            [
                "La Source hairpin",
                "Les Combes",
                "Bruxelles / Pouhon approach",
                "Chicane entry"
            ],
            [
                "Eau Rouge/Raidillon exit",
                "Les Combes exit",
                "Stavelot exit"
            ],
            [
                "Kemmel straight",
                "Blanchimont",
                "Eau Rouge/Raidillon"
            ],
            [
                "Kemmel straight into Les Combes",
                "Bus stop chicane on the inside line"
            ],
            [
                "High-speed stability for Eau Rouge and Blanchimont.",
                "Brake bias and ABS support for low-grip final sector.",
                "Tyre temperature management on long laps."
            ],
            [
                "Long lap and high-speed load heat all four tyres.",
                "Low ambient temperature can delay front grip in early laps."
            ],
            [
                "Warm fronts before attacking Pouhon.",
                "Rears need clean exits at Les Combes before pushing Blanchimont."
            ],
            [
                "Rear overheating accumulates through Sector 2 if exits are messy.",
                "Front-left can grain in cold conditions under heavy braking."
            ],
            [
                "Fuel consumption rises with flat-out Kemmel runs and traffic battles.",
                "Safety car risk can swing one-stop strategy windows."
            ],
            [
                "Fuel consumption rises with flat-out Kemmel runs and traffic battles.",
                "Safety car risk can swing one-stop strategy windows."
            ],
            [
                new("La Source", 0.03, 0.06, "Tight opening hairpin."),
                new("Eau Rouge", 0.10, 0.16, "Commit only with stable car."),
                new("Les Combes", 0.30, 0.36, "Heavy braking and overtaking point."),
                new("Pouhon", 0.48, 0.54, "Blind entry; smooth minimum speed.")
            ],
            [
                new("Built-in track guide catalog", "catalog://track/spa"),
                new("Community reference", "https://en.wikipedia.org/wiki/circuit_de_spa-francorchamps")
            ],
            fetchedAt,
            null,
            "web-catalog");
    }
}
