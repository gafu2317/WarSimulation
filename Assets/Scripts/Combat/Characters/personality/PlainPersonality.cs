using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

public class PlainPersonality : PersonalityBase
{
    private const float EnemyStoneScore = 100f;
    private const float SearchScore = 10f;

    [SerializeField] private CombatCharacterSystem _characterSystem;
    [SerializeField] private CombatMapSystem _mapSystem;
    [SerializeField, Min(1)] private int _highGroundSearchSamples = 8;
    [SerializeField, Min(1f)] private float _highGroundSearchRadius = 15f;
    [SerializeField] private Vector3 _fallbackSearchOffset = new Vector3(4f, 0f, 4f);

    public override CombatAiPlan DecidePlan()
    {
        Character owner = Owner;
        if (owner == null || owner.Health == null || !owner.Health.CanAct)
        {
            return CombatAiPlan.None;
        }

        CombatObjective objective = DecideObjective();
        CombatMoveTarget moveTarget = CreateMoveTarget(objective);
        SelectBestSkill(out SkillBase skill, out Character skillTarget);

        return new CombatAiPlan(objective, moveTarget, skill, skillTarget);
    }

    public CombatObjective DecideObjective()
    {
        float destroyEnemyStoneScore = TryGetEnemyStonePosition(out _) ? EnemyStoneScore : 0f;
        float searchScore = SearchScore;

        return destroyEnemyStoneScore > searchScore
            ? CombatObjective.DestroyEnemyStone
            : CombatObjective.Search;
    }

    private CombatMoveTarget CreateMoveTarget(CombatObjective objective)
    {
        if (objective == CombatObjective.DestroyEnemyStone &&
            TryGetEnemyStonePosition(out Vector3 enemyStonePosition))
        {
            return CombatMoveTarget.ForPosition(enemyStonePosition);
        }

        if (TryFindHighGroundDestination(out Vector3 highGroundDestination))
        {
            return CombatMoveTarget.ForPosition(highGroundDestination);
        }

        Character owner = Owner;
        Vector3 origin = owner != null ? owner.transform.position : transform.position;
        return CombatMoveTarget.ForPosition(origin + _fallbackSearchOffset);
    }

    private void SelectBestSkill(out SkillBase bestSkill, out Character bestTarget)
    {
        bestSkill = null;
        bestTarget = null;

        WeaponBase weapon = GetCurrentWeapon();
        IReadOnlyList<SkillBase> skills = weapon.Skills;
        if (skills == null || skills.Count == 0) return;

        float bestScore = 0f;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null) continue;
            if (Owner.SkillCooldowns != null && !Owner.SkillCooldowns.IsReady(skill)) continue;

            List<Character> candidates = GetSkillTargetCandidates(skill);
            for (int j = 0; j < candidates.Count; j++)
            {
                Character target = candidates[j];
                if (!IsValidSkillTarget(skill, target)) continue;

                float score = skill.EvaluateScore(Owner, target);
                if (score <= bestScore) continue;

                bestScore = score;
                bestSkill = skill;
                bestTarget = target;
            }
        }
    }

    private List<Character> GetSkillTargetCandidates(SkillBase skill)
    {
        var candidates = new List<Character>();
        if (skill == null) return candidates;

        if (skill.TargetKind == SkillTargetKind.Ally ||
            skill.TargetKind == SkillTargetKind.AllyOrSelf)
        {
            AddAllySkillCandidates(candidates, includeSelf: skill.TargetKind == SkillTargetKind.AllyOrSelf);
            return candidates;
        }

        IReadOnlyList<Character> visibleEnemies = GetVisibleEnemies();
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            Character enemy = visibleEnemies[i];
            if (enemy == null) continue;
            candidates.Add(enemy);
        }

        return candidates;
    }

    private void AddAllySkillCandidates(List<Character> candidates, bool includeSelf)
    {
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        Character owner = Owner;
        if (owner == null) return;

        if (characterSystem != null)
        {
            IReadOnlyList<Character> allies = characterSystem.GetAlliesOf(owner);
            for (int i = 0; i < allies.Count; i++)
            {
                Character ally = allies[i];
                if (ally == null) continue;
                if (!includeSelf && ally == owner) continue;
                candidates.Add(ally);
            }
        }

        if (includeSelf && !candidates.Contains(owner))
        {
            candidates.Add(owner);
        }
    }

    private bool IsValidSkillTarget(SkillBase skill, Character target)
    {
        Character owner = Owner;
        if (skill == null || target == null || target.Health == null || owner == null) return false;

        if (skill.TargetKind == SkillTargetKind.Ally ||
            skill.TargetKind == SkillTargetKind.AllyOrSelf)
        {
            if (target == owner) return target.Health.CanAct;
            return target.Team == owner.Team && target.Health.CanAct;
        }

        return target.Team != owner.Team && target.Health.IsTargetable;
    }

    private bool TryGetEnemyStonePosition(out Vector3 position)
    {
        position = default;

        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        Character owner = Owner;
        if (characterSystem == null || owner == null) return false;

        return characterSystem.TryGetEnemyHomePosition(owner, out position);
    }

    private bool TryFindHighGroundDestination(out Vector3 destination)
    {
        destination = default;

        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null) return false;

        Character owner = Owner;
        Vector3 origin = owner != null ? owner.transform.position : transform.position;
        float currentHeight = mapSystem.TryGetTerrainInfo(origin, out TerrainInfo currentTerrain)
            ? currentTerrain.Height
            : origin.y;
        float bestHeight = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < _highGroundSearchSamples; i++)
        {
            float angle = i * (360f / _highGroundSearchSamples) * Mathf.Deg2Rad;
            Vector3 sample = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _highGroundSearchRadius;
            if (!mapSystem.TryGetTerrainInfo(sample, out TerrainInfo info)) continue;
            if (!info.IsInBounds || info.Height <= bestHeight) continue;

            bestHeight = info.Height;
            destination = new Vector3(sample.x, info.Height, sample.z);
            found = true;
        }

        return found && bestHeight > currentHeight;
    }

    private CombatCharacterSystem ResolveCharacterSystem()
    {
        if (_characterSystem != null) return _characterSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            _characterSystem = context.CharacterSystem;
            return _characterSystem;
        }

        _characterSystem = FindAnyObjectByType<CombatCharacterSystem>();
        return _characterSystem;
    }

    private CombatMapSystem ResolveMapSystem()
    {
        if (_mapSystem != null) return _mapSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.MapSystem != null)
        {
            _mapSystem = context.MapSystem;
            return _mapSystem;
        }

        _mapSystem = FindAnyObjectByType<CombatMapSystem>();
        return _mapSystem;
    }
}
