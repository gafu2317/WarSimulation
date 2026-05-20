using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class SimpleCombatBrainTests
{
    [Test]
    public void Decide_ReturnsIdleMoveAndNoActionWhenOwnerCannotAct()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f);
        try
        {
            fixture.Owner.Health.TakeDamage(100);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.Idle));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.None));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_AttacksVisibleEnemyInRange()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f);
        try
        {
            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.AttackEnemy));
            Assert.That(decision.Action.Target, Is.EqualTo(fixture.Enemy));
            Assert.That(decision.Action.Score, Is.EqualTo(100f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_ChasesVisibleEnemyOutOfRangeWithoutAction()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));
            Assert.That(decision.Move.Target, Is.EqualTo(fixture.Enemy));
            Assert.That(decision.Move.Destination, Is.EqualTo(fixture.Enemy.transform.position));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.None));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_UsesRetreatMoveAtLowHpAndCriticalHp()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f, withMap: true);
        try
        {
            fixture.Owner.Health.TakeDamage(20);

            SimpleCombatBrain.Decision lowHpDecision = fixture.Brain.Decide();

            Assert.That(lowHpDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.RetreatToHome));
            Assert.That(lowHpDecision.Move.Score, Is.EqualTo(90f));
            Assert.That(lowHpDecision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.AttackEnemy));

            fixture.Owner.Health.RestoreFull();
            fixture.Owner.Health.TakeDamage(25);

            SimpleCombatBrain.Decision criticalHpDecision = fixture.Brain.Decide();

            Assert.That(criticalHpDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.RetreatToHome));
            Assert.That(criticalHpDecision.Move.Score, Is.EqualTo(120f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_PrioritizesHomeDefenseWhenVisibleEnemyIsNearHome()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 2f, withMap: true);
        try
        {
            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.DefendHomeBase));
            Assert.That(decision.Move.Score, Is.EqualTo(95f));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.AttackEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_HoldsPositionWhenVisibleEnemyIsAlreadyInRangeAwayFromHome()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 10.5f, withMap: true);
        try
        {
            fixture.OwnerGo.transform.position = new Vector3(0f, 0f, 9f);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.Idle));
            Assert.That(decision.Move.Score, Is.EqualTo(80f));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.AttackEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_AssaultsEnemyBaseWhenNoEnemyVisible()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: -5f, withMap: true);
        try
        {
            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.AssaultEnemyBase));
            Assert.That(decision.Move.Score, Is.EqualTo(60f));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.None));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_FollowsAllyWhenAllyIsFarAway()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        GameObject allyGo = new GameObject("FarAlly");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(30);
            allyGo.transform.position = new Vector3(0f, 0f, 12f);
            fixture.System.AllyCharacters.Add(ally);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.FollowAlly));
            Assert.That(decision.Move.Target, Is.EqualTo(ally));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.None));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_UsesPatrolWhenNoOtherObjectiveExists()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        try
        {
            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.Patrol));
            Assert.That(decision.Move.Score, Is.EqualTo(20f));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.None));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_MovesToLastKnownPositionAfterTargetLeavesSight()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            SimpleCombatBrain.Decision visibleDecision = fixture.Brain.Decide();
            Vector3 lastKnownPosition = fixture.Enemy.transform.position;
            Assert.That(visibleDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));

            fixture.Enemy.transform.position = new Vector3(0f, 0f, -5f);
            SimpleCombatBrain.Decision lostDecision = fixture.Brain.Decide();

            Assert.That(lostDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition));
            Assert.That(lostDecision.Move.Target, Is.EqualTo(fixture.Enemy));
            Assert.That(lostDecision.Move.Destination, Is.EqualTo(lastKnownPosition));
            Assert.That(lostDecision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.None));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_ShieldPrioritizesFollowingMeleeAlly()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        GameObject swordAllyGo = new GameObject("SwordAlly");
        GameObject wandAllyGo = new GameObject("WandAlly");
        try
        {
            fixture.Owner.EquipWeapon(new Shield());

            Character swordAlly = swordAllyGo.AddComponent<Character>();
            swordAlly.SetTeam(CombatTeam.Ally);
            swordAlly.EquipWeapon(new Sword());
            swordAlly.Health.Initialize(30);
            swordAllyGo.transform.position = new Vector3(0f, 0f, 12f);
            fixture.System.AllyCharacters.Add(swordAlly);

            Character wandAlly = wandAllyGo.AddComponent<Character>();
            wandAlly.SetTeam(CombatTeam.Ally);
            wandAlly.EquipWeapon(new Wand());
            wandAlly.Health.Initialize(30);
            wandAllyGo.transform.position = new Vector3(0f, 0f, 14f);
            fixture.System.AllyCharacters.Add(wandAlly);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.FollowAlly));
            Assert.That(decision.Move.Target, Is.EqualTo(swordAlly));
            Assert.That(decision.Move.Score, Is.GreaterThan(75f));
        }
        finally
        {
            Object.DestroyImmediate(swordAllyGo);
            Object.DestroyImmediate(wandAllyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_GrimoireSeeksHighGroundWhenHigherPointExists()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null, withMap: true);
        try
        {
            fixture.Owner.EquipWeapon(new Grimoire());
            fixture.Map.Height.SetHeight(9, 9, 8f);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.MoveToHighGround));
            Assert.That(decision.Move.Score, Is.EqualTo(50f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_WandWithForestBiasMovesToForestWhenNotAlreadyInside()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null, withMap: true);
        try
        {
            fixture.Owner.EquipWeapon(new Wand(hideInForestBias: 40f));
            fixture.Map.AddForestRegion(new ForestRegion(new Vector2(8f, 8f), 3f, 0f, 0.1f));

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.HideInForest));
            Assert.That(decision.Move.Score, Is.EqualTo(40f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Tick_AttacksVisibleEnemyAndDoesNotAttackWhileOnCooldownOrRetreating()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f);
        try
        {
            fixture.Brain.Tick();
            Assert.That(fixture.Enemy.Health.HP, Is.EqualTo(18));

            fixture.Enemy.Health.RestoreFull();
            fixture.Brain.Tick();
            Assert.That(fixture.Enemy.Health.HP, Is.EqualTo(30));

            fixture.Owner.Health.TakeDamage(100);
            fixture.Brain.Tick();
            Assert.That(fixture.Enemy.Health.HP, Is.EqualTo(30));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    private static BrainFixture CreateFixture(float? enemyDistance, bool withMap = false)
    {
        var fixture = new BrainFixture();
        fixture.SystemGo = new GameObject("CombatCharacterSystem");
        fixture.OwnerGo = new GameObject("Owner");

        fixture.System = fixture.SystemGo.AddComponent<CombatCharacterSystem>();
        fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
        fixture.Brain = fixture.OwnerGo.AddComponent<SimpleCombatBrain>();

        fixture.Owner.SetTeam(CombatTeam.Ally);
        fixture.Owner.EquipWeapon(new Sword());
        fixture.Owner.Health.Initialize(30);
        fixture.OwnerGo.transform.position = Vector3.zero;
        fixture.System.AllyCharacters.Add(fixture.Owner);

        if (enemyDistance.HasValue)
        {
            fixture.EnemyGo = new GameObject("Enemy");
            fixture.Enemy = fixture.EnemyGo.AddComponent<Character>();
            fixture.Enemy.SetTeam(CombatTeam.Enemy);
            fixture.Enemy.Health.Initialize(30);
            fixture.EnemyGo.transform.position = new Vector3(0f, 0f, enemyDistance.Value);
            fixture.System.EnemyCharacters.Add(fixture.Enemy);
        }

        if (withMap)
        {
            fixture.MapGo = new GameObject("CombatMapSystem");
            CombatMapSystem mapSystem = fixture.MapGo.AddComponent<CombatMapSystem>();
            fixture.Map = new MapData(new HeightMap(10, 10, 1f), new GroundStateGrid(10, 10, 1f), seed: 1);
            fixture.Map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, Vector3.zero));
            fixture.Map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(0f, 0f, 9f)));
            mapSystem.SetCurrentMap(fixture.Map);
        }

        fixture.System.AssignTeamsFromLists();
        fixture.Owner.Vision.Initialize();

        return fixture;
    }

    private sealed class BrainFixture
    {
        public GameObject MapGo;
        public GameObject SystemGo;
        public GameObject OwnerGo;
        public GameObject EnemyGo;
        public CombatCharacterSystem System;
        public Character Owner;
        public Character Enemy;
        public SimpleCombatBrain Brain;
        public MapData Map;

        public void Destroy()
        {
            Object.DestroyImmediate(MapGo);
            Object.DestroyImmediate(SystemGo);
            Object.DestroyImmediate(OwnerGo);
            Object.DestroyImmediate(EnemyGo);
        }
    }
}
