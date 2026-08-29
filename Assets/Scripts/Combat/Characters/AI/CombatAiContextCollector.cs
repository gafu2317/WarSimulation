using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatAiContextCollector : MonoBehaviour
{
    [SerializeField] private CombatCharacterSystem _characterSystem;
    [SerializeField] private CombatMapSystem _mapSystem;

    private readonly List<CombatCharacterIntel> _enemyIntel = new();
    private readonly List<CombatCharacterIntel> _allyIntel = new();
    private readonly List<CombatAiAssaultRoute> _assaultRoutes = new();
    private readonly List<Vector3> _highGroundCandidates = new();
    private readonly List<Vector3> _forestCandidates = new();
    private readonly List<CombatAiPendingDamage> _allyPendingDamage = new();
    private readonly List<CombatAiPendingDamage> _enemyPendingDamage = new();
    private readonly List<CombatAiPendingHealing> _allyPendingHealing = new();
    private readonly List<CombatAiPendingHealing> _enemyPendingHealing = new();

    public CombatAiContext Collect(Character owner)
    {
        return Collect(owner, null, false, false, default);
    }

    public CombatAiContext Collect(
        Character owner,
        CombatAiTeamReservations reservations,
        bool perceptionPrepared,
        bool hasBlockedMoveDestination,
        Vector3 blockedMoveDestination,
        Character recentAttacker = null)
    {
        ClearBuffers();

        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        CombatMapSystem mapSystem = ResolveMapSystem();
        CombatVision vision = owner != null ? owner.Vision : null;
        if (!perceptionPrepared)
        {
            vision?.UpdateVision();
        }

        IReadOnlyList<Character> enemies = characterSystem != null && owner != null
            ? characterSystem.GetEnemiesOf(owner)
            : Array.Empty<Character>();
        IReadOnlyList<Character> allies = characterSystem != null && owner != null
            ? characterSystem.GetAlliesOf(owner)
            : Array.Empty<Character>();

        BuildIntel(owner, enemies, vision, reservations, _enemyIntel);
        BuildIntel(owner, allies, vision, reservations, _allyIntel);
        CollectPendingDamage(allies, owner, _allyPendingDamage);
        CollectPendingDamage(enemies, null, _enemyPendingDamage);
        if (owner != null && reservations != null)
        {
            reservations.AppendPendingDamage(owner.Team, _allyPendingDamage, _enemyPendingDamage);
            reservations.AppendPendingHealing(owner.Team, _allyPendingHealing, _enemyPendingHealing);
        }

        Vector3 ownStonePosition = default;
        Vector3 enemyStonePosition = default;
        bool hasOwnStonePosition = characterSystem != null &&
            owner != null &&
            characterSystem.TryGetMainStoneHomePosition(owner, out ownStonePosition);
        bool hasEnemyStonePosition = characterSystem != null &&
            owner != null &&
            characterSystem.TryGetEnemyHomePosition(owner, out enemyStonePosition);

        CollectMapFeatures(
            owner,
            mapSystem,
            out bool hasOwnStoneFeaturePosition,
            out Vector3 ownStoneFeaturePosition,
            out bool hasEnemyStoneFeaturePosition,
            out Vector3 enemyStoneFeaturePosition);
        if (!hasOwnStonePosition && hasOwnStoneFeaturePosition)
        {
            hasOwnStonePosition = true;
            ownStonePosition = ownStoneFeaturePosition;
        }

        if (!hasEnemyStonePosition && hasEnemyStoneFeaturePosition)
        {
            hasEnemyStonePosition = true;
            enemyStonePosition = enemyStoneFeaturePosition;
        }

        CollectTerrainCandidates(mapSystem);
        CollectAssaultRoutes(owner, mapSystem);
        bool hasEnemyStoneHealth = TryGetEnemyStoneHealth(
            owner,
            out int enemyStoneHP,
            out int enemyStoneMaxHP);

        return new CombatAiContext(
            owner,
            _enemyIntel,
            _allyIntel,
            mapSystem != null ? mapSystem.CurrentWeather : default,
            hasOwnStonePosition,
            ownStonePosition,
            hasEnemyStonePosition,
            enemyStonePosition,
            _highGroundCandidates,
            _forestCandidates,
            hasEnemyStoneHealth,
            enemyStoneHP,
            enemyStoneMaxHP,
            _allyPendingDamage,
            _enemyPendingDamage,
            _allyPendingHealing,
            _enemyPendingHealing,
            hasBlockedMoveDestination,
            blockedMoveDestination,
            _assaultRoutes,
            recentAttacker);
    }

    private static bool TryGetEnemyStoneHealth(Character owner, out int hp, out int maxHp)
    {
        hp = 0;
        maxHp = 0;
        CombatMagicStoneSystem stoneSystem = CombatMagicStoneSystemResolver.Resolve();
        FeatureType enemyStoneType = owner != null && owner.Team == CombatTeam.Enemy
            ? FeatureType.OwnMainStone
            : FeatureType.EnemyMainStone;
        if (stoneSystem == null ||
            !stoneSystem.TryGetState(enemyStoneType, out MagicStoneRuntimeState state) ||
            state.MaxHP <= 0)
        {
            return false;
        }

        hp = state.HP;
        maxHp = state.MaxHP;
        return true;
    }

    private void ClearBuffers()
    {
        _enemyIntel.Clear();
        _allyIntel.Clear();
        _assaultRoutes.Clear();
        _highGroundCandidates.Clear();
        _forestCandidates.Clear();
        _allyPendingDamage.Clear();
        _enemyPendingDamage.Clear();
        _allyPendingHealing.Clear();
        _enemyPendingHealing.Clear();
    }

    private static void CollectPendingDamage(
        IReadOnlyList<Character> characters,
        Character excludedSource,
        List<CombatAiPendingDamage> destination)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            Character source = characters[i];
            if (source == excludedSource) continue;
            CombatSkillCaster caster = source != null ? source.SkillCaster : null;
            SkillBase skill = caster != null ? caster.CastingSkill : null;
            if (skill == null || !CombatAiSkillClassifier.IsDamage(skill)) continue;

            SkillExecutionContext castingContext = caster.CastingContext.Capture(source);
            for (int j = 0; j < castingContext.ResolvedTargets.Count; j++)
            {
                Character target = castingContext.ResolvedTargets[j];
                if (target == null || target.Team == source.Team || !target.Health.IsAlive) continue;

                int damage = skill.EstimateDamage(source, castingContext, target);
                if (damage > 0)
                {
                    destination.Add(new CombatAiPendingDamage(target, damage));
                }
            }
        }
    }

    private static void BuildIntel(
        Character owner,
        IReadOnlyList<Character> characters,
        CombatVision vision,
        CombatAiTeamReservations reservations,
        List<CombatCharacterIntel> destination)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || character == owner || ContainsCharacter(destination, character)) continue;

            Vector3 lastKnownPosition = default;
            bool hasLastKnownPosition = vision != null &&
                vision.TryGetLastKnownPosition(character, out lastKnownPosition);
            bool hasMemory = vision != null && vision.HasMemoryOf(character);
            bool hasDirectSight = vision != null && vision.IsVisible(character);
            bool hasKnownPosition = hasDirectSight || (hasMemory && hasLastKnownPosition);
            Vector3 knownPosition = hasDirectSight
                ? character.transform.position
                : hasKnownPosition
                    ? lastKnownPosition
                    : default;
            float memoryAgeSeconds = vision != null ? vision.GetMemoryAgeSeconds(character) : float.PositiveInfinity;
            WeaponBase weapon = character.EquippedWeapon ?? WeaponBase.Unarmed;
            NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
            CombatHealth health = character.Health;
            CombatStatusEffects statusEffectsComponent = character.GetComponent<CombatStatusEffects>();
            CombatVision targetVision = character.Vision;
            IReadOnlyList<CombatStatusEffectSnapshot> statusEffects = statusEffectsComponent != null
                ? statusEffectsComponent.GetActiveEffectSnapshots()
                : Array.Empty<CombatStatusEffectSnapshot>();
            CombatAiBrain brain = character.GetComponent<CombatAiBrain>();
            CombatAiPlan reservedPlan = CombatAiPlan.None;
            bool hasReservedPlan = reservations != null &&
                reservations.TryGetPlan(character, out reservedPlan);
            bool hasObjective = hasReservedPlan || brain != null && brain.LastContext != null;
            CombatAiPlan plan = hasReservedPlan
                ? reservedPlan
                : hasObjective
                    ? brain.LastPlan
                    : CombatAiPlan.None;
            CombatObjective objective = plan.Objective;
            Character intendedTarget = plan.SkillTarget != null
                ? plan.SkillTarget
                : plan.MoveTarget.TargetCharacter;
            bool hasIntendedDestination = hasObjective && plan.MoveTarget.HasDestination;
            bool recognizesOwner = targetVision != null && owner != null && targetVision.HasRecognitionOf(owner);

            destination.Add(new CombatCharacterIntel(
                character,
                character.Team,
                character.transform.position,
                hasDirectSight,
                hasMemory,
                hasKnownPosition,
                knownPosition,
                memoryAgeSeconds,
                recognizesOwner,
                health != null ? health.HP : 0,
                health != null ? health.MaxHP : 0,
                health != null && health.CanAct,
                weapon.Kind,
                weapon.Range,
                CopyStatusEffects(statusEffects),
                hasObjective,
                objective,
                agent != null ? agent.speed : 3.5f,
                intendedTarget,
                hasIntendedDestination,
                hasIntendedDestination ? plan.MoveTarget.Destination : default));
        }
    }

    private static bool ContainsCharacter(List<CombatCharacterIntel> characters, Character target)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].Character == target) return true;
        }

        return false;
    }

    private static IReadOnlyList<CombatStatusEffectSnapshot> CopyStatusEffects(
        IReadOnlyList<CombatStatusEffectSnapshot> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<CombatStatusEffectSnapshot>();

        var copy = new CombatStatusEffectSnapshot[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            copy[i] = source[i];
        }

        return copy;
    }

    private void CollectMapFeatures(
        Character owner,
        CombatMapSystem mapSystem,
        out bool hasOwnStonePosition,
        out Vector3 ownStonePosition,
        out bool hasEnemyStonePosition,
        out Vector3 enemyStonePosition)
    {
        hasOwnStonePosition = false;
        ownStonePosition = default;
        hasEnemyStonePosition = false;
        enemyStonePosition = default;

        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null) return;

        FeatureType ownMainStoneType = owner != null && owner.Team == CombatTeam.Enemy
            ? FeatureType.EnemyMainStone
            : FeatureType.OwnMainStone;
        FeatureType enemyMainStoneType = owner != null && owner.Team == CombatTeam.Enemy
            ? FeatureType.OwnMainStone
            : FeatureType.EnemyMainStone;

        Transform origin = mapSystem.MapOrigin;
        List<PlacedFeature> features = map.Features;
        for (int i = 0; i < features.Count; i++)
        {
            PlacedFeature feature = features[i];
            Vector3 position = origin != null
                ? origin.TransformPoint(feature.WorldPosition)
                : feature.WorldPosition;

            switch (feature.Type)
            {
                case FeatureType.OwnMainStone:
                case FeatureType.EnemyMainStone:
                    if (feature.Type == ownMainStoneType)
                    {
                        hasOwnStonePosition = true;
                        ownStonePosition = position;
                    }
                    else if (feature.Type == enemyMainStoneType)
                    {
                        hasEnemyStonePosition = true;
                        enemyStonePosition = position;
                    }
                    break;
            }
        }
    }

    private void CollectAssaultRoutes(Character owner, CombatMapSystem mapSystem)
    {
        _assaultRoutes.Clear();
        if (owner == null || mapSystem == null) return;

        IReadOnlyList<CombatAiAssaultRoute> cached = CombatAssaultRouteCache.GetRoutes(
            owner.Team,
            mapSystem);
        for (int i = 0; i < cached.Count; i++)
        {
            _assaultRoutes.Add(cached[i]);
        }
    }

    private void CollectTerrainCandidates(CombatMapSystem mapSystem)
    {
        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null) return;

        Transform mapOrigin = mapSystem.MapOrigin;
        List<MountainRegion> mountains = map.Mountains;
        for (int i = 0; i < mountains.Count; i++)
        {
            MountainRegion mountain = mountains[i];
            Vector3 localCenter = new Vector3(mountain.Center.x, 0f, mountain.Center.y);
            _highGroundCandidates.Add(ToWorldSurfacePosition(mapSystem, localCenter));
        }

        List<ForestRegion> forests = map.ForestRegions;
        for (int i = 0; i < forests.Count; i++)
        {
            ForestRegion forest = forests[i];
            Vector3 localCenter = new Vector3(forest.Center.x, 0f, forest.Center.y);
            _forestCandidates.Add(ToWorldSurfacePosition(mapSystem, localCenter));
            for (int directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                float angle = directionIndex * Mathf.PI * 0.25f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float radius = forest.EffectiveRadius(
                    direction.x * forest.Radius,
                    direction.y * forest.Radius) * 0.8f;
                Vector2 point = forest.Center + direction * radius;
                _forestCandidates.Add(ToWorldSurfacePosition(mapSystem, new Vector3(point.x, 0f, point.y)));
            }
        }
    }

    private static Vector3 ToWorldSurfacePosition(CombatMapSystem mapSystem, Vector3 mapLocalPosition)
    {
        return mapSystem != null
            ? mapSystem.MapLocalToSurfaceWorldPosition(mapLocalPosition)
            : mapLocalPosition;
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
