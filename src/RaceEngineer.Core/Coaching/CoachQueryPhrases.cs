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

    public static readonly string[] PushConfidence =
    [
        "can i push",
        "can i push now",
        "should i push",
        "should i push now",
        "can i push harder",
        "mogu li gurnuti",
        "smijem li gurnuti",
        "mogu li povecati tempo",
        "are the tyres ready",
        "are tires ready",
        "tyres ready",
        "tires ready",
        "tyre readiness"
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
        "jesu li gume zagrijane",
        "jesu li gume zagrete"
    ];

    public static readonly string[] TrackIdentity =
    [
        "which track am i on",
        "what track am i on",
        "which track is this",
        "what track is this",
        "which circuit am i on",
        "what circuit am i on",
        "which circuit is this",
        "what circuit is this",
        "where am i racing",
        "what track am i driving",
        "which track am i driving"
    ];

    public static readonly string[] CarIdentity =
    [
        "which car am i in",
        "what car am i in",
        "which car am i driving",
        "what car am i driving",
        "what car is this",
        "which car is this"
    ];

    public static readonly string[] RaceAwareness =
    [
        "gap ahead",
        "gap behind",
        "car ahead",
        "car behind",
        "am i gaining",
        "closing on",
        "who is ahead",
        "who is behind",
        "race position",
        "opponent gap"
    ];

    public static readonly string[] TrackMemory =
    [
        "am i faster than last time",
        "faster than last time",
        "how did i drive this track before",
        "how did i drive here before",
        "what was my fuel use here",
        "fuel use here",
        "last session at",
        "previous session at",
        "my history at",
        "compare to last time"
    ];

    public static readonly string[] Position =
    [
        "what is my position",
        "my position",
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
            .Concat(PushConfidence)
            .Concat(Tyre)
            .Concat(Position)
            .Concat(FuelAmount)
            .Concat(FuelConsumption)
            .Concat(FuelStrategy)
            .Concat(RaceAwareness)
            .Concat(TrackIdentity)
            .Concat(CarIdentity)
            .Concat(TrackMemory)
            .Concat(Fuel)
            .Concat(Strategy)
            .Concat(Pit)
            .Concat(LapTime)
            .Concat(Incidents)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
