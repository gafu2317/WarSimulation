using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

public static class CombatSkillTargeting
{
    public static IReadOnlyList<Character> GetEnemiesInRadius(Character owner, Vector3 center, float radius)
    {
        return CollectCharacters(owner, includeAllies: false, center, radius, includeSelf: false, requireRecognition: false);
    }

    public static IReadOnlyList<MagicStone> GetEnemyStones(Character owner)
    {
        return CollectEnemyStones(owner, center: default, radius: float.PositiveInfinity);
    }

    public static IReadOnlyList<MagicStone> GetEnemyStonesInRadius(Character owner, Vector3 center, float radius)
    {
        return CollectEnemyStones(owner, center, radius);
    }

    public static IReadOnlyList<Character> GetAlliesInRadius(
        Character owner,
        Vector3 center,
        float radius,
        bool includeSelf = false)
    {
        return CollectCharacters(owner, includeAllies: true, center, radius, includeSelf, requireRecognition: false);
    }

    public static IReadOnlyList<Character> GetRecognizedEnemies(Character owner)
    {
        if (owner == null)
        {
            return System.Array.Empty<Character>();
        }

        CombatVision vision = owner.Vision;
        if (vision == null)
        {
            return System.Array.Empty<Character>();
        }

        vision.UpdateVision();
        IReadOnlyList<Character> enemies = GetCharactersForTeam(owner, includeAllies: false);
        var recognized = new List<Character>();

        for (int i = 0; i < enemies.Count; i++)
        {
            Character enemy = enemies[i];
            if (enemy == null) continue;
            if (!IsValidCandidate(owner, enemy, includeAllies: false)) continue;
            if (!vision.HasRecognitionOf(enemy)) continue;

            recognized.Add(enemy);
        }

        return recognized;
    }

    public static IReadOnlyList<Character> GetAllAllies(Character owner, bool includeSelf = false)
    {
        return CollectCharacters(
            owner,
            includeAllies: true,
            center: default,
            radius: float.PositiveInfinity,
            includeSelf,
            requireRecognition: false);
    }

    public static SkillExecutionContext CreateEnemyAreaContext(
        Character owner,
        Vector3 center,
        float radius,
        bool includeMagicStones = true)
    {
        IReadOnlyList<Character> targets = GetEnemiesInRadius(owner, center, radius);
        IReadOnlyList<MagicStone> stones = includeMagicStones
            ? GetEnemyStonesInRadius(owner, center, radius)
            : System.Array.Empty<MagicStone>();
        return SkillExecutionContext.ForPoint(center, targets, stones);
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

    public static SkillExecutionContext CreateRecognizedEnemiesContext(Character owner)
    {
        return SkillExecutionContext.ForTargets(GetRecognizedEnemies(owner), GetEnemyStones(owner));
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
        bool includeSelf,
        bool requireRecognition)
    {
        if (owner == null)
        {
            return System.Array.Empty<Character>();
        }

        IReadOnlyList<Character> source = GetCharactersForTeam(owner, includeAllies);
        CombatVision vision = !includeAllies ? owner.Vision : null;
        vision?.UpdateVision();

        float radiusSqr = radius * radius;
        bool filterByRadius = !float.IsPositiveInfinity(radius);
        var results = new List<Character>();

        bool sourceContainsOwner = false;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == owner)
            {
                sourceContainsOwner = true;
                break;
            }
        }

        if (includeAllies &&
            includeSelf &&
            !sourceContainsOwner &&
            IsValidCandidate(owner, owner, includeAllies: true, requireRecognition: false))
        {
            if (!filterByRadius)
            {
                results.Add(owner);
            }
            else
            {
                Vector3 delta = owner.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    results.Add(owner);
                }
            }
        }

        for (int i = 0; i < source.Count; i++)
        {
            Character candidate = source[i];
            if (candidate == null) continue;
            if (candidate == owner && !includeSelf) continue;
            if (!IsValidCandidate(owner, candidate, includeAllies, requireRecognition)) continue;

            if (filterByRadius)
            {
                Vector3 delta = candidate.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr) continue;
            }

            results.Add(candidate);
        }

        return results;
    }

    private static IReadOnlyList<MagicStone> CollectEnemyStones(Character owner, Vector3 center, float radius)
    {
        if (owner == null)
        {
            return System.Array.Empty<MagicStone>();
        }

        float radiusSqr = radius * radius;
        bool filterByRadius = !float.IsPositiveInfinity(radius);
        var results = new List<MagicStone>();
        MagicStone[] stones = Object.FindObjectsByType<MagicStone>(FindObjectsInactive.Exclude);
        for (int i = 0; i < stones.Length; i++)
        {
            MagicStone stone = stones[i];
            if (!IsValidEnemyStone(owner, stone)) continue;

            if (filterByRadius)
            {
                Vector3 delta = stone.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr) continue;
            }

            results.Add(stone);
        }

        return results;
    }

    public static bool IsValidEnemyStone(Character owner, MagicStone stone)
    {
        if (owner == null || stone == null || stone.FeatureIndex < 0) return false;
        if (!IsEnemyStoneType(owner.Team, stone.FeatureType)) return false;

        // 敵魔石の位置は戦闘開始から既知。候補列挙に認識・FOVは不要。
        // 実際に撃つときの視線（向き）は Evaluator 側で要求し、未達なら実行前に向く。
        CombatMagicStoneSystem system = CombatMagicStoneSystemResolver.Resolve();
        return system == null ||
            !system.TryGetState(stone.FeatureIndex, out MagicStoneRuntimeState state) ||
            state.HP > 0;
    }

    private static bool IsEnemyStoneType(CombatTeam ownerTeam, FeatureType featureType)
    {
        return ownerTeam == CombatTeam.Ally
            ? featureType == FeatureType.EnemyMainStone || featureType == FeatureType.EnemySubStone
            : featureType == FeatureType.OwnMainStone || featureType == FeatureType.OwnSubStone;
    }

    private static bool IsValidCandidate(Character owner, Character candidate, bool includeAllies)
    {
        return IsValidCandidate(owner, candidate, includeAllies, requireRecognition: true);
    }

    private static bool IsValidCandidate(
        Character owner,
        Character candidate,
        bool includeAllies,
        bool requireRecognition)
    {
        CombatHealth health = candidate.Health;
        if (health == null) return false;

        if (includeAllies)
        {
            if (candidate != owner && candidate.Team != owner.Team) return false;
            return health.IsAlive;
        }

        if (candidate.Team == owner.Team) return false;
        CombatVision vision = owner.Vision;
        if (requireRecognition && vision != null && !vision.HasRecognitionOf(candidate)) return false;
        return health.IsTargetable;
    }

    private static IReadOnlyList<Character> GetCharactersForTeam(Character owner, bool includeAllies)
    {
        CombatCharacterSystem characterSystem = ResolveCharacterSystem(owner);
        if (characterSystem != null && ContainsCharacter(characterSystem, owner))
        {
            return includeAllies
                ? characterSystem.GetAlliesOf(owner)
                : characterSystem.GetEnemiesOf(owner);
        }

        return FindCharactersByTeam(owner, includeAllies);
    }

    private static IReadOnlyList<Character> FindCharactersByTeam(Character owner, bool includeAllies)
    {
        Character[] allCharacters = Object.FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        if (allCharacters == null || allCharacters.Length == 0)
        {
            return System.Array.Empty<Character>();
        }

        var results = new List<Character>();
        for (int i = 0; i < allCharacters.Length; i++)
        {
            Character candidate = allCharacters[i];
            if (candidate == null || candidate == owner) continue;

            bool sameTeam = candidate.Team == owner.Team;
            if (includeAllies ? sameTeam : !sameTeam)
            {
                results.Add(candidate);
            }
        }

        return results;
    }

    private static bool ContainsCharacter(CombatCharacterSystem system, Character character)
    {
        if (system == null || character == null) return false;
        return system.AllyCharacters.Contains(character) || system.EnemyCharacters.Contains(character);
    }

    private static CombatCharacterSystem ResolveCharacterSystem(Character owner)
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            if (owner == null || ContainsCharacter(context.CharacterSystem, owner))
            {
                return context.CharacterSystem;
            }
        }

        CombatCharacterSystem[] systems = Object.FindObjectsByType<CombatCharacterSystem>(FindObjectsInactive.Exclude);
        if (systems != null)
        {
            for (int i = systems.Length - 1; i >= 0; i--)
            {
                if (owner == null || ContainsCharacter(systems[i], owner))
                {
                    return systems[i];
                }
            }

            return systems.Length > 0 ? systems[systems.Length - 1] : null;
        }

        return null;
    }
}
