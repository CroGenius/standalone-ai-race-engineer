namespace RaceEngineer.Core.Coaching;

public static class CoachQueryPhrases
{
    public static readonly string[] LosingTime =
    [
        "where am i losing time",
        "losing time",
        "lose time",
        "where am i losing",
        "gdje gubim vrijeme",
        "gdje gubim vreme"
    ];

    public static readonly string[] Braking =
    [
        "how is my braking",
        "my braking",
        "brake status",
        "how is my brake",
        "braking",
        "kako kočim",
        "kako kocim",
        "kakvo mi je kočenje",
        "kakvo mi je kocenje",
        "kočenje",
        "kocenje"
    ];

    public static readonly string[] Throttle =
    [
        "how is my throttle",
        "my throttle",
        "throttle application",
        "throttle",
        "gas",
        "kakav mi je gas",
        "kakav je gas"
    ];

    public static readonly string[] Improvement =
    [
        "what should i improve",
        "what should i work on",
        "what to improve",
        "što trebam popraviti",
        "sto trebam popraviti"
    ];

    public static readonly string[] RacePace =
    [
        "race pace",
        "stint pace",
        "kakav mi je tempo",
        "kakvo mi je tempo"
    ];

    public static readonly string[] Tyre =
    [
        "tyre status",
        "tire status",
        "my tyres",
        "my tires",
        "how are my tyres",
        "how are my tires",
        "kakve su gume",
        "stanje guma",
        "kakvo je stanje guma",
        "are the tyres ready",
        "are tires ready",
        "tyres ready",
        "tires ready",
        "tyre readiness",
        "can i push",
        "can i push now",
        "jesu li gume zagrijane",
        "jesu li gume zagrete"
    ];

    public static readonly string[] Position =
    [
        "what is my position",
        "my position",
        "race position",
        "what position am i",
        "koja mi je pozicija",
        "koja je pozicija",
        "koji sam",
        "pozicija"
    ];

    public static readonly string[] FuelAmount =
    [
        "fuel status",
        "fuel level",
        "how much fuel",
        "how much fuel do i have",
        "koliko goriva imam",
        "koliko imam goriva",
        "koliko goriva"
    ];

    public static readonly string[] FuelConsumption =
    [
        "fuel consumption",
        "koliko trošim",
        "koliko trosim",
        "potrošnja goriva",
        "potrosnja goriva"
    ];

    public static readonly string[] FuelStrategy =
    [
        "can i finish",
        "imam li dovoljno goriva"
    ];

    public static readonly string[] Fuel =
    [
        "fuel plan",
        "fuel strategy"
    ];

    public static readonly string[] Strategy =
    [
        "do i need to pit",
        "should i stay out",
        "what is my strategy",
        "when to pit",
        "pit window",
        "pit now",
        "stay out",
        "strategy",
        "strategija",
        "koliko još mogu voziti",
        "koliko jos mogu voziti",
        "kakva je strategija",
        "kada u boks",
        "trebam li u boks"
    ];

    public static readonly string[] Pit =
    [
        "pit",
        "box",
        "boks",
        "u boks",
        "pit stop",
        "pitstop"
    ];

    public static readonly string[] LapTime =
    [
        "lap time",
        "my lap time",
        "current lap time",
        "what is my lap time",
        "what's my lap time",
        "vrijeme kruga",
        "vreme kruga",
        "koliko mi je vrijeme kruga",
        "koliko mi je vreme kruga",
        "last lap",
        "previous lap",
        "best lap"
    ];

    public static readonly string[] Incidents =
    [
        "incident",
        "incidents",
        "incidenti",
        "recent mistakes",
        "mistake",
        "mistakes"
    ];

    public static readonly string[] EnglishRecognition =
    [
        "fuel status",
        "how much fuel do i have",
        "tyre status",
        "tire status",
        "kakve su gume",
        "koliko goriva imam",
        "gorivo",
        "gume",
        "tempo",
        "pace",
        "pit",
        "box",
        "boks",
        "gas",
        "fuel",
        "strategija",
        "brake status",
        "how is my braking",
        "how is my throttle",
        "last lap",
        "best lap",
        "lap time",
        "recent mistakes",
        "next lap focus",
        "where am i losing time",
        "what should i improve",
        "tell me about this track",
        "setup notes",
        "fuel plan",
        "what is my strategy",
        "should I stay out",
        "do I need to pit",
        "koliko goriva imam",
        "gdje gubim vrijeme",
        "kako kočim",
        "kakav mi je tempo",
        "copy",
        "radio check"
    ];

    public static IEnumerable<string> AllRecognitionPhrases =>
        EnglishRecognition
            .Concat(LosingTime)
            .Concat(Braking)
            .Concat(Throttle)
            .Concat(Improvement)
            .Concat(RacePace)
            .Concat(Tyre)
            .Concat(Position)
            .Concat(FuelAmount)
            .Concat(FuelConsumption)
            .Concat(FuelStrategy)
            .Concat(Fuel)
            .Concat(Strategy)
            .Concat(Pit)
            .Concat(LapTime)
            .Concat(Incidents)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
