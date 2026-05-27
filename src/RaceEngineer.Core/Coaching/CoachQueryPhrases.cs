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
        "kako kočim",
        "kako kocim",
        "kakvo mi je kočenje",
        "kakvo mi je kocenje"
    ];

    public static readonly string[] Throttle =
    [
        "how is my throttle",
        "my throttle",
        "throttle application",
        "kakav mi je gas"
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
        "kakav mi je tempo"
    ];

    public static readonly string[] Fuel =
    [
        "fuel status",
        "fuel plan",
        "fuel strategy",
        "koliko goriva",
        "koliko goriva imam"
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
        "koliko još mogu voziti",
        "koliko jos mogu voziti",
        "imam li dovoljno goriva",
        "kakva je strategija",
        "kada u boks",
        "trebam li u boks"
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
        "tyre status",
        "tire status",
        "brake status",
        "last lap",
        "best lap",
        "recent mistakes",
        "next lap focus",
        "where am i losing time",
        "how is my braking",
        "how is my throttle",
        "what should i improve",
        "tell me about this track",
        "setup notes",
        "fuel plan",
        "what is my strategy",
        "should I stay out",
        "do I need to pit",
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
            .Concat(Fuel)
            .Concat(Strategy)
            .Concat(Incidents)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
