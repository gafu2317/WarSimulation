using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatAiContextCollectorTests
{
    [Test]
    public void Collect_GathersCharactersMapFeaturesTerrainAndStatus()
    {
        AiContextFixture fixture = CreateFixture();
        try
        {
            fixture.OwnerPersonality.Tick();
            fixture.Enemy.StatusEffects.Apply(
                CombatStatusEffects.StatKind.STR,
                0.5f,
                10f,
                "TestStrDebuff");
            fixture.Enemy.EquipWeapon(new Sword(range: 7f));
            fixture.ObserverVision.ReceiveSharedMemory(
                fixture.Owner,
                new List<CharacterMemory>
                {
                    new CharacterMemory(fixture.RememberedEnemy, new Vector3(4f, 0f, 7f), Time.time),
                });

            CombatAiContext context = fixture.Collector.Collect(fixture.Observer);

            Assert.That(context.Owner, Is.EqualTo(fixture.Observer));
            Assert.That(context.VisibleEnemies, Does.Contain(fixture.Enemy));
            Assert.That(context.RememberedEnemies, Does.Contain(fixture.RememberedEnemy));
            Assert.That(context.Allies, Does.Contain(fixture.Owner));
            Assert.That(context.HasOwnStonePosition, Is.True);
            Assert.That(context.OwnStonePosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(context.HasEnemyStonePosition, Is.True);
            Assert.That(context.EnemyStonePosition, Is.EqualTo(new Vector3(8f, 0f, 8f)));
            Assert.That(context.Weather, Is.EqualTo(CombatMapSystem.Weather.Rainy));
            Assert.That(context.WindVector, Is.EqualTo(new Vector3(1f, 0f, 0.5f)));
            Assert.That(context.RockPositions, Does.Contain(new Vector3(2f, 0f, 2f)));
            Assert.That(context.BridgePositions, Does.Contain(new Vector3(3f, 0f, 3f)));
            Assert.That(context.HighGroundCandidates, Does.Contain(new Vector3(6f, 4f, 5f)));
            Assert.That(context.HighGroundCandidates, Does.Contain(new Vector3(9f, 2f, 9f)));
            Assert.That(context.ForestCandidates.Count, Is.GreaterThan(0));

            CombatCharacterIntel enemyIntel = FindIntel(context.EnemyIntel, fixture.Enemy);
            Assert.That(enemyIntel.HasDirectSight, Is.True);
            Assert.That(enemyIntel.HasMemory, Is.True);
            Assert.That(enemyIntel.WeaponKind, Is.EqualTo(WeaponKind.Sword));
            Assert.That(enemyIntel.WeaponRange, Is.EqualTo(7f).Within(0.001f));
            Assert.That(enemyIntel.HP, Is.EqualTo(30));
            Assert.That(enemyIntel.MaxHP, Is.EqualTo(30));
            Assert.That(enemyIntel.StatusEffects.Count, Is.EqualTo(1));
            Assert.That(enemyIntel.StatusEffects[0].Key, Is.EqualTo("TestStrDebuff"));
            Assert.That(enemyIntel.StatusEffects[0].IsDebuff, Is.True);
            Assert.That(enemyIntel.HasObjective, Is.False);

            CombatCharacterIntel rememberedIntel = FindIntel(context.EnemyIntel, fixture.RememberedEnemy);
            Assert.That(rememberedIntel.HasDirectSight, Is.False);
            Assert.That(rememberedIntel.HasMemory, Is.True);
            Assert.That(rememberedIntel.HasLastKnownPosition, Is.True);
            Assert.That(rememberedIntel.LastKnownPosition, Is.EqualTo(new Vector3(4f, 0f, 7f)));
            Assert.That(rememberedIntel.HasObjective, Is.False);

            CombatCharacterIntel allyIntel = FindIntel(context.AllyIntel, fixture.Owner);
            Assert.That(allyIntel.HasObjective, Is.True);
            Assert.That(allyIntel.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Collect_WithoutSystems_ReturnsEmptyContext()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject mapGo = new GameObject("CombatMapSystem");
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            CombatMapSystem mapSystem = mapGo.AddComponent<CombatMapSystem>();
            var map = new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), seed: 1);
            mapSystem.SetCurrentMap(map);
            CombatEditModeTestUtil.WireMapSystem(system, mapSystem);

            Character owner = ownerGo.AddComponent<Character>();
            CombatAiContextCollector collector = ownerGo.GetComponent<CombatAiContextCollector>() ??
                ownerGo.AddComponent<CombatAiContextCollector>();
            CombatEditModeTestUtil.WireCollector(collector, system, mapSystem);
            CombatEditModeTestUtil.WireVision(owner.Vision, system);
            owner.Vision.Initialize();

            CombatAiContext context = collector.Collect(owner);

            Assert.That(context.Owner, Is.EqualTo(owner));
            Assert.That(context.VisibleEnemies, Is.Empty);
            Assert.That(context.EnemyIntel, Is.Empty);
            Assert.That(context.HasOwnStonePosition, Is.False);
            Assert.That(context.HasEnemyStonePosition, Is.False);
            Assert.That(context.RockPositions, Is.Empty);
            Assert.That(context.HighGroundCandidates, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(mapGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    private static AiContextFixture CreateFixture()
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
        CombatEditModeTestUtil.SetPrivateField(fixture.MapSystem, "<WindVector>k__BackingField", new Vector3(1f, 0f, 0.5f));
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

        fixture.OwnerPersonality = CombatEditModeTestUtil.EnsurePlainPersonality(fixture.OwnerGo);
        CombatEditModeTestUtil.WirePersonality(fixture.OwnerPersonality, fixture.System, fixture.MapSystem);
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

    private static MapData CreateMap()
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

    private static void AddCharacterCollider(GameObject go)
    {
        var collider = go.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.height = 2f;
    }

    private static CombatCharacterIntel FindIntel(IReadOnlyList<CombatCharacterIntel> intel, Character character)
    {
        for (int i = 0; i < intel.Count; i++)
        {
            if (intel[i].Character == character) return intel[i];
        }

        Assert.Fail($"No intel found for {character.name}.");
        return default;
    }

    private sealed class AiContextFixture
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
        public PlainPersonality OwnerPersonality;
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
