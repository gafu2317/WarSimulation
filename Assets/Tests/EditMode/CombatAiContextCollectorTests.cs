using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;
using static CombatEditModeTestUtil;

public sealed class CombatAiContextCollectorTests
{
    [Test]
    public void Context_CopiesInputCollections()
    {
        var bridges = new List<Vector3> { Vector3.one };
        var context = new CombatAiContext(
            null,
            null,
            null,
            default,
            false,
            default,
            false,
            default,
            bridges,
            null,
            null);

        bridges.Clear();

        Assert.That(context.BridgePositions, Is.EqualTo(new[] { Vector3.one }));
    }

    [Test]
    public void Collect_GathersCharactersMapFeaturesTerrainAndStatus()
    {
        AiContextFixture fixture = CreateFixture();
        try
        {
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
            Assert.That(context.HasOwnStonePosition, Is.True);
            Assert.That(context.OwnStonePosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(context.HasEnemyStonePosition, Is.True);
            Assert.That(context.EnemyStonePosition, Is.EqualTo(new Vector3(8f, 0f, 8f)));
            Assert.That(context.Weather, Is.EqualTo(CombatMapSystem.Weather.Rainy));
            Assert.That(context.BridgePositions, Does.Contain(new Vector3(3f, 0f, 3f)));
            Assert.That(context.AssaultRoutes, Is.Empty);
            Assert.That(context.HighGroundCandidates, Does.Contain(new Vector3(6f, 4f, 5f)));
            Assert.That(context.HighGroundCandidates, Does.Contain(new Vector3(9f, 2f, 9f)));
            Assert.That(context.ForestCandidates.Count, Is.GreaterThan(0));

            CombatCharacterIntel enemyIntel = FindIntel(context.EnemyIntel, fixture.Enemy);
            Assert.That(enemyIntel.HasDirectSight, Is.True);
            Assert.That(enemyIntel.HasMemory, Is.True);
            Assert.That(enemyIntel.RecognizesOwner, Is.False);
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
            Assert.That(rememberedIntel.KnownPosition, Is.EqualTo(new Vector3(4f, 0f, 7f)));
            Assert.That(rememberedIntel.RecognizesOwner, Is.False);
            Assert.That(rememberedIntel.HasObjective, Is.False);

            CombatCharacterIntel allyIntel = FindIntel(context.AllyIntel, fixture.Owner);
            Assert.That(allyIntel.HasObjective, Is.False);

            Assert.That(
                CombatAiAssessmentBuilder.Build(context)
                    .GetValue(CombatAiMetricIndex.EnemyLocationConfidence),
                Is.EqualTo(50f));
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
            Assert.That(context.EnemyIntel, Is.Empty);
            Assert.That(context.HasOwnStonePosition, Is.False);
            Assert.That(context.HasEnemyStonePosition, Is.False);
            Assert.That(context.HighGroundCandidates, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(mapGo);
            Object.DestroyImmediate(systemGo);
        }
    }
}
