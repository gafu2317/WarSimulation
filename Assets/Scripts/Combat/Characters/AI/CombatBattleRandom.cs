using System.Collections.Generic;
using UnityEngine;

public static class CombatBattleRandom
{
    private static readonly Dictionary<string, int> Counters = new();
    private static readonly Dictionary<int, int> DecisionTicks = new();
    private static int _seed;

    public static void Initialize(int seed)
    {
        _seed = seed;
        Counters.Clear();
        DecisionTicks.Clear();
    }

    public static int GetDecisionInterval(
        Character owner,
        float seconds,
        float decisionIntervalSeconds = 0.5f)
    {
        int ticksPerInterval = Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Max(0.01f, seconds) / Mathf.Max(0.01f, decisionIntervalSeconds)));
        return GetDecisionTick(owner) / ticksPerInterval;
    }

    public static void SetDecisionTick(Character owner, int decisionTick)
    {
        if (owner == null) return;
        DecisionTicks[ResolveParticipantId(owner)] = Mathf.Max(0, decisionTick);
    }

    public static int AdvanceDecisionTick(Character owner)
    {
        if (owner == null) return 0;

        int participantId = ResolveParticipantId(owner);
        DecisionTicks.TryGetValue(participantId, out int decisionTick);
        decisionTick++;
        DecisionTicks[participantId] = decisionTick;
        return decisionTick;
    }

    public static int GetDecisionTick(Character owner)
    {
        if (owner == null) return 0;
        return DecisionTicks.TryGetValue(ResolveParticipantId(owner), out int decisionTick)
            ? decisionTick
            : 0;
    }

    public static int Choose(Character owner, string purpose, int interval, int count)
    {
        if (count <= 1) return 0;
        uint value = StableHash(BuildKey(owner, purpose, interval));
        return (int)(value % (uint)count);
    }

    public static bool Roll(Character owner, string purpose, float probability)
    {
        probability = Mathf.Clamp01(probability);
        string key = BuildKey(owner, purpose, 0);
        Counters.TryGetValue(key, out int counter);
        Counters[key] = counter + 1;
        uint value = StableHash(key + ":" + counter);
        return value / (float)uint.MaxValue < probability;
    }

    private static string BuildKey(Character owner, string purpose, int interval)
    {
        return _seed + ":" + ResolveParticipantId(owner) + ":" + purpose + ":" + interval;
    }

    private static int ResolveParticipantId(Character owner)
    {
        if (owner == null) return 0;
        return owner.BattleParticipantId != 0
            ? owner.BattleParticipantId
            : owner.GetEntityId().GetHashCode();
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint hash = offset;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }
        return hash;
    }
}
