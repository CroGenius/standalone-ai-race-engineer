namespace RaceEngineer.Core.Knowledge;

public static class TrackCarKnowledgeCatalog
{
    private static readonly IReadOnlyDictionary<TrackGuideCatalogTrack, TrackKnowledge> Tracks =
        new Dictionary<TrackGuideCatalogTrack, TrackKnowledge>
        {
            [TrackGuideCatalogTrack.Monza] = new(
                "Monza",
                "High — long full-throttle sections and heavy braking from top speed.",
                "Medium-high — rear tyres pay on repeated Parabolica exits and Ascari traction.",
                "Front grip builds through Lesmo; rears need one clean traction lap.",
                "High — Turn 1, Lesmo, and Ascari load the front axle repeatedly.",
                "Very high — low drag and slipstream define lap time.",
                "Medium — low downforce helps straight speed but hurts kerb stability.",
                "Moderate — draft on the main straight, costly defence into Turn 1.",
                ["One-stop common if fuel target is met; two-stop if fighting in dirty air.", "Protect tyres before final stint push."],
                ["Stable braking into Turn 1", "Rear tyre life for Parabolica exits", "Low drag without unstable kerb ride"],
                ["Main straight into Turn 1", "Approach to Ascari", "Into Turn 1 after a draft"]),
            [TrackGuideCatalogTrack.Spa] = new(
                "Spa",
                "High — long lap with Kemmel straight and full-throttle time.",
                "High — long corners and variable weather stress all four tyres.",
                "Warm fronts before Pouhon; rears need clean Les Combes exits.",
                "Very high — La Source, Les Combes, and Bus Stop are heavy stops.",
                "Very high — Kemmel and Blanchimont reward low drag.",
                "Medium-high — Eau Rouge/Raidillon need stability more than peak downforce.",
                "Moderate — Kemmel draft helps, Bus Stop is a prime chance.",
                ["Two-stop common in sprint races; monitor weather windows.", "Fuel and tyre life both swing with traffic on Kemmel."],
                ["High-speed stability", "Brake migration on long runs", "Tyre temperature in mixed conditions"],
                ["Kemmel straight into Les Combes", "Bus Stop chicane", "La Source on lap 1"]),
            [TrackGuideCatalogTrack.RedBullRing] = new(
                "Red Bull Ring",
                "Medium — short lap but repeated hard braking from high speed.",
                "Medium — traction zones at Remus and final sector wear rears.",
                "Tyres come in quickly on a short lap; one out lap is usually enough.",
                "Medium-high — Turn 1 and Remus are repeated heavy stops.",
                "High — main straight speed is decisive.",
                "Medium — enough downforce for Turn 1 without sacrificing too much straight speed.",
                "Moderate — Turn 1 and Turn 3 are the main chances.",
                ["One-stop is typical; fuel margin is less critical than tyre life.", "Short lap makes traffic management important."],
                ["Turn 1 brake stability", "Traction at Remus", "Final sector rear support"],
                ["Main straight into Turn 1", "Into Turn 3 after a draft", "Turn 1 on lap 1"]),
            [TrackGuideCatalogTrack.NurburgringGp] = new(
                "Nürburgring GP",
                "Medium — mix of long straight and technical middle sector.",
                "Medium-high — loaded corners and traction zones stress rears.",
                "Fronts warm on the straight; rears need tidy exits at Turn 1 and Mercedes arena.",
                "Medium-high — Turn 1, Turn 6, and final chicane load the front.",
                "High — long back straight rewards low drag.",
                "Medium — balance straight speed with Mercedes and final sector grip.",
                "Moderate — back straight and Turn 1 are main passing zones.",
                ["One-stop common; watch tyre drop-off in long runs.", "Fuel use rises if you fight on the back straight."],
                ["Brake stability into Turn 1", "Rear traction through Mercedes", "Ride height over kerbs in final sector"],
                ["Back straight into Turn 1", "Turn 1 after a draft", "Final chicane on defensive line"])
        };

    private static readonly IReadOnlyDictionary<string, CarClassKnowledge> Classes =
        new Dictionary<string, CarClassKnowledge>(StringComparer.OrdinalIgnoreCase)
        {
            ["GT3"] = new(
                "GT3",
                "Class baseline medium — varies strongly by track length and straights.",
                "Moderate rear degradation on long stints.",
                "Plan one out lap before push; fronts build through lap 2.",
                "High in heavy braking zones for most GT tracks.",
                "Medium — top speed matters on low-drag setups.",
                "Medium — enough downforce for trail braking without excessive drag.",
                "Moderate — draft helps on long straights.",
                ["Typical first stop laps 14-18 depending on track.", "Match fuel target before late push."],
                ["Stable brake bias", "Conservative rear wing if tyre temps spike early"],
                2.4,
                1.0),
            ["GT4"] = new(
                "GT4",
                "Class baseline medium-low — generally lower power and fuel burn than GT3.",
                "Moderate — less aero load can reduce peak tyre stress.",
                "Tyres warm quickly; avoid sliding on out lap.",
                "Medium-high under heavy braking.",
                "Medium-low — less sensitive to top speed than GT3.",
                "Medium — mechanical grip matters more than peak downforce.",
                "Moderate-low — fewer overlap opportunities than GT3.",
                ["Longer stints possible on some tracks.", "Fuel margin usually less tight than GT3."],
                ["Mechanical grip and brake pedal feel", "Conservative camber for long runs"],
                2.0,
                1.0),
            ["Touring"] = new(
                "Touring",
                "Class baseline low-medium — shorter gearing and lower top speed reduce burn.",
                "Medium — front tyres can grain under heavy trail braking.",
                "Warm tyres on out lap; avoid aggressive cold push.",
                "Very high — shorter braking zones with heavy pedal pressure.",
                "Low — top speed rarely decides lap time.",
                "Low-medium — mechanical balance over aero.",
                "High — close racing and frequent side-by-side.",
                ["Pit windows depend on tyre compound and race length.", "Fuel stops less common in sprint formats."],
                ["Front tyre temperature", "Brake cooling and bias"],
                1.8,
                0.75),
            ["Cup"] = new(
                "Cup",
                "Class baseline low — lighter cars with moderate power.",
                "Medium — rear wear follows traction abuse more than aero load.",
                "Quick warmup; peak grip early in stint.",
                "High — repeated threshold braking in pack racing.",
                "Low-medium — draft still matters on faster tracks.",
                "Low — setup focus is mechanical grip.",
                "High — pack racing creates many overlap chances.",
                ["Fuel rarely limits stint length in sprints.", "Tyre life often decides stop timing."],
                ["Brake cooling", "Rear traction on exit"],
                1.6,
                0.75),
            ["Hypercar"] = new(
                "Hypercar",
                "Class baseline medium-high — power and aero load increase consumption on long tracks.",
                "High — aero load and long corners heat all four tyres.",
                "Warm tyres gently; peak grip after one flying lap.",
                "Very high — aero-assisted braking with heavy entry speeds.",
                "High — low drag essential on long straights.",
                "High — platform stability under aero load matters.",
                "Low-moderate — pace differential reduces side-by-side time.",
                ["Two-stop common in endurance formats.", "Fuel and tyre windows are tightly linked."],
                ["Brake migration", "Aero balance for high-speed stability"],
                3.0,
                0.75)
        };

    private static readonly IReadOnlyDictionary<(TrackGuideCatalogTrack Track, string Class), TrackCarKnowledge> Combos =
        new Dictionary<(TrackGuideCatalogTrack, string), TrackCarKnowledge>
        {
            [(TrackGuideCatalogTrack.Monza, "GT3")] = BuildCombo(TrackGuideCatalogTrack.Monza, "GT3", 2.9, "Very high on Monza — full throttle and heavy stops dominate."),
            [(TrackGuideCatalogTrack.Monza, "GT4")] = BuildCombo(TrackGuideCatalogTrack.Monza, "GT4", 2.4),
            [(TrackGuideCatalogTrack.Monza, "Hypercar")] = BuildCombo(TrackGuideCatalogTrack.Monza, "Hypercar", 3.4),
            [(TrackGuideCatalogTrack.Spa, "GT3")] = BuildCombo(TrackGuideCatalogTrack.Spa, "GT3", 3.0),
            [(TrackGuideCatalogTrack.Spa, "GT4")] = BuildCombo(TrackGuideCatalogTrack.Spa, "GT4", 2.5),
            [(TrackGuideCatalogTrack.Spa, "Hypercar")] = BuildCombo(TrackGuideCatalogTrack.Spa, "Hypercar", 3.5),
            [(TrackGuideCatalogTrack.RedBullRing, "GT3")] = BuildCombo(TrackGuideCatalogTrack.RedBullRing, "GT3", 2.3, "Medium on Red Bull Ring — short lap limits total burn."),
            [(TrackGuideCatalogTrack.RedBullRing, "GT4")] = BuildCombo(TrackGuideCatalogTrack.RedBullRing, "GT4", 1.9),
            [(TrackGuideCatalogTrack.RedBullRing, "Touring")] = BuildCombo(TrackGuideCatalogTrack.RedBullRing, "Touring", 1.7),
            [(TrackGuideCatalogTrack.RedBullRing, "Cup")] = BuildCombo(TrackGuideCatalogTrack.RedBullRing, "Cup", 1.5),
            [(TrackGuideCatalogTrack.RedBullRing, "Hypercar")] = BuildCombo(TrackGuideCatalogTrack.RedBullRing, "Hypercar", 2.8),
            [(TrackGuideCatalogTrack.NurburgringGp, "GT3")] = BuildCombo(TrackGuideCatalogTrack.NurburgringGp, "GT3", 2.5),
            [(TrackGuideCatalogTrack.NurburgringGp, "GT4")] = BuildCombo(TrackGuideCatalogTrack.NurburgringGp, "GT4", 2.1),
            [(TrackGuideCatalogTrack.NurburgringGp, "Hypercar")] = BuildCombo(TrackGuideCatalogTrack.NurburgringGp, "Hypercar", 3.1)
        };

    public static bool TryResolve(string? trackName, string? carClassOrName, out TrackCarKnowledge knowledge)
    {
        knowledge = null!;
        var trackId = TrackGuideCatalogIdentity.Identify(trackName);
        if (trackId == TrackGuideCatalogTrack.None)
        {
            return false;
        }

        var normalizedClass = NormalizeCarClass(carClassOrName);
        if (Combos.TryGetValue((trackId, normalizedClass ?? string.Empty), out var combo))
        {
            knowledge = combo;
            return true;
        }

        if (!Tracks.TryGetValue(trackId, out var track))
        {
            return false;
        }

        Classes.TryGetValue(normalizedClass ?? string.Empty, out var carClass);
        knowledge = Merge(track, carClass, normalizedClass);
        return true;
    }

    public static bool TryGetTrack(string? trackName, out TrackKnowledge track)
    {
        track = null!;
        var trackId = TrackGuideCatalogIdentity.Identify(trackName);
        return trackId != TrackGuideCatalogTrack.None && Tracks.TryGetValue(trackId, out track);
    }

    public static bool TryGetCarClass(string? carClassOrName, out CarClassKnowledge carClass)
    {
        carClass = null!;
        var normalized = NormalizeCarClass(carClassOrName);
        return normalized is not null && Classes.TryGetValue(normalized, out carClass);
    }

    public static string? NormalizeCarClass(string? carClassOrName)
    {
        if (string.IsNullOrWhiteSpace(carClassOrName))
        {
            return null;
        }

        var key = carClassOrName.Trim();
        if (key.Contains("hypercar", StringComparison.OrdinalIgnoreCase)
            || key.Contains("lmh", StringComparison.OrdinalIgnoreCase)
            || key.Contains("lmdh", StringComparison.OrdinalIgnoreCase))
        {
            return "Hypercar";
        }

        if (key.Contains("gt3", StringComparison.OrdinalIgnoreCase))
        {
            return "GT3";
        }

        if (key.Contains("gt4", StringComparison.OrdinalIgnoreCase))
        {
            return "GT4";
        }

        if (key.Contains("touring", StringComparison.OrdinalIgnoreCase)
            || key.Contains("tcr", StringComparison.OrdinalIgnoreCase))
        {
            return "Touring";
        }

        if (key.Contains("cup", StringComparison.OrdinalIgnoreCase)
            || key.Contains("cayman", StringComparison.OrdinalIgnoreCase))
        {
            return "Cup";
        }

        return Classes.ContainsKey(key) ? key : null;
    }

    private static TrackCarKnowledge BuildCombo(
        TrackGuideCatalogTrack trackId,
        string carClass,
        double fuelPerLap,
        string? fuelNote = null)
    {
        var track = Tracks[trackId];
        var cls = Classes[carClass];
        var trackName = TrackGuideCatalogIdentity.CanonicalName(trackId)!;
        return new TrackCarKnowledge(
            trackName,
            carClass,
            fuelNote ?? $"{track.FuelUsageExpectation} Typical {carClass} burn about {fuelPerLap.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} L/lap.",
            Prefer(track.TyreWearExpectation, cls.TyreWearExpectation),
            Prefer(track.TyreWarmupExpectation, cls.TyreWarmupExpectation),
            Prefer(track.BrakeDemand, cls.BrakeDemand),
            Prefer(track.TopSpeedSensitivity, cls.TopSpeedSensitivity),
            Prefer(track.DownforceSensitivity, cls.DownforceSensitivity),
            Prefer(track.OvertakingDifficulty, cls.OvertakingDifficulty),
            track.PitStrategyNotes.Concat(cls.PitStrategyNotes).Take(3).ToArray(),
            track.SetupPriorities.Concat(cls.SetupPriorities).Take(4).ToArray(),
            track.OvertakingZones,
            fuelPerLap,
            cls.FuelSafetyMarginLaps);
    }

    private static TrackCarKnowledge Merge(TrackKnowledge track, CarClassKnowledge? carClass, string? normalizedClass)
    {
        return new TrackCarKnowledge(
            track.TrackName,
            normalizedClass ?? "unknown",
            carClass is null ? track.FuelUsageExpectation : $"{track.FuelUsageExpectation} {carClass.FuelUsageExpectation}",
            Prefer(track.TyreWearExpectation, carClass?.TyreWearExpectation),
            Prefer(track.TyreWarmupExpectation, carClass?.TyreWarmupExpectation),
            Prefer(track.BrakeDemand, carClass?.BrakeDemand),
            Prefer(track.TopSpeedSensitivity, carClass?.TopSpeedSensitivity),
            Prefer(track.DownforceSensitivity, carClass?.DownforceSensitivity),
            Prefer(track.OvertakingDifficulty, carClass?.OvertakingDifficulty),
            track.PitStrategyNotes.Concat(carClass?.PitStrategyNotes ?? []).Take(3).ToArray(),
            track.SetupPriorities.Concat(carClass?.SetupPriorities ?? []).Take(4).ToArray(),
            track.OvertakingZones,
            carClass?.BaselineFuelPerLapLiters,
            carClass?.FuelSafetyMarginLaps ?? 1.0);
    }

    private static string Prefer(string primary, string? secondary) =>
        string.IsNullOrWhiteSpace(secondary) ? primary : $"{primary} {secondary}";
}
