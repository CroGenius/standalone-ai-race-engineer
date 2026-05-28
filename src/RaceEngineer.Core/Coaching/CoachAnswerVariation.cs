using System.Collections.Concurrent;

namespace RaceEngineer.Core.Coaching;

public static class CoachAnswerVariation
{
    private const int MaxRecentPhrases = 6;
    private static readonly ConcurrentDictionary<Guid, SessionCoachMemory> Memories = new();

    public static string Choose(
        Guid sessionId,
        CoachQueryTopic topic,
        string signature,
        IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return "";
        }

        if (candidates.Count == 1)
        {
            Remember(sessionId, topic, signature, candidates[0]);
            return candidates[0];
        }

        var memory = Memories.GetOrAdd(sessionId, _ => new SessionCoachMemory());
        memory.LastSignatures.TryGetValue(topic, out var lastSignature);
        var recent = memory.RecentPhrases.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (string.Equals(lastSignature, signature, StringComparison.Ordinal)
                && recent.Contains(candidate))
            {
                continue;
            }

            Remember(sessionId, topic, signature, candidate);
            return candidate;
        }

        var fallback = candidates[memory.TopicRotationIndex(topic) % candidates.Count];
        Remember(sessionId, topic, signature, fallback);
        return fallback;
    }

    public static void Remember(Guid sessionId, CoachQueryTopic topic, string signature, string phrase)
    {
        var memory = Memories.GetOrAdd(sessionId, _ => new SessionCoachMemory());
        memory.LastSignatures[topic] = signature;
        memory.RecentPhrases.Enqueue(Normalize(phrase));
        while (memory.RecentPhrases.Count > MaxRecentPhrases)
        {
            memory.RecentPhrases.Dequeue();
        }
    }

    private static string Normalize(string phrase) =>
        phrase.Trim().ToLowerInvariant();

    private sealed class SessionCoachMemory
    {
        public Dictionary<CoachQueryTopic, string> LastSignatures { get; } = [];
        public Queue<string> RecentPhrases { get; } = new();
        private readonly Dictionary<CoachQueryTopic, int> rotation = [];

        public int TopicRotationIndex(CoachQueryTopic topic)
        {
            rotation.TryGetValue(topic, out var index);
            rotation[topic] = index + 1;
            return index;
        }
    }
}
