using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

internal static partial class CombatEditModeTestUtil
{
    internal static AiContextFixture CreateFixture()
    {
        var fixture = new AiContextFixture();
        fixture.SystemGo = new GameObject("CombatCharacterSystem");
        fixture.MapGo = new GameObject("CombatMapSystem");
        fixture.OwnerGo = new GameObject("Owner");
        fixture.ObserverGo = new GameObject("Observer");
        fixture.EnemyGo = new GameObject("VisibleEnemy");
        fixture.RememberedEnemyGo = new GameObject("RememberedEnemy");

        fixture.System = fixture.SystemGo.AddComponent<CombatCharacterSystem>();
        fixture.MapSystem = fixture.MapGo.AddComponent<CombatMapSystem>();
        fixture.Map = CreateMap();
        fixture.MapSystem.SetCurrentMap(fixture.Map);
        CombatEditModeTestUtil.SetPrivateField(fixture.MapSystem, "<CurrentWeather>k__BackingField", CombatMapSystem.Weather.Rainy);
        CombatEditModeTestUtil.WireMapSystem(fixture.System, fixture.MapSystem);

        fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
        fixture.Observer = fixture.ObserverGo.AddComponent<Character>();
        fixture.Enemy = fixture.EnemyGo.AddComponent<Character>();
        fixture.RememberedEnemy = fixture.RememberedEnemyGo.AddComponent<Character>();

        fixture.Owner.Health.Initialize(30);
        fixture.Observer.Health.Initialize(30);
        fixture.Enemy.Health.Initialize(30);
        fixture.RememberedEnemy.Health.Initialize(30);
        fixture.Owner.EquipWeapon(new Sword());

        fixture.OwnerGo.transform.position = new Vector3(5f, 0f, 4f);
        fixture.ObserverGo.transform.position = new Vector3(5f, 0f, 5f);
        fixture.EnemyGo.transform.position = new Vector3(5f, 0f, 9f);
        fixture.RememberedEnemyGo.transform.position = new Vector3(5f, 0f, -5f);
        AddCharacterCollider(fixture.ObserverGo);
        AddCharacterCollider(fixture.EnemyGo);
        AddCharacterCollider(fixture.RememberedEnemyGo);
        Physics.SyncTransforms();

        fixture.System.AllyCharacters.Add(fixture.Owner);
        fixture.System.AllyCharacters.Add(fixture.Observer);
        fixture.System.EnemyCharacters.Add(fixture.Enemy);
        fixture.System.EnemyCharacters.Add(fixture.RememberedEnemy);
        fixture.System.AssignTeamsFromLists();

        CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
        fixture.Owner.Vision.Initialize();

        fixture.ObserverVision = fixture.Observer.Vision;
        CombatEditModeTestUtil.WireVision(fixture.ObserverVision, fixture.System);
        fixture.ObserverVision.Initialize();

        fixture.Collector = fixture.ObserverGo.GetComponent<CombatAiContextCollector>() ??
            fixture.ObserverGo.AddComponent<CombatAiContextCollector>();
        CombatEditModeTestUtil.WireCollector(fixture.Collector, fixture.System, fixture.MapSystem);
        return fixture;
    }

    internal static MapData CreateMap()
    {
        var height = new HeightMap(12, 12, 1f);
        var ground = new GroundStateGrid(12, 12, 1f);
        height.SetHeight(6, 5, 4f);
        height.SetHeight(9, 9, 2f);

        var map = new MapData(height, ground, seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(8f, 0f, 8f)));
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(2f, 0f, 2f)));
        map.AddFeature(new PlacedFeature(FeatureType.Bridge, new Vector3(3f, 0f, 3f)));
        map.AddForestRegion(new ForestRegion(new Vector2(5f, 7f), 1.25f, 0f, 1f));
        map.AddMountain(new MountainRegion(
            MountainKind.Large,
            new Vector2(6f, 5f),
            2f,
            new Vector2(1.2f, 1.2f),
            0f,
            null));
        map.AddMountain(new MountainRegion(
            MountainKind.Small,
            new Vector2(9f, 9f),
            1f,
            new Vector2(0.8f, 0.8f),
            0f,
            null));
        return map;
    }

    internal static void AddCharacterCollider(GameObject go)
    {
        var collider = go.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.height = 2f;
    }

    internal static CombatCharacterIntel FindIntel(IReadOnlyList<CombatCharacterIntel> intel, Character character)
    {
        for (int i = 0; i < intel.Count; i++)
        {
            if (intel[i].Character == character) return intel[i];
        }

        Assert.Fail($"No intel found for {character.name}.");
        return default;
    }

    internal static CombatAiContext CreatePlannerContext(
        Character owner,
        IReadOnlyList<CombatCharacterIntel> enemyIntel = null,
        IReadOnlyList<CombatCharacterIntel> allyIntel = null,
        bool hasOwnStonePosition = false,
        Vector3 ownStonePosition = default,
        bool hasEnemyStonePosition = false,
        Vector3 enemyStonePosition = default,
        IReadOnlyList<Vector3> highGroundCandidates = null,
        bool hasEnemyStoneHealth = false,
        int enemyStoneHP = 0,
        int enemyStoneMaxHP = 0,
        IReadOnlyList<CombatAiPendingDamage> allyPendingDamage = null,
        IReadOnlyList<CombatAiPendingDamage> enemyPendingDamage = null,
        IReadOnlyList<CombatAiPendingHealing> allyPendingHealing = null,
        IReadOnlyList<CombatAiPendingHealing> enemyPendingHealing = null,
        IReadOnlyList<Vector3> bridgePositions = null,
        IReadOnlyList<Vector3> forestCandidates = null,
        bool hasBlockedMoveDestination = false,
        Vector3 blockedMoveDestination = default)
    {
        return new CombatAiContext(
            owner,
            enemyIntel ?? System.Array.Empty<CombatCharacterIntel>(),
            allyIntel ?? System.Array.Empty<CombatCharacterIntel>(),
            CombatMapSystem.Weather.Sunny,
            hasOwnStonePosition,
            ownStonePosition,
            hasEnemyStonePosition,
            enemyStonePosition,
            bridgePositions ?? System.Array.Empty<Vector3>(),
            highGroundCandidates ?? System.Array.Empty<Vector3>(),
            forestCandidates ?? System.Array.Empty<Vector3>(),
            hasEnemyStoneHealth,
            enemyStoneHP,
            enemyStoneMaxHP,
            allyPendingDamage,
            enemyPendingDamage,
            allyPendingHealing,
            enemyPendingHealing,
            hasBlockedMoveDestination,
            blockedMoveDestination);
    }

    internal static float FindObjectiveScore(CombatAiDebugSnapshot snapshot, CombatObjective objective)
    {
        for (int i = 0; i < snapshot.ObjectiveEntries.Count; i++)
        {
            if (snapshot.ObjectiveEntries[i].Objective == objective)
            {
                return snapshot.ObjectiveEntries[i].Breakdown.Total;
            }
        }

        Assert.Fail("目的候補が見つかりません: " + objective);
        return 0f;
    }

    internal static void AssertPlanMatchesDebugSnapshot(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile)
    {
        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, personalityProfile);
        CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, personalityProfile);

        Assert.That(snapshot, Is.Not.Null);
        Assert.That(plan.Objective, Is.EqualTo(snapshot.SelectedObjective.Objective));
        Assert.That(plan.MoveTarget.Kind, Is.EqualTo(snapshot.SelectedMove.Target.Kind));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(snapshot.SelectedMove.Target.Destination));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.EqualTo(snapshot.SelectedMove.Target.TargetCharacter));
        Assert.That(plan.Skill, Is.EqualTo(snapshot.SelectedSkill.Skill));
        Assert.That(plan.SkillContext.PrimaryTarget, Is.EqualTo(snapshot.SelectedSkill.SkillContext.PrimaryTarget));
        Assert.That(plan.SkillContext.PrimaryStone, Is.EqualTo(snapshot.SelectedSkill.SkillContext.PrimaryStone));
        Assert.That(plan.SkillContext.TargetPoint, Is.EqualTo(snapshot.SelectedSkill.SkillContext.TargetPoint));
    }

    internal static float FindSkillScore(CombatAiDebugSnapshot snapshot, SkillBase skill, Character target)
    {
        for (int i = 0; i < snapshot.SkillEntries.Count; i++)
        {
            if (snapshot.SkillEntries[i].Skill == skill &&
                snapshot.SkillEntries[i].SkillContext.PrimaryTarget == target)
            {
                return snapshot.SkillEntries[i].Breakdown.Total;
            }
        }

        Assert.Fail("スキル候補が見つかりません: " + skill.Name);
        return 0f;
    }

    internal static float FindMoveScore(CombatAiDebugSnapshot snapshot, string code)
    {
        return FindMove(snapshot, code).Breakdown.Total;
    }

    internal static CombatAiMoveCandidateEntry FindMove(CombatAiDebugSnapshot snapshot, string code)
    {
        for (int i = 0; i < snapshot.MoveEntries.Count; i++)
        {
            if (snapshot.MoveEntries[i].Code == code)
            {
                return snapshot.MoveEntries[i];
            }
        }

        Assert.Fail("移動候補が見つかりません: " + code);
        return null;
    }

    internal static CombatCharacterIntel CreateIntel(
        Character character,
        bool hasKnownPosition,
        Vector3 knownPosition,
        bool hasDirectSight = true,
        bool hasMemory = false,
        IReadOnlyList<CombatStatusEffectSnapshot> statusEffects = null,
        bool hasObjective = false,
        CombatObjective objective = default,
        Character intendedTarget = null,
        bool hasIntendedDestination = false,
        Vector3 intendedDestination = default,
        float moveSpeed = 3.5f)
    {
        CombatHealth health = character != null ? character.Health : null;
        WeaponBase weapon = character != null ? character.EquippedWeapon ?? WeaponBase.Unarmed : WeaponBase.Unarmed;
        return new CombatCharacterIntel(
            character,
            character != null ? character.Team : default,
            character != null ? character.transform.position : default,
            hasDirectSight,
            hasMemory,
            hasKnownPosition,
            knownPosition,
            memoryAgeSeconds: hasMemory ? 0f : float.PositiveInfinity,
            recognizesOwner: false,
            hp: health != null ? health.HP : 0,
            maxHp: health != null ? health.MaxHP : 0,
            canAct: health != null && health.CanAct,
            weaponKind: weapon.Kind,
            weaponRange: weapon.Range,
            statusEffects: statusEffects ?? System.Array.Empty<CombatStatusEffectSnapshot>(),
            hasObjective: hasObjective,
            objective: objective,
            moveSpeed: moveSpeed,
            intendedTarget: intendedTarget,
            hasIntendedDestination: hasIntendedDestination,
            intendedDestination: intendedDestination);
    }

    internal sealed class AiPlannerBasicAttackSkill : SkillBase
    {
        public override string Name => "通常攻撃";
        public override float MaxRange => 3f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    internal sealed class AiPlannerBoltCooldownSkill : SkillBase
    {
        public override string Name => "BoltCooldown";
        public override float CooldownSeconds => 10f;
        public override float MaxRange => 3f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    internal sealed class AiPlannerHealSkill : SkillBase
    {
        public override string Name => "PlannerHeal";
        public override SkillTargetKind TargetKind => SkillTargetKind.Ally;
        public override float MaxRange => 10f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    internal sealed class AiPlannerAreaBlastSkill : SkillBase
    {
        public override string Name => "PlannerAreaBlast";
        public override SkillTargetKind TargetKind => SkillTargetKind.Area;
        public override float MaxRange => 10f;
        public override float AreaRadius => 2f;
        public override int EstimateDamage(Character self, SkillExecutionContext context, Character target) => 5;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    internal sealed class AiPlannerLongCastBoltSkill : SkillBase
    {
        public override string Name => "長詠唱攻撃";
        public override float CastTimeSeconds => 2.5f;
        public override float MaxRange => 30f;
        public override int EstimateDamage(Character self, SkillExecutionContext context, Character target) => 5;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    internal sealed class AiContextFixture
    {
        public GameObject SystemGo;
        public GameObject MapGo;
        public GameObject OwnerGo;
        public GameObject ObserverGo;
        public GameObject EnemyGo;
        public GameObject RememberedEnemyGo;
        public CombatCharacterSystem System;
        public CombatMapSystem MapSystem;
        public MapData Map;
        public Character Owner;
        public Character Observer;
        public Character Enemy;
        public Character RememberedEnemy;
        public CombatVision ObserverVision;
        public CombatAiContextCollector Collector;

        public void Destroy()
        {
            if (RememberedEnemyGo != null) Object.DestroyImmediate(RememberedEnemyGo);
            if (EnemyGo != null) Object.DestroyImmediate(EnemyGo);
            if (ObserverGo != null) Object.DestroyImmediate(ObserverGo);
            if (OwnerGo != null) Object.DestroyImmediate(OwnerGo);
            if (MapGo != null) Object.DestroyImmediate(MapGo);
            if (SystemGo != null) Object.DestroyImmediate(SystemGo);
        }
    }
}
