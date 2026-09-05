using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

internal sealed class CombatAiWorldSnapshot
{
    private readonly Dictionary<Character, CombatAiCharacterSnapshot> _characters = new();
    private readonly List<PendingDamageSnapshot> _pendingDamage = new();
    private bool _hasOwnStoneHealth;
    private int _ownStoneHP;
    private int _ownStoneMaxHP;
    private bool _hasEnemyStoneHealth;
    private int _enemyStoneHP;
    private int _enemyStoneMaxHP;

    public CombatAiStaticMapSnapshot StaticMap { get; }

    private CombatAiWorldSnapshot(CombatMapSystem mapSystem)
    {
        StaticMap = CombatAiStaticMapCache.Get(mapSystem);
        CaptureStoneHealth();
    }

    public static CombatAiWorldSnapshot Capture(
        IReadOnlyList<Character> allies,
        IReadOnlyList<Character> enemies,
        CombatMapSystem mapSystem)
    {
        var snapshot = new CombatAiWorldSnapshot(mapSystem);
        snapshot.CaptureCharacters(allies);
        snapshot.CaptureCharacters(enemies);
        snapshot.CapturePendingDamage();
        return snapshot;
    }

    public bool TryGetCharacter(Character character, out CombatAiCharacterSnapshot snapshot)
    {
        snapshot = default;
        return character != null && _characters.TryGetValue(character, out snapshot);
    }

    public void AppendPendingDamage(
        CombatTeam observerTeam,
        Character excludedAllySource,
        List<CombatAiPendingDamage> allyDestination,
        List<CombatAiPendingDamage> enemyDestination)
    {
        for (int i = 0; i < _pendingDamage.Count; i++)
        {
            PendingDamageSnapshot pending = _pendingDamage[i];
            bool isAllySource = pending.SourceTeam == observerTeam;
            if (isAllySource && pending.Source == excludedAllySource) continue;

            List<CombatAiPendingDamage> destination = isAllySource
                ? allyDestination
                : enemyDestination;
            destination.Add(new CombatAiPendingDamage(pending.Target, pending.Damage));
        }
    }

    public bool TryGetEnemyStoneHealth(CombatTeam observerTeam, out int hp, out int maxHp)
    {
        bool observesOwnStoneAsEnemy = observerTeam == CombatTeam.Enemy;
        hp = observesOwnStoneAsEnemy ? _ownStoneHP : _enemyStoneHP;
        maxHp = observesOwnStoneAsEnemy ? _ownStoneMaxHP : _enemyStoneMaxHP;
        return observesOwnStoneAsEnemy ? _hasOwnStoneHealth : _hasEnemyStoneHealth;
    }

    private void CaptureCharacters(IReadOnlyList<Character> characters)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || _characters.ContainsKey(character)) continue;

            WeaponBase weapon = character.EquippedWeapon ?? WeaponBase.Unarmed;
            CombatHealth health = character.Health;
            CombatStatusEffects statusEffects = character.StatusEffects;
            CombatAiBrain brain = character.GetComponent<CombatAiBrain>();
            NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
            IReadOnlyList<CombatStatusEffectSnapshot> effects = statusEffects != null
                ? statusEffects.GetActiveEffectSnapshots()
                : Array.Empty<CombatStatusEffectSnapshot>();
            var effectCopy = effects.Count > 0
                ? new CombatStatusEffectSnapshot[effects.Count]
                : Array.Empty<CombatStatusEffectSnapshot>();
            for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                effectCopy[effectIndex] = effects[effectIndex];
            }

            _characters.Add(character, new CombatAiCharacterSnapshot(
                character,
                character.Team,
                character.transform.position,
                health != null ? health.HP : 0,
                health != null ? health.MaxHP : 0,
                health != null && health.CanAct,
                weapon.Kind,
                weapon.Range,
                agent != null ? agent.speed : 3.5f,
                effectCopy,
                character.Vision,
                brain != null && brain.LastContext != null,
                brain != null ? brain.LastPlan : CombatAiPlan.None));
        }
    }

    private void CapturePendingDamage()
    {
        foreach (KeyValuePair<Character, CombatAiCharacterSnapshot> pair in _characters)
        {
            Character source = pair.Key;
            CombatSkillCaster caster = source.SkillCaster;
            SkillBase skill = caster != null ? caster.CastingSkill : null;
            if (skill == null || !CombatAiSkillClassifier.IsDamage(skill)) continue;

            SkillExecutionContext context = caster.CastingContext.Capture(source);
            for (int i = 0; i < context.ResolvedTargets.Count; i++)
            {
                Character target = context.ResolvedTargets[i];
                if (target == null || target.Team == source.Team || target.Health == null || !target.Health.IsAlive)
                {
                    continue;
                }

                int damage = skill.EstimateDamage(source, context, target);
                if (damage > 0)
                {
                    _pendingDamage.Add(new PendingDamageSnapshot(source, source.Team, target, damage));
                }
            }
        }
    }

    private void CaptureStoneHealth()
    {
        CombatMagicStoneSystem stoneSystem = CombatMagicStoneSystemResolver.Resolve();
        if (stoneSystem == null) return;

        if (stoneSystem.TryGetState(FeatureType.OwnMainStone, out MagicStoneRuntimeState ownState) &&
            ownState.MaxHP > 0)
        {
            _hasOwnStoneHealth = true;
            _ownStoneHP = ownState.HP;
            _ownStoneMaxHP = ownState.MaxHP;
        }

        if (stoneSystem.TryGetState(FeatureType.EnemyMainStone, out MagicStoneRuntimeState enemyState) &&
            enemyState.MaxHP > 0)
        {
            _hasEnemyStoneHealth = true;
            _enemyStoneHP = enemyState.HP;
            _enemyStoneMaxHP = enemyState.MaxHP;
        }
    }

    private readonly struct PendingDamageSnapshot
    {
        public Character Source { get; }
        public CombatTeam SourceTeam { get; }
        public Character Target { get; }
        public int Damage { get; }

        public PendingDamageSnapshot(Character source, CombatTeam sourceTeam, Character target, int damage)
        {
            Source = source;
            SourceTeam = sourceTeam;
            Target = target;
            Damage = damage;
        }
    }
}

internal readonly struct CombatAiCharacterSnapshot
{
    public Character Character { get; }
    public CombatTeam Team { get; }
    public Vector3 Position { get; }
    public int HP { get; }
    public int MaxHP { get; }
    public bool CanAct { get; }
    public WeaponKind WeaponKind { get; }
    public float WeaponRange { get; }
    public float MoveSpeed { get; }
    public IReadOnlyList<CombatStatusEffectSnapshot> StatusEffects { get; }
    public CombatVision Vision { get; }
    public bool HasObjective { get; }
    public CombatAiPlan Plan { get; }

    public CombatAiCharacterSnapshot(
        Character character,
        CombatTeam team,
        Vector3 position,
        int hp,
        int maxHp,
        bool canAct,
        WeaponKind weaponKind,
        float weaponRange,
        float moveSpeed,
        IReadOnlyList<CombatStatusEffectSnapshot> statusEffects,
        CombatVision vision,
        bool hasObjective,
        CombatAiPlan plan)
    {
        Character = character;
        Team = team;
        Position = position;
        HP = hp;
        MaxHP = maxHp;
        CanAct = canAct;
        WeaponKind = weaponKind;
        WeaponRange = weaponRange;
        MoveSpeed = moveSpeed;
        StatusEffects = statusEffects;
        Vision = vision;
        HasObjective = hasObjective;
        Plan = plan;
    }
}

internal sealed class CombatAiStaticMapSnapshot
{
    public static readonly CombatAiStaticMapSnapshot Empty = new(
        false, default, false, default,
        Array.Empty<Vector3>(), Array.Empty<CombatAiHighGroundRegion>(), Array.Empty<Vector3>(),
        Array.Empty<CombatAiAssaultRoute>(), Array.Empty<CombatAiAssaultRoute>());

    public bool HasOwnStonePosition { get; }
    public Vector3 OwnStonePosition { get; }
    public bool HasEnemyStonePosition { get; }
    public Vector3 EnemyStonePosition { get; }
    public IReadOnlyList<Vector3> HighGroundCandidates { get; }
    public IReadOnlyList<CombatAiHighGroundRegion> HighGroundRegions { get; }
    public IReadOnlyList<Vector3> ForestCandidates { get; }
    public IReadOnlyList<CombatAiAssaultRoute> AllyAssaultRoutes { get; }
    public IReadOnlyList<CombatAiAssaultRoute> EnemyAssaultRoutes { get; }

    public CombatAiStaticMapSnapshot(
        bool hasOwnStonePosition,
        Vector3 ownStonePosition,
        bool hasEnemyStonePosition,
        Vector3 enemyStonePosition,
        IReadOnlyList<Vector3> highGroundCandidates,
        IReadOnlyList<CombatAiHighGroundRegion> highGroundRegions,
        IReadOnlyList<Vector3> forestCandidates,
        IReadOnlyList<CombatAiAssaultRoute> allyAssaultRoutes,
        IReadOnlyList<CombatAiAssaultRoute> enemyAssaultRoutes)
    {
        HasOwnStonePosition = hasOwnStonePosition;
        OwnStonePosition = ownStonePosition;
        HasEnemyStonePosition = hasEnemyStonePosition;
        EnemyStonePosition = enemyStonePosition;
        HighGroundCandidates = highGroundCandidates;
        HighGroundRegions = highGroundRegions;
        ForestCandidates = forestCandidates;
        AllyAssaultRoutes = allyAssaultRoutes;
        EnemyAssaultRoutes = enemyAssaultRoutes;
    }
}

internal static class CombatAiStaticMapCache
{
    private const float HighGroundExtentRatio = 0.7f;
    private static MapData _map;
    private static Transform _origin;
    private static CombatAiStaticMapSnapshot _snapshot = CombatAiStaticMapSnapshot.Empty;

    public static CombatAiStaticMapSnapshot Get(CombatMapSystem mapSystem)
    {
        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        Transform origin = mapSystem != null ? mapSystem.MapOrigin : null;
        if (ReferenceEquals(map, _map) && origin == _origin) return _snapshot;

        _map = map;
        _origin = origin;
        _snapshot = Build(mapSystem, map);
        return _snapshot;
    }

    private static CombatAiStaticMapSnapshot Build(CombatMapSystem mapSystem, MapData map)
    {
        if (mapSystem == null || map == null) return CombatAiStaticMapSnapshot.Empty;

        bool hasOwnStone = false;
        bool hasEnemyStone = false;
        Vector3 ownStone = default;
        Vector3 enemyStone = default;
        var highGround = new List<Vector3>();
        var highGroundRegions = new List<CombatAiHighGroundRegion>();
        var forests = new List<Vector3>();

        for (int i = 0; i < map.Features.Count; i++)
        {
            PlacedFeature feature = map.Features[i];
            Vector3 position = mapSystem.MapOrigin != null
                ? mapSystem.MapOrigin.TransformPoint(feature.WorldPosition)
                : feature.WorldPosition;
            if (feature.Type == FeatureType.OwnMainStone)
            {
                hasOwnStone = true;
                ownStone = position;
            }
            else if (feature.Type == FeatureType.EnemyMainStone)
            {
                hasEnemyStone = true;
                enemyStone = position;
            }
        }

        for (int i = 0; i < map.Mountains.Count; i++)
        {
            MountainRegion mountain = map.Mountains[i];
            Vector3 localCenter = new(mountain.Center.x, 0f, mountain.Center.y);
            Vector3 center = mapSystem.MapLocalToSurfaceWorldPosition(localCenter);
            highGround.Add(center);
            highGroundRegions.Add(new CombatAiHighGroundRegion(
                center,
                mountain.Extent * HighGroundExtentRatio));
        }

        for (int i = 0; i < map.ForestRegions.Count; i++)
        {
            ForestRegion forest = map.ForestRegions[i];
            Vector3 localCenter = new(forest.Center.x, 0f, forest.Center.y);
            forests.Add(mapSystem.MapLocalToSurfaceWorldPosition(localCenter));
            for (int directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                float angle = directionIndex * Mathf.PI * 0.25f;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float radius = forest.EffectiveRadius(
                    direction.x * forest.Radius,
                    direction.y * forest.Radius) * 0.8f;
                Vector2 point = forest.Center + direction * radius;
                forests.Add(mapSystem.MapLocalToSurfaceWorldPosition(new Vector3(point.x, 0f, point.y)));
            }
        }

        return new CombatAiStaticMapSnapshot(
            hasOwnStone,
            ownStone,
            hasEnemyStone,
            enemyStone,
            highGround.ToArray(),
            highGroundRegions.ToArray(),
            forests.ToArray(),
            SnapshotRoutes(CombatAssaultRouteCache.GetRoutes(CombatTeam.Ally, mapSystem)),
            SnapshotRoutes(CombatAssaultRouteCache.GetRoutes(CombatTeam.Enemy, mapSystem)));
    }

    private static CombatAiAssaultRoute[] SnapshotRoutes(IReadOnlyList<CombatAiAssaultRoute> routes)
    {
        if (routes == null || routes.Count == 0) return Array.Empty<CombatAiAssaultRoute>();

        var snapshot = new CombatAiAssaultRoute[routes.Count];
        for (int i = 0; i < routes.Count; i++) snapshot[i] = routes[i];
        return snapshot;
    }
}
