using System.Collections.Generic;
using UnityEngine;

public static class CombatSkillTargeting
{
    public static IReadOnlyList<Character> GetEnemiesInRadius(Character owner, Vector3 center, float radius)
    {
        return CollectCharacters(owner, includeAllies: false, center, radius, includeSelf: false);
    }

    public static IReadOnlyList<Character> GetAlliesInRadius(
        Character owner,
        Vector3 center,
        float radius,
        bool includeSelf = false)
    {
        return CollectCharacters(owner, includeAllies: true, center, radius, includeSelf);
    }

    public static IReadOnlyList<Character> GetAllEnemies(Character owner)
    {
        return CollectCharacters(owner, includeAllies: false, center: default, radius: float.PositiveInfinity, includeSelf: false);
    }

    public static IReadOnlyList<Character> GetAllAllies(Character owner, bool includeSelf = false)
    {
        return CollectCharacters(owner, includeAllies: true, center: default, radius: float.PositiveInfinity, includeSelf);
    }

    public static SkillExecutionContext CreateEnemyAreaContext(Character owner, Vector3 center, float radius)
    {
        IReadOnlyList<Character> targets = GetEnemiesInRadius(owner, center, radius);
        return SkillExecutionContext.ForPoint(center, targets);
    }

    public static SkillExecutionContext CreateAllyAreaContext(
        Character owner,
        Vector3 center,
        float radius,
        bool includeSelf = false)
    {
        IReadOnlyList<Character> targets = GetAlliesInRadius(owner, center, radius, includeSelf);
        return SkillExecutionContext.ForPoint(center, targets);
    }

    public static SkillExecutionContext CreateAllEnemiesContext(Character owner)
    {
        return SkillExecutionContext.ForTargets(GetAllEnemies(owner));
    }

    public static SkillExecutionContext CreateAllAlliesContext(Character owner, bool includeSelf = false)
    {
        return SkillExecutionContext.ForTargets(GetAllAllies(owner, includeSelf));
    }

    private static IReadOnlyList<Character> CollectCharacters(
        Character owner,
        bool includeAllies,
        Vector3 center,
        float radius,
        bool includeSelf)
    {
        if (owner == null)
        {
            return System.Array.Empty<Character>();
        }

        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        IReadOnlyList<Character> source = includeAllies
            ? characterSystem != null ? characterSystem.GetAlliesOf(owner) : System.Array.Empty<Character>()
            : characterSystem != null ? characterSystem.GetEnemiesOf(owner) : System.Array.Empty<Character>();

        float radiusSqr = radius * radius;
        bool filterByRadius = !float.IsPositiveInfinity(radius);
        var results = new List<Character>();

        for (int i = 0; i < source.Count; i++)
        {
            Character candidate = source[i];
            if (candidate == null) continue;
            if (candidate == owner && !includeSelf) continue;
            if (!IsValidCandidate(owner, candidate, includeAllies)) continue;

            if (filterByRadius)
            {
                Vector3 delta = candidate.transform.position - center;
                if (delta.sqrMagnitude > radiusSqr) continue;
            }

            results.Add(candidate);
        }

        return results;
    }

    private static bool IsValidCandidate(Character owner, Character candidate, bool includeAllies)
    {
        CombatHealth health = candidate.Health;
        if (health == null) return false;

        if (includeAllies)
        {
            if (candidate != owner && candidate.Team != owner.Team) return false;
            return health.IsAlive;
        }

        if (candidate.Team == owner.Team) return false;
        return health.IsTargetable;
    }

    private static CombatCharacterSystem ResolveCharacterSystem()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            return context.CharacterSystem;
        }

        return Object.FindAnyObjectByType<CombatCharacterSystem>();
    }
}
