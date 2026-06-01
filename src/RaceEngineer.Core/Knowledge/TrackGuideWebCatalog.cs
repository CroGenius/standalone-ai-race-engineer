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

        return TrackGuideCatalogIdentity.Identify(trackName) switch
        {
            TrackGuideCatalogTrack.Monza => Assign(BuildMonza(), out guide),
            TrackGuideCatalogTrack.Spa => Assign(BuildSpa(), out guide),
            TrackGuideCatalogTrack.RedBullRing => Assign(BuildRedBullRing(), out guide),
            _ => false
        };
    }

    private static bool Assign(TrackGuide source, out TrackGuide guide)
    {
        guide = source;
        return true;
    }

    private static TrackGuide BuildMonza()
    {
        var fetchedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        return new TrackGuide(
            TrackGuide.IdForTrack("Monza"),
            "Monza",
            ["Autodromo Nazionale Monza", "Monza GP", "Monza-GP"],
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

    private static TrackGuide BuildRedBullRing()
    {
        var fetchedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        return new TrackGuide(
            TrackGuide.IdForTrack("Red Bull Ring"),
            "Red Bull Ring",
            [
                "RedBullRing",
                "Red Bull Ring",
                "Red-Bull-Ring",
                "Spielberg",
                "RBR",
                "rb_ring",
                "Red Bull Ring GP"
            ],
            "Austria",
            "4.318 km",
            10,
            [
                "Sector 1 is heavy braking into Turn 1 and momentum through Remus.",
                "Sector 2 mixes mid-speed corners with traction-sensitive exits.",
                "Sector 3 rewards commitment through the final two corners onto the straight."
            ],
            [
                "Turn 1 / Niki Lauda Kurve",
                "Turn 3 / Remus",
                "Turn 4 / Schlossgold",
                "Turn 6 / Würth"
            ],
            [
                "Remus exit",
                "Rindt exit",
                "Final corner exit"
            ],
            [
                "Start/finish straight",
                "Approach to Turn 1",
                "Run to Turn 3"
            ],
            [
                "Into Turn 1 after the main straight",
                "Into Turn 3 after a draft"
            ],
            [
                "Stable front under Turn 1 braking.",
                "Rear support for traction at Remus and Schlossgold.",
                "Ride height and kerb compliance for final sector."
            ],
            [
                "Turn 1 loads the front-left repeatedly.",
                "Rear tyres pay for aggressive Remus exits."
            ],
            [
                "Front grip builds quickly on short straights.",
                "Rears need a clean lap before pushing final sector."
            ],
            [
                "Rear overheating shows up under repeated Remus exits.",
                "Front-left can fade if Turn 1 is over-driven early in a stint."
            ],
            [
                "Short lap with repeated hard braking keeps consumption moderate.",
                "Drafting on the main straight can reduce fuel use."
            ],
            [
                "One-stop is common; protect tyres through mid-sector to keep pace late."
            ],
            [
                new("Niki Lauda Kurve", 0.05, 0.11, "Turn 1 heavy braking from top speed."),
                new("Turn 2", 0.11, 0.17, "Short link between Turn 1 and Remus."),
                new("Remus", 0.18, 0.26, "Turn 3 downhill left; traction on exit matters."),
                new("Schlossgold", 0.28, 0.36, "Turn 4 right-hander; rhythm and rear stability."),
                new("Rauch", 0.38, 0.46, "Turn 5 left; prepare for Würth."),
                new("Würth", 0.46, 0.54, "Turn 6 right; traction sets up mid-sector."),
                new("Rindt", 0.58, 0.66, "Turn 7 left-hand corner."),
                new("Red Bull Mobile", 0.66, 0.74, "Turn 8 right; commit to final sector."),
                new("Turn 9", 0.80, 0.88, "Penultimate corner before the straight."),
                new("Final Corner", 0.88, 0.96, "Turn 10 onto the main straight.")
            ],
            [
                new("Built-in track guide catalog", "catalog://track/red-bull-ring"),
                new("Community reference", "https://en.wikipedia.org/wiki/red_bull_ring")
            ],
            fetchedAt,
            null,
            "web-catalog");
    }
}
