using System.Collections.Generic;
using UnityEngine;

public static class CombatAiSkillContextBuilder
{
    public static List<SkillExecutionContext> Build(CombatAiContext context, Character owner, SkillBase skill)
    {
        var contexts = new List<SkillExecutionContext>();
        Build(context, owner, skill, contexts);
        return contexts;
    }

    public static void Build(CombatAiContext context, Character owner, SkillBase skill, List<SkillExecutionContext> contexts)
    {
        contexts.Clear();
        if (owner == null || skill == null)
        {
            contexts.Add(SkillExecutionContext.None);
            return;
        }

        switch (skill.TargetKind)
        {
            case SkillTargetKind.None:
                contexts.Add(SkillExecutionContext.None);
                break;
            case SkillTargetKind.Self:
                contexts.Add(SkillExecutionContext.ForSelf(owner));
                break;
            case SkillTargetKind.Enemy:
                AddEnemyTargets(context, skill, contexts);
                break;
            case SkillTargetKind.Ally:
                AddAllyTargets(context, contexts, owner, includeSelf: false);
                break;
            case SkillTargetKind.AllyOrSelf:
                AddAllyTargets(context, contexts, owner, includeSelf: true);
                break;
            case SkillTargetKind.Point:
                AddPointTargets(context, owner, skill, contexts);
                break;
            case SkillTargetKind.Area:
                AddAreaTargets(context, owner, skill, contexts);
                break;
            case SkillTargetKind.RecognizedEnemies:
                contexts.Add(CombatSkillTargeting.CreateRecognizedEnemiesContext(owner));
                break;
            case SkillTargetKind.AllAllies:
                contexts.Add(CombatSkillTargeting.CreateAllAlliesContext(owner, includeSelf: true));
                break;
            default:
                contexts.Add(SkillExecutionContext.None);
                break;
        }

        if (contexts.Count == 0)
        {
            contexts.Add(SkillExecutionContext.None);
        }
    }

    private static void AddEnemyTargets(CombatAiContext context, SkillBase skill, List<SkillExecutionContext> contexts)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition) continue;

            AddUniqueTarget(contexts, enemy.Character);
        }

        if (skill.CanTargetMagicStone)
        {
            Character owner = context.Owner;
            IReadOnlyList<MagicStone> stones = CombatSkillTargeting.GetEnemyStones(owner);
            for (int i = 0; i < stones.Count; i++)
            {
                AddUniqueTarget(contexts, stones[i]);
            }
        }
    }

    private static void AddAllyTargets(
        CombatAiContext context,
        List<SkillExecutionContext> contexts,
        Character owner,
        bool includeSelf)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null) continue;

            AddUniqueTarget(contexts, ally.Character);
        }

        if (includeSelf)
        {
            AddUniqueTarget(contexts, owner);
        }
    }

    private static void AddPointTargets(
        CombatAiContext context,
        Character owner,
        SkillBase skill,
        List<SkillExecutionContext> contexts)
    {
        bool support = CombatAiSkillClassifier.IsSupport(skill);
        IReadOnlyList<CombatCharacterIntel> targets = support
            ? context.AllyIntel
            : context.EnemyIntel;
        for (int i = 0; i < targets.Count; i++)
        {
            CombatCharacterIntel target = targets[i];
            if (!support && (!target.IsAlive || !target.HasKnownPosition)) continue;
            Vector3 targetPosition = support ? target.CurrentPosition : target.KnownPosition;

            AddUniquePoint(contexts, targetPosition);
            for (int j = i + 1; j < targets.Count; j++)
            {
                CombatCharacterIntel other = targets[j];
                if (!support && (!other.IsAlive || !other.HasKnownPosition)) continue;
                Vector3 otherPosition = support ? other.CurrentPosition : other.KnownPosition;
                if (HorizontalDistance(targetPosition, otherPosition) > skill.AreaRadius * 2f) continue;
                AddUniquePoint(contexts, (targetPosition + otherPosition) * 0.5f);
            }
        }

        if (support && owner != null)
        {
            AddUniquePoint(contexts, owner.transform.position);
        }

        if (!support && context.HasEnemyStonePosition)
        {
            AddUniquePoint(contexts, context.EnemyStonePosition);
        }

        if (contexts.Count == 0 && owner != null)
        {
            AddUniquePoint(contexts, owner.transform.position);
        }
    }

    private static void AddAreaTargets(
        CombatAiContext context,
        Character owner,
        SkillBase skill,
        List<SkillExecutionContext> contexts)
    {
        if (skill == null)
        {
            return;
        }

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;

            AddUniqueArea(contexts, owner, enemy.KnownPosition, skill.AreaRadius, skill);
            for (int j = i + 1; j < context.EnemyIntel.Count; j++)
            {
                CombatCharacterIntel other = context.EnemyIntel[j];
                if (!other.IsAlive || !other.HasKnownPosition) continue;
                if (HorizontalDistance(enemy.KnownPosition, other.KnownPosition) > skill.AreaRadius * 2f) continue;

                Vector3 center = (enemy.KnownPosition + other.KnownPosition) * 0.5f;
                AddUniqueArea(contexts, owner, center, skill.AreaRadius, skill);
            }
        }

        if (context.HasEnemyStonePosition)
        {
            AddUniqueArea(contexts, owner, context.EnemyStonePosition, skill.AreaRadius, skill);
        }

        if (contexts.Count == 0 && owner != null)
        {
            AddUniqueArea(contexts, owner, owner.transform.position, skill.AreaRadius, skill);
        }
    }

    private static void AddUniqueTarget(List<SkillExecutionContext> contexts, Character target)
    {
        if (target == null) return;

        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].PrimaryTarget == target && !contexts[i].HasTargetPoint)
            {
                return;
            }
        }

        contexts.Add(SkillExecutionContext.ForTarget(target));
    }

    private static void AddUniqueTarget(List<SkillExecutionContext> contexts, MagicStone target)
    {
        if (target == null) return;

        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].PrimaryStone == target && !contexts[i].HasTargetPoint)
            {
                return;
            }
        }

        contexts.Add(SkillExecutionContext.ForTarget(target));
    }

    private static void AddUniquePoint(List<SkillExecutionContext> contexts, Vector3 point)
    {
        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].HasTargetPoint &&
                HorizontalDistance(contexts[i].TargetPoint, point) <= 0.1f)
            {
                return;
            }
        }

        contexts.Add(SkillExecutionContext.ForPoint(point));
    }

    private static void AddUniqueArea(
        List<SkillExecutionContext> contexts,
        Character owner,
        Vector3 point,
        float radius,
        SkillBase skill)
    {
        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].HasTargetPoint &&
                HorizontalDistance(contexts[i].TargetPoint, point) <= 0.1f)
            {
                return;
            }
        }

        contexts.Add(CombatSkillTargeting.CreateEnemyAreaContext(owner, point, radius, skill.CanTargetMagicStone));
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
