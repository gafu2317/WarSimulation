using System.Collections.Generic;
using System.Reflection;
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
            fixture.Owner.Health.Initialize(30, 0);

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
            Assert.That(lostDecision.Move.Score, Is.EqualTo(90f));
            Assert.That(fixture.Brain.CurrentTarget, Is.EqualTo(fixture.Enemy));
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
            Assert.That(decision.Move.Score, Is.GreaterThanOrEqualTo(75f));
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
            ConfigureLargeMapForTerrainSearch(fixture);
            fixture.Owner.EquipWeapon(new Grimoire(seekHighGroundBias: 70f));
            Assert.That(fixture.Owner.EquippedWeapon.SeekHighGroundBias, Is.EqualTo(70f));
            fixture.Map.Height.SetHeight(15, 0, 8f);
            BindBrainReferences(fixture);

            Assert.That(
                fixture.MapSystem.TryGetTerrainInfo(new Vector3(15f, 0f, 0f), out TerrainInfo highGround),
                Is.True);
            Assert.That(highGround.Height, Is.EqualTo(8f).Within(0.01f));

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.MoveToHighGround));
            Assert.That(decision.Move.Score, Is.EqualTo(70f));
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
            ConfigureLargeMapForTerrainSearch(fixture);
            fixture.Owner.EquipWeapon(new Wand(hideInForestBias: 70f));
            Assert.That(fixture.Owner.EquippedWeapon.HideInForestBias, Is.EqualTo(70f));
            fixture.Map.AddForestRegion(new ForestRegion(new Vector2(15f, 0f), 2f, 0f, 0.1f));
            BindBrainReferences(fixture);

            Assert.That(
                fixture.MapSystem.TryGetTerrainInfo(new Vector3(15f, 0f, 0f), out TerrainInfo forestCell),
                Is.True);
            Assert.That(forestCell.IsForest, Is.True);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.HideInForest));
            Assert.That(decision.Move.Score, Is.EqualTo(70f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_WandPrefersForestOverChaseWhenEnemyVisibleAndInRange()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f, withMap: true);
        try
        {
            ConfigureLargeMapForTerrainSearch(fixture);
            fixture.Owner.EquipWeapon(new Wand(hideInForestBias: 70f));
            fixture.Map.AddForestRegion(new ForestRegion(new Vector2(15f, 0f), 2f, 0f, 0.1f));
            BindBrainReferences(fixture);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.HideInForest));
            Assert.That(decision.Move.Score, Is.EqualTo(95f));
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.AttackEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_DoesNotClearTargetOnArrivalAtLastKnown()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            fixture.Brain.Decide();
            Vector3 lastKnownPosition = fixture.Enemy.transform.position;

            fixture.Enemy.transform.position = new Vector3(0f, 0f, -5f);
            fixture.OwnerGo.transform.position = lastKnownPosition;
            fixture.Owner.Vision.UpdateVision();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(fixture.Brain.CurrentTarget, Is.EqualTo(fixture.Enemy));
            bool staysOnSearch =
                decision.Move.Kind == SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition ||
                decision.Move.Kind == SimpleCombatBrain.MoveKind.Idle;
            Assert.That(staysOnSearch, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_PrefersLastKnownOverHideInForestWhenWandRemembersEnemy()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f, withMap: true);
        try
        {
            ConfigureLargeMapForTerrainSearch(fixture);
            fixture.Owner.EquipWeapon(new Wand(hideInForestBias: 70f));
            fixture.Map.AddForestRegion(new ForestRegion(new Vector2(15f, 0f), 2f, 0f, 0.1f));
            BindBrainReferences(fixture);

            fixture.Brain.Decide();
            Vector3 lastKnownPosition = fixture.Enemy.transform.position;

            fixture.Enemy.transform.position = new Vector3(0f, 0f, -5f);
            fixture.Owner.Vision.UpdateVision();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition));
            Assert.That(decision.Move.Score, Is.EqualTo(90f));
            Assert.That(decision.Move.Destination, Is.EqualTo(lastKnownPosition));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_ForgetsTargetAfterMemoryTimeout()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            fixture.Brain.Decide();
            Assert.That(fixture.Owner.Vision.HasMemoryOf(fixture.Enemy), Is.True);

            fixture.Enemy.transform.position = new Vector3(0f, 0f, -5f);
            SetVisionLastSeenTime(fixture.Owner.Vision, fixture.Enemy, Time.time - 15f);
            fixture.Owner.Vision.UpdateVision();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(fixture.Brain.CurrentTarget, Is.Null);
            Assert.That(decision.Move.Kind, Is.Not.EqualTo(SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_KeepsChaseEnemyWhenDefendHomeIsOnlySlightlyHigher()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f, withMap: true);
        try
        {
            fixture.Owner.EquipWeapon(new Sword(chaseEnemyBias: 0f));

            SimpleCombatBrain.Decision chaseDecision = fixture.Brain.Decide();
            Assert.That(chaseDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));
            Assert.That(chaseDecision.Move.Score, Is.EqualTo(85f));

            fixture.EnemyGo.transform.position = new Vector3(0f, 0f, 2f);
            SimpleCombatBrain.Decision defendDecision = fixture.Brain.Decide();

            Assert.That(defendDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));
            Assert.That(defendDecision.Move.Target, Is.EqualTo(fixture.Enemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_SwitchesToRetreatImmediatelyDespiteChaseLock()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f, withMap: true);
        try
        {
            SimpleCombatBrain.Decision chaseDecision = fixture.Brain.Decide();
            Assert.That(chaseDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));

            fixture.Owner.Health.TakeDamage(20);
            SimpleCombatBrain.Decision retreatDecision = fixture.Brain.Decide();

            Assert.That(retreatDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.RetreatToHome));
            Assert.That(retreatDecision.Move.Score, Is.EqualTo(90f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_SwitchesAfterMoveIntentLockExpires()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f, withMap: true);
        try
        {
            fixture.Owner.EquipWeapon(new Sword(chaseEnemyBias: 0f));

            SimpleCombatBrain.Decision chaseDecision = fixture.Brain.Decide();
            Assert.That(chaseDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));

            SetPrivateField(fixture.Brain, "_moveIntentLockedUntil", Time.time - 0.01f);
            fixture.EnemyGo.transform.position = new Vector3(0f, 0f, 2f);

            SimpleCombatBrain.Decision defendDecision = fixture.Brain.Decide();

            Assert.That(defendDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.DefendHomeBase));
            Assert.That(defendDecision.Move.Score, Is.EqualTo(95f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_PrefersLastKnownOverLockedChaseEnemy()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            SimpleCombatBrain.Decision chaseDecision = fixture.Brain.Decide();
            Assert.That(chaseDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));

            Vector3 lastKnownPosition = fixture.Enemy.transform.position;
            fixture.Enemy.transform.position = new Vector3(0f, 0f, -5f);
            SimpleCombatBrain.Decision memoryDecision = fixture.Brain.Decide();

            Assert.That(memoryDecision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition));
            Assert.That(memoryDecision.Move.Destination, Is.EqualTo(lastKnownPosition));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_DoesNotFollowAllyWhilePursuingRememberedEnemy()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        GameObject swordAllyGo = new GameObject("SwordAlly");
        try
        {
            fixture.Owner.EquipWeapon(new Shield());

            Character swordAlly = swordAllyGo.AddComponent<Character>();
            swordAlly.SetTeam(CombatTeam.Ally);
            swordAlly.EquipWeapon(new Sword());
            swordAlly.Health.Initialize(30);
            swordAllyGo.transform.position = new Vector3(0f, 0f, 12f);
            fixture.System.AllyCharacters.Add(swordAlly);
            fixture.System.AssignTeamsFromLists();

            fixture.Brain.Decide();
            Vector3 lastKnownPosition = fixture.Enemy.transform.position;
            fixture.Enemy.transform.position = new Vector3(0f, 0f, -5f);

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition));
            Assert.That(decision.Move.Destination, Is.EqualTo(lastKnownPosition));
        }
        finally
        {
            Object.DestroyImmediate(swordAllyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_SwordStillChasesWhenEnemyIsOutOfMeleeRange()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            fixture.Owner.EquipWeapon(new Sword(chaseEnemyBias: 20f));

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.ChaseEnemy));
            Assert.That(decision.Move.Score, Is.EqualTo(105f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_ShieldFollowsMeleeAllyWhenEnemyVisible()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        GameObject swordAllyGo = new GameObject("SwordAlly");
        try
        {
            fixture.Owner.EquipWeapon(new Shield(followMeleeAllyBias: 40f));

            Character swordAlly = swordAllyGo.AddComponent<Character>();
            swordAlly.SetTeam(CombatTeam.Ally);
            swordAlly.EquipWeapon(new Sword());
            swordAlly.Health.Initialize(30);
            swordAllyGo.transform.position = new Vector3(0f, 0f, 12f);
            fixture.System.AllyCharacters.Add(swordAlly);
            fixture.System.AssignTeamsFromLists();
            fixture.Owner.Vision.Initialize();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Move.Kind, Is.EqualTo(SimpleCombatBrain.MoveKind.FollowAlly));
            Assert.That(decision.Move.Target, Is.EqualTo(swordAlly));
            Assert.That(decision.Move.Score, Is.GreaterThanOrEqualTo(105f));
        }
        finally
        {
            Object.DestroyImmediate(swordAllyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_BibleHealsOverAttackWhenEnemyVisibleAndAllyWounded()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        GameObject allyGo = new GameObject("WoundedAlly");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(maxHP: 30, currentHP: 12);
            allyGo.transform.position = new Vector3(0f, 0f, 3f);
            fixture.System.AllyCharacters.Add(ally);

            fixture.Owner.EquipWeapon(new Bible());
            fixture.System.AssignTeamsFromLists();
            fixture.Owner.Vision.Initialize();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(ally));
            Assert.That(decision.Action.Skill, Is.TypeOf<BibleHealSkill>());
            Assert.That(decision.Action.Score, Is.GreaterThan(82f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_RosaryBuffsOverAttackWhenEnemyVisible()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(30);
            allyGo.transform.position = new Vector3(0f, 0f, 3f);
            fixture.System.AllyCharacters.Add(ally);

            fixture.Owner.EquipWeapon(new Rosary());
            fixture.System.AssignTeamsFromLists();
            fixture.Owner.Vision.Initialize();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(ally));
            Assert.That(decision.Action.Skill, Is.TypeOf<RosaryFaithBuffSkill>());
            Assert.That(decision.Action.Score, Is.GreaterThan(82f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_UsesSkillWhenSkillScoreExceedsAttackScore()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f);
        try
        {
            fixture.Owner.EquipWeapon(new TestSkillWeapon(new SkillBase[] { new HighScoreTestSkill() }));

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(fixture.Enemy));
            Assert.That(decision.Action.Skill, Is.TypeOf<HighScoreTestSkill>());
            Assert.That(decision.Action.Score, Is.EqualTo(200f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_GrimoireSelectsStrDebuffSkillWhenEnemyInRangeAndAttackOnCooldown()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            fixture.Owner.EquipWeapon(new Grimoire());
            fixture.Brain.Tick();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(fixture.Enemy));
            Assert.That(decision.Action.Skill, Is.TypeOf<GrimoireStrDebuffSkill>());
            Assert.That(decision.Action.Score, Is.EqualTo(90f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_DoesNotSelectSkillWhileSkillIsOnCooldown()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f);
        try
        {
            var skill = new HighScoreTestSkill(cooldownSeconds: 3f);
            fixture.Owner.EquipWeapon(new TestSkillWeapon(new SkillBase[] { skill }));

            fixture.Brain.Tick();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.AttackEnemy));
            Assert.That(fixture.Owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Tick_StartsCooldownAfterUsingSkill()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 1.5f);
        try
        {
            var skill = new HighScoreTestSkill(cooldownSeconds: 3f);
            fixture.Owner.EquipWeapon(new TestSkillWeapon(new SkillBase[] { skill }));

            Assert.That(fixture.Owner.SkillCooldowns.IsReady(skill), Is.True);

            fixture.Brain.Tick();

            Assert.That(fixture.Owner.SkillCooldowns.IsReady(skill), Is.False);
            Assert.That(fixture.Owner.SkillCooldowns.GetRemainingSeconds(skill), Is.GreaterThan(0f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Tick_StartsCooldownAfterUsingGrimoireStrDebuffSkill()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: 5f);
        try
        {
            fixture.Owner.EquipWeapon(new Grimoire());
            var skill = new GrimoireStrDebuffSkill();

            fixture.Brain.Tick();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();
            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Skill, Is.TypeOf<GrimoireStrDebuffSkill>());

            fixture.Brain.Tick();

            Assert.That(fixture.Owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_BibleSelectsWoundedAllyForHeal()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        GameObject allyGo = new GameObject("WoundedAlly");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(maxHP: 30, currentHP: 12);
            allyGo.transform.position = new Vector3(0f, 0f, 3f);
            fixture.System.AllyCharacters.Add(ally);

            fixture.Owner.UnEquipWeapon();
            fixture.Owner.EquipWeapon(new Bible());
            fixture.Owner.Vision.Initialize();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(ally));
            Assert.That(decision.Action.Skill, Is.TypeOf<BibleHealSkill>());
            Assert.That(decision.Action.Score, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Tick_BibleHealsWoundedAllyAndStartsCooldown()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        GameObject allyGo = new GameObject("WoundedAlly");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(maxHP: 30, currentHP: 12);
            allyGo.transform.position = new Vector3(0f, 0f, 3f);
            fixture.System.AllyCharacters.Add(ally);

            fixture.Owner.UnEquipWeapon();
            fixture.Owner.EquipWeapon(new Bible());
            SetCharacterFai(fixture.Owner, 10);
            fixture.Owner.Vision.Initialize();

            var skill = new BibleHealSkill();
            Assert.That(fixture.Owner.SkillCooldowns.IsReady(skill), Is.True);

            fixture.Brain.Tick();

            Assert.That(ally.Health.HP, Is.GreaterThan(12));
            Assert.That(fixture.Owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_RosarySelectsAllyForFaithBuff()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(30);
            allyGo.transform.position = new Vector3(0f, 0f, 3f);
            fixture.System.AllyCharacters.Add(ally);

            fixture.Owner.UnEquipWeapon();
            fixture.Owner.EquipWeapon(new Rosary());
            fixture.Owner.Vision.Initialize();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(ally));
            Assert.That(decision.Action.Skill, Is.TypeOf<RosaryFaithBuffSkill>());
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            fixture.Destroy();
        }
    }

    [Test]
    public void Decide_RosaryBuffsSelfWhenNoOtherAllyNeedsBuff()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        try
        {
            fixture.Owner.UnEquipWeapon();
            fixture.Owner.EquipWeapon(new Rosary());
            fixture.Owner.Vision.Initialize();

            SimpleCombatBrain.Decision decision = fixture.Brain.Decide();

            Assert.That(decision.Action.Kind, Is.EqualTo(SimpleCombatBrain.ActionKind.UseSkill));
            Assert.That(decision.Action.Target, Is.EqualTo(fixture.Owner));
            Assert.That(decision.Action.Skill, Is.TypeOf<RosaryFaithBuffSkill>());
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Tick_RosaryAppliesFaithBuffWithoutStackingOnRefresh()
    {
        BrainFixture fixture = CreateFixture(enemyDistance: null);
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character ally = allyGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(30);
            allyGo.transform.position = new Vector3(0f, 0f, 3f);
            fixture.System.AllyCharacters.Add(ally);

            fixture.Owner.UnEquipWeapon();
            fixture.Owner.EquipWeapon(new Rosary());
            fixture.Owner.Vision.Initialize();

            fixture.Brain.Tick();

            Assert.That(ally.FAIBuff, Is.EqualTo(1.2f).Within(0.001f));

            fixture.Brain.Tick();

            Assert.That(ally.FAIBuff, Is.EqualTo(1.2f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
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
            fixture.MapSystem = fixture.MapGo.AddComponent<CombatMapSystem>();
            fixture.Map = new MapData(new HeightMap(10, 10, 1f), new GroundStateGrid(10, 10, 1f), seed: 1);
            fixture.Map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, Vector3.zero));
            fixture.Map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(0f, 0f, 9f)));
            fixture.MapSystem.SetCurrentMap(fixture.Map);
            SetPrivateField(fixture.System, "_mapSystem", fixture.MapSystem);
        }

        fixture.Brain = fixture.OwnerGo.AddComponent<SimpleCombatBrain>();
        BindOwnerVisionReferences(fixture, includeMapSystem: withMap);
        if (withMap)
        {
            BindBrainReferences(fixture);
        }
        else
        {
            SetPrivateField(fixture.Brain, "_characterSystem", fixture.System);
        }

        fixture.System.AssignTeamsFromLists();
        fixture.Owner.Vision.Initialize();

        return fixture;
    }

    private static void ConfigureLargeMapForTerrainSearch(BrainFixture fixture)
    {
        fixture.Map = new MapData(
            new HeightMap(32, 32, 1f),
            new GroundStateGrid(32, 32, 1f),
            seed: 1);
        fixture.MapSystem.SetCurrentMap(fixture.Map);
    }

    private static void BindBrainReferences(BrainFixture fixture)
    {
        SetPrivateField(fixture.Brain, "_mapSystem", fixture.MapSystem);
        SetPrivateField(fixture.Brain, "_characterSystem", fixture.System);
        SetPrivateField(fixture.System, "_mapSystem", fixture.MapSystem);
        BindOwnerVisionReferences(fixture, includeMapSystem: true);
    }

    private static void BindOwnerVisionReferences(BrainFixture fixture, bool includeMapSystem)
    {
        SetPrivateField(fixture.Owner.Vision, "_characterSystem", fixture.System);
        if (includeMapSystem)
        {
            SetPrivateField(fixture.Owner.Vision, "_mapSystem", fixture.MapSystem);
        }
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
        public CombatMapSystem MapSystem;

        public void Destroy()
        {
            Object.DestroyImmediate(MapGo);
            Object.DestroyImmediate(SystemGo);
            Object.DestroyImmediate(OwnerGo);
            Object.DestroyImmediate(EnemyGo);
        }
    }

    private sealed class HighScoreTestSkill : SkillBase
    {
        private readonly float _cooldownSeconds;

        public HighScoreTestSkill(float cooldownSeconds = 3f)
        {
            _cooldownSeconds = cooldownSeconds;
        }

        public override string Name => "HighScoreTest";

        public override float CooldownSeconds => _cooldownSeconds;

        public override float EvaluateScore(Character self, Character target) => 200f;

        public override void Execute(Character self, Character target)
        {
        }
    }

    private sealed class TestSkillWeapon : WeaponBase
    {
        private readonly IReadOnlyList<SkillBase> _skills;

        public TestSkillWeapon(IReadOnlyList<SkillBase> skills)
        {
            _skills = skills;
        }

        public override IReadOnlyList<SkillBase> Skills => _skills;
    }

    private static void SetCharacterFai(Character character, int fai)
    {
        PropertyInfo property = typeof(Character).GetProperty(
            nameof(Character.FAI),
            BindingFlags.Public | BindingFlags.Instance);
        property?.SetValue(character, fai);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }

    private static void SetVisionLastSeenTime(CombatVision vision, Character enemy, float lastSeenAt)
    {
        FieldInfo dictionaryField = typeof(CombatVision).GetField(
            "_lastSeenTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(dictionaryField, Is.Not.Null);

        var dictionary = dictionaryField.GetValue(vision) as Dictionary<Character, float>;
        Assert.That(dictionary, Is.Not.Null);
        dictionary[enemy] = lastSeenAt;
    }
}
