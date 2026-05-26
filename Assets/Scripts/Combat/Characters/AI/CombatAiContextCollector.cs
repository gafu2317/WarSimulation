using System;
using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
public sealed class CombatAiContextCollector : MonoBehaviour
{
    [SerializeField] private CombatCharacterSystem _characterSystem;
    [SerializeField] private CombatMapSystem _mapSystem;

    private readonly List<Character> _visibleEnemies = new();
    private readonly List<Character> _rememberedEnemies = new();
    private readonly List<Character> _allies = new();
    private readonly List<CombatCharacterIntel> _enemyIntel = new();
    private readonly List<CombatCharacterIntel> _allyIntel = new();
    private readonly List<Vector3> _rockPositions = new();
    private readonly List<Vector3> _bridgePositions = new();
    private readonly List<Vector3> _highGroundCandidates = new();
    private readonly List<Vector3> _forestCandidates = new();

    public CombatAiContext Collect()
    {
        return Collect(GetComponent<Character>());
    }

    public CombatAiContext Collect(Character owner)
    {
        ClearBuffers();

        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        CombatMapSystem mapSystem = ResolveMapSystem();
        CombatVision vision = owner != null ? owner.Vision : null;
        vision?.UpdateVision();

        CopyCharacters(vision != null ? vision.VisibleEnemies : null, _visibleEnemies);
        CopyCharacters(vision != null ? vision.RememberedEnemies : null, _rememberedEnemies);

        IReadOnlyList<Character> enemies = characterSystem != null && owner != null
            ? characterSystem.GetEnemiesOf(owner)
            : Array.Empty<Character>();
        IReadOnlyList<Character> allies = characterSystem != null && owner != null
            ? characterSystem.GetAlliesOf(owner)
            : Array.Empty<Character>();

        CopyAllies(owner, allies, _allies);
        BuildIntel(owner, enemies, vision, _enemyIntel);
        BuildIntel(owner, _allies, vision, _allyIntel);

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

        return new CombatAiContext(
            owner,
            _visibleEnemies.ToArray(),
            _rememberedEnemies.ToArray(),
            _allies.ToArray(),
            _enemyIntel.ToArray(),
            _allyIntel.ToArray(),
            mapSystem != null ? mapSystem.CurrentWeather : default,
            mapSystem != null ? mapSystem.WindVector : Vector3.zero,
            hasOwnStonePosition,
            ownStonePosition,
            hasEnemyStonePosition,
            enemyStonePosition,
            _rockPositions.ToArray(),
            _bridgePositions.ToArray(),
            _highGroundCandidates.ToArray(),
            _forestCandidates.ToArray());
    }

    private void ClearBuffers()
    {
        _visibleEnemies.Clear();
        _rememberedEnemies.Clear();
        _allies.Clear();
        _enemyIntel.Clear();
        _allyIntel.Clear();
        _rockPositions.Clear();
        _bridgePositions.Clear();
        _highGroundCandidates.Clear();
        _forestCandidates.Clear();
    }

    private static void CopyCharacters(IReadOnlyList<Character> source, List<Character> destination)
    {
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];
            if (character == null || destination.Contains(character)) continue;

            destination.Add(character);
        }
    }

    private static void CopyAllies(Character owner, IReadOnlyList<Character> source, List<Character> destination)
    {
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            Character ally = source[i];
            if (ally == null || ally == owner || destination.Contains(ally)) continue;

            destination.Add(ally);
        }
    }

    private static void BuildIntel(
        Character owner,
        IReadOnlyList<Character> characters,
        CombatVision vision,
        List<CombatCharacterIntel> destination)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || character == owner) continue;

            Vector3 lastKnownPosition = default;
            bool hasLastKnownPosition = vision != null &&
                vision.TryGetLastKnownPosition(character, out lastKnownPosition);
            bool hasMemory = vision != null && vision.HasMemoryOf(character);
            float memoryAgeSeconds = vision != null ? vision.GetMemoryAgeSeconds(character) : float.PositiveInfinity;
            WeaponBase weapon = character.EquippedWeapon ?? WeaponBase.Unarmed;
            CombatHealth health = character.Health;
            PersonalityBase personality = character.GetComponent<PersonalityBase>();
            CombatStatusEffects statusEffectsComponent = character.GetComponent<CombatStatusEffects>();
            IReadOnlyList<CombatStatusEffectSnapshot> statusEffects = statusEffectsComponent != null
                ? statusEffectsComponent.GetActiveEffectSnapshots()
                : Array.Empty<CombatStatusEffectSnapshot>();
            bool hasObjective = personality != null && personality.HasPlannedOnce;
            CombatObjective objective = hasObjective
                ? personality.LastPlan.Objective
                : default;

            destination.Add(new CombatCharacterIntel(
                character,
                character.Team,
                character.transform.position,
                vision != null && vision.IsVisible(character),
                hasMemory,
                hasLastKnownPosition,
                hasLastKnownPosition ? lastKnownPosition : default,
                memoryAgeSeconds,
                health != null ? health.HP : 0,
                health != null ? health.MaxHP : 0,
                health != null && health.CanAct,
                weapon.Kind,
                weapon.Range,
                CopyStatusEffects(statusEffects),
                hasObjective,
                objective));
        }
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
                case FeatureType.Rock:
                    _rockPositions.Add(position);
                    break;
                case FeatureType.Bridge:
                    _bridgePositions.Add(position);
                    break;
            }
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
