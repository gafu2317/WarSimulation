using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class PlainPersonalityTests
{
    [Test]
    public void DecidePlan_SelectsDestroyEnemyStoneWhenEnemyStoneExists()
    {
        PlainFixture fixture = CreateFixture(withEnemyStone: true);
        try
        {
            CombatAiPlan plan = fixture.Personality.DecidePlan();

            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
            Assert.That(plan.MoveTarget.Kind, Is.EqualTo(CombatMoveTargetKind.Position));
            Assert.That(plan.MoveTarget.Destination, Is.EqualTo(new Vector3(0f, 0f, 9f)));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void DecidePlan_SelectsSearchWhenEnemyStoneIsMissing()
    {
        PlainFixture fixture = CreateFixture(withEnemyStone: false);
        try
        {
            CombatAiPlan plan = fixture.Personality.DecidePlan();

            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Search));
            Assert.That(plan.MoveTarget.HasDestination, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void DecidePlan_SelectsHighestScoringReadySkill()
    {
        PlainFixture fixture = CreateFixture(withEnemyStone: true);
        try
        {
            var lowSkill = new TestEnemySkill("Low", 25f);
            var highSkill = new TestEnemySkill("High", 90f);
            fixture.Owner.EquipWeapon(new Sword(range: 10f, skills: new SkillBase[] { lowSkill, highSkill }));
            CreateEnemy(fixture, new Vector3(0f, 0f, 4f));
            fixture.Owner.Vision.Initialize();

            CombatAiPlan plan = fixture.Personality.DecidePlan();

            Assert.That(plan.Skill, Is.EqualTo(highSkill));
            Assert.That(plan.SkillTarget, Is.EqualTo(fixture.Enemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void DecidePlan_LeavesSkillEmptyWhenNoSkillScoresAboveZero()
    {
        PlainFixture fixture = CreateFixture(withEnemyStone: true);
        try
        {
            fixture.Owner.EquipWeapon(new Sword(range: 10f, skills: new SkillBase[] { new TestEnemySkill("Zero", 0f) }));
            CreateEnemy(fixture, new Vector3(0f, 0f, 4f));
            fixture.Owner.Vision.Initialize();

            CombatAiPlan plan = fixture.Personality.DecidePlan();

            Assert.That(plan.Skill, Is.Null);
            Assert.That(plan.SkillTarget, Is.Null);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Tick_SendsPlanDestinationToMovement()
    {
        PlainFixture fixture = CreateFixture(withEnemyStone: true, useTrackingPersonality: true);
        try
        {
            fixture.TrackingPersonality.Tick();

            Assert.That(fixture.TrackingPersonality.MoveCommandCount, Is.EqualTo(1));
            Assert.That(fixture.TrackingPersonality.LastMoveDestination, Is.EqualTo(new Vector3(0f, 0f, 9f)));
            Assert.That(fixture.TrackingPersonality.LastPlan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    private static PlainFixture CreateFixture(bool withEnemyStone, bool useTrackingPersonality = false)
    {
        var fixture = new PlainFixture();
        fixture.SystemGo = new GameObject("CombatCharacterSystem");
        fixture.MapGo = new GameObject("CombatMapSystem");
        fixture.OwnerGo = new GameObject("Owner");

        fixture.System = fixture.SystemGo.AddComponent<CombatCharacterSystem>();
        fixture.MapSystem = fixture.MapGo.AddComponent<CombatMapSystem>();
        fixture.Map = new MapData(new HeightMap(16, 16, 1f), new GroundStateGrid(16, 16, 1f), seed: 1);
        fixture.Map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, Vector3.zero));
        if (withEnemyStone)
        {
            fixture.Map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(0f, 0f, 9f)));
        }

        fixture.MapSystem.SetCurrentMap(fixture.Map);
        SetPrivateField(fixture.System, "_mapSystem", fixture.MapSystem);

        fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
        fixture.Owner.SetTeam(CombatTeam.Ally);
        fixture.Owner.Health.Initialize(30);
        fixture.Owner.EquipWeapon(new Sword());
        fixture.System.AllyCharacters.Add(fixture.Owner);

        fixture.Personality = fixture.OwnerGo.GetComponent<PlainPersonality>();
        if (useTrackingPersonality)
        {
            Object.DestroyImmediate(fixture.Personality);
            fixture.TrackingPersonality = fixture.OwnerGo.AddComponent<TrackingPlainPersonality>();
            fixture.Personality = fixture.TrackingPersonality;
        }

        BindPersonalityReferences(fixture.Personality, fixture.System, fixture.MapSystem);
        fixture.System.AssignTeamsFromLists();
        fixture.Owner.Vision.Initialize();

        return fixture;
    }

    private static void CreateEnemy(PlainFixture fixture, Vector3 position)
    {
        fixture.EnemyGo = new GameObject("Enemy");
        fixture.Enemy = fixture.EnemyGo.AddComponent<Character>();
        fixture.Enemy.SetTeam(CombatTeam.Enemy);
        fixture.Enemy.Health.Initialize(30);
        fixture.EnemyGo.transform.position = position;
        fixture.System.EnemyCharacters.Add(fixture.Enemy);
        fixture.System.AssignTeamsFromLists();
        SetPrivateField(fixture.Owner.Vision, "_characterSystem", fixture.System);
    }

    private static void BindPersonalityReferences(PlainPersonality personality, CombatCharacterSystem system, CombatMapSystem mapSystem)
    {
        SetPrivateField(personality, "_characterSystem", system);
        SetPrivateField(personality, "_mapSystem", mapSystem);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field ??= target.GetType().BaseType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private sealed class PlainFixture
    {
        public GameObject SystemGo;
        public GameObject MapGo;
        public GameObject OwnerGo;
        public GameObject EnemyGo;
        public CombatCharacterSystem System;
        public CombatMapSystem MapSystem;
        public MapData Map;
        public Character Owner;
        public Character Enemy;
        public PlainPersonality Personality;
        public TrackingPlainPersonality TrackingPersonality;

        public void Destroy()
        {
            Object.DestroyImmediate(EnemyGo);
            Object.DestroyImmediate(OwnerGo);
            Object.DestroyImmediate(MapGo);
            Object.DestroyImmediate(SystemGo);
        }
    }

    private sealed class TrackingPlainPersonality : PlainPersonality
    {
        public int MoveCommandCount { get; private set; }
        public Vector3 LastMoveDestination { get; private set; }

        protected override bool TryMoveTo(Vector3 destination)
        {
            MoveCommandCount++;
            LastMoveDestination = destination;
            return true;
        }
    }

    private sealed class TestEnemySkill : SkillBase
    {
        private readonly string _name;
        private readonly float _score;

        public TestEnemySkill(string name, float score)
        {
            _name = name;
            _score = score;
        }

        public override string Name => _name;

        public override float EvaluateScore(Character self, Character target)
        {
            return _score;
        }

        public override void Execute(Character self, Character target)
        {
        }
    }
}
