using UnityEngine;
using WarSimulation.Combat.Map;

public class PlainPersonality : PersonalityBase
{
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

        return new CombatAiPlan(objective, moveTarget, null, null);
    }

    public CombatObjective DecideObjective()
    {
        return TryGetEnemyStonePosition(out _)
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
