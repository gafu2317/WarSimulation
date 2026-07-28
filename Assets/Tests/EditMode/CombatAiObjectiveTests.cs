using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;
using static CombatEditModeTestUtil;

public sealed class CombatAiObjectiveTests
{
    [Test]
    public void Planner_LowEnemyMainStoneHealthRaisesDestroyObjectiveScore()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            CombatAiContext fullHealth = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(8f, 0f, 8f),
                hasEnemyStoneHealth: true,
                enemyStoneHP: 500,
                enemyStoneMaxHP: 500);
            CombatAiContext lowHealth = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(8f, 0f, 8f),
                hasEnemyStoneHealth: true,
                enemyStoneHP: 125,
                enemyStoneMaxHP: 500);

            CombatAiDebugSnapshot fullSnapshot = CombatAiPlanner.BuildDebugSnapshot(fullHealth, null);
            CombatAiDebugSnapshot lowSnapshot = CombatAiPlanner.BuildDebugSnapshot(lowHealth, null);
            float fullScore = FindObjectiveScore(fullSnapshot, CombatObjective.DestroyEnemyStone);
            float lowScore = FindObjectiveScore(lowSnapshot, CombatObjective.DestroyEnemyStone);

            Assert.That(lowScore - fullScore, Is.EqualTo(33.75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ReturnsReasonsFromTheSelectedObjectiveEvaluation()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(10);
            enemyGo.transform.position = Vector3.forward;
            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) });
            var reasons = new List<CombatAiReasonCode>();

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(
                context,
                null,
                previousObjective: CombatObjective.Search,
                selectedObjectiveReasons: reasons);
            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(plan.Objective, Is.EqualTo(snapshot.SelectedObjective.Objective));
            Assert.That(reasons, Is.Not.Empty);
            CollectionAssert.AreEqual(snapshot.SelectedObjective.Breakdown.ReasonCodes, reasons);
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_SwordCommitsToKnownEnemyStone()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());

            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo("AdvanceEnemyStone"));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ExcludesObjectivesAndTargetsThatHaveNoLivingEnemy()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject enemyGo = new GameObject("DeadEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new AiPlannerBasicAttackSkill());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemy.Health.TakeDamage(30);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f),
                hasEnemyStoneHealth: true,
                enemyStoneHP: 100,
                enemyStoneMaxHP: 100);
            CombatAiPlan plan = CombatAiPlanner.BuildPlan(
                context,
                null,
                enemy,
                focusCommitmentRemainingSeconds: 10f,
                previousObjective: CombatObjective.AttackEnemy);
            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(
                context,
                null,
                enemy,
                focusCommitmentRemainingSeconds: 10f,
                previousObjective: CombatObjective.AttackEnemy);

            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
            Assert.That(plan.MoveTarget.Destination, Is.EqualTo(new Vector3(17.5f, 0f, 0f)));
            Assert.That(snapshot.ObjectiveEntries.Exists(entry => entry.Objective == CombatObjective.AttackEnemy), Is.False);
            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
            Assert.That(snapshot.SkillEntries.Exists(entry => entry.SkillContext.PrimaryTarget == enemy), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_SwordPrefersAttackEnemyBeforeStoneWhenEnemyIsReachable()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(2f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_SwordPrefersStoneWhenStoneIsAlreadyInMeleeRange()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject enemyGo = new GameObject("NearbyEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(2f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(1.5f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_SwordPrefersStoneWhenEnemyIsFarAway()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject enemyGo = new GameObject("DistantEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(15f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_WandPrefersStoneWhenEnemyIsOnlyAtLongRange()
    {
        GameObject ownerGo = new GameObject("WandOwner");
        GameObject enemyGo = new GameObject("LongRangeEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(22f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_BibleKeepsSearchingWhenEnemyStoneIsKnown()
    {
        GameObject ownerGo = new GameObject("BibleOwner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Bible());

            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.Search));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_GrimoireChoosesAttackEnemyWhenEnemyIsKnown()
    {
        GameObject ownerGo = new GameObject("GrimoireOwner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Grimoire());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(2f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_NumericalAdvantageRaisesOffenseAndLowersRetreat()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            allyGo.transform.position = new Vector3(100f, 0f, 0f);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(2f, 0f, 0f);

            CombatAiContext balanced = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));
            CombatAiContext advantaged = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot balancedSnapshot = CombatAiPlanner.BuildDebugSnapshot(balanced, null);
            CombatAiDebugSnapshot advantagedSnapshot = CombatAiPlanner.BuildDebugSnapshot(advantaged, null);

            Assert.That(
                FindObjectiveScore(advantagedSnapshot, CombatObjective.AttackEnemy) -
                FindObjectiveScore(balancedSnapshot, CombatObjective.AttackEnemy),
                Is.EqualTo(6f).Within(0.001f));
            Assert.That(
                FindObjectiveScore(advantagedSnapshot, CombatObjective.DestroyEnemyStone) -
                FindObjectiveScore(balancedSnapshot, CombatObjective.DestroyEnemyStone),
                Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(
                FindObjectiveScore(advantagedSnapshot, CombatObjective.Retreat) -
                FindObjectiveScore(balancedSnapshot, CombatObjective.Retreat),
                Is.EqualTo(-4.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_NumericalDisadvantageLowersOffenseAndRaisesRetreat()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGoA = new GameObject("EnemyA");
        GameObject enemyGoB = new GameObject("EnemyB");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character enemyA = enemyGoA.AddComponent<Character>();
            enemyA.SetTeam(CombatTeam.Enemy);
            enemyA.Health.Initialize(30);
            enemyGoA.transform.position = new Vector3(2f, 0f, 0f);
            Character enemyB = enemyGoB.AddComponent<Character>();
            enemyB.SetTeam(CombatTeam.Enemy);
            enemyB.Health.Initialize(30);
            enemyGoB.transform.position = new Vector3(3f, 0f, 0f);

            CombatAiContext balanced = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemyA, true, enemyGoA.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));
            CombatAiContext disadvantaged = CreatePlannerContext(
                owner,
                enemyIntel: new[]
                {
                    CreateIntel(enemyA, true, enemyGoA.transform.position),
                    CreateIntel(
                        enemyB,
                        false,
                        default,
                        hasDirectSight: false,
                        hasMemory: false),
                },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot balancedSnapshot = CombatAiPlanner.BuildDebugSnapshot(balanced, null);
            CombatAiDebugSnapshot disadvantagedSnapshot = CombatAiPlanner.BuildDebugSnapshot(disadvantaged, null);

            Assert.That(
                FindObjectiveScore(disadvantagedSnapshot, CombatObjective.AttackEnemy) -
                FindObjectiveScore(balancedSnapshot, CombatObjective.AttackEnemy),
                Is.EqualTo(-6f).Within(0.001f));
            Assert.That(
                FindObjectiveScore(disadvantagedSnapshot, CombatObjective.DestroyEnemyStone) -
                FindObjectiveScore(balancedSnapshot, CombatObjective.DestroyEnemyStone),
                Is.EqualTo(-4.5f).Within(0.001f));
            Assert.That(
                FindObjectiveScore(disadvantagedSnapshot, CombatObjective.Retreat) -
                FindObjectiveScore(balancedSnapshot, CombatObjective.Retreat),
                Is.EqualTo(4.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGoB);
            Object.DestroyImmediate(enemyGoA);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldDefendsThreatenedOwnStone()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = Vector3.right;

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DefendOwnStone));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo("ReturnOwnStone"));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_RosaryPrefersRetreatWhenSelfThreatIsHigh()
    {
        GameObject ownerGo = new GameObject("RosaryOwner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30, 5);
            owner.EquipWeapon(new Rosary());

            CombatAiContext context = CreatePlannerContext(owner);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.Retreat));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }
}
