using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;
using static CombatEditModeTestUtil;

public sealed class CombatAiAssessmentTests
{
    [Test]
    public void Assessment_IgnoresDeadAllyWhenEvaluatingFragility()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("DeadAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            ally.Health.TakeDamage(30);

            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) });

            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);

            Assert.That(
                assessment.GetValue(CombatAiMetricIndex.AllyFragility),
                Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Assessment_IgnoresEnemyWithoutKnownPosition()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("UnknownEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(1f, 0f, 0f);

            CombatAiContext context = new CombatAiContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, hasKnownPosition: false, knownPosition: default) },
                allyIntel: System.Array.Empty<CombatCharacterIntel>(),
                weather: CombatMapSystem.Weather.Sunny,
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: false,
                enemyStonePosition: default,
                bridgePositions: System.Array.Empty<Vector3>(),
                highGroundCandidates: System.Array.Empty<Vector3>(),
                forestCandidates: System.Array.Empty<Vector3>());

            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);

            Assert.That(assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat), Is.EqualTo(0f));
            Assert.That(assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue), Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Assessment_IgnoresDeadEnemyAcrossCombatMetrics()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("DeadEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemy.Health.TakeDamage(30);
            enemyGo.transform.position = Vector3.right;

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero);

            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);

            Assert.That(assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat), Is.EqualTo(0f));
            Assert.That(assessment.GetValue(CombatAiMetricIndex.SelfThreat), Is.EqualTo(0f));
            Assert.That(assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue), Is.EqualTo(0f));
            Assert.That(assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence), Is.EqualTo(0f));
            Assert.That(assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel), Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Assessment_WinProximityTracksEnemyMainStoneRemainingHealth()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(8f, 0f, 8f),
                hasEnemyStoneHealth: true,
                enemyStoneHP: 125,
                enemyStoneMaxHP: 500);

            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);

            Assert.That(assessment.GetValue(CombatAiMetricIndex.WinProximity), Is.EqualTo(75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Assessment_IncomingEnemyCastRaisesSelfThreatByPredictedDamage()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("CastingEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(20f, 0f, 0f);
            CombatAiContext safeContext = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) });
            CombatAiContext threatenedContext = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                enemyPendingDamage: new[] { new CombatAiPendingDamage(owner, 15) });

            float safeThreat = CombatAiAssessmentBuilder.Build(safeContext)
                .GetValue(CombatAiMetricIndex.SelfThreat);
            float incomingThreat = CombatAiAssessmentBuilder.Build(threatenedContext)
                .GetValue(CombatAiMetricIndex.SelfThreat);

            Assert.That(incomingThreat - safeThreat, Is.EqualTo(30f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AdvanceProgress_UsesPositionAlongStoneAxisAndIgnoresLateralOffset()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            CombatAiContext context = CreatePlannerContext(
                owner,
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(10f, 0f, 0f));

            Assert.That(CombatAiPositioning.GetAdvanceProgress(context, Vector3.zero), Is.EqualTo(0f));
            Assert.That(CombatAiPositioning.GetAdvanceProgress(context, new Vector3(5f, 0f, 0f)), Is.EqualTo(0.5f));
            Assert.That(CombatAiPositioning.GetAdvanceProgress(context, new Vector3(5f, 0f, 20f)), Is.EqualTo(0.5f));
            Assert.That(CombatAiPositioning.GetAdvanceProgress(context, new Vector3(10f, 0f, 0f)), Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WeaponWeights_UseCodeDefaults()
    {
        Assert.That(
            CombatAiWeaponWeights.GetObjectiveWeight(WeaponKind.Sword, CombatObjective.AttackEnemy),
            Is.EqualTo(24f));
        Assert.That(
            CombatAiWeaponWeights.GetMoveWeight(WeaponKind.Wand, CombatAiMoveCode.TakeHighGround),
            Is.EqualTo(20f));
        Assert.That(
            CombatAiWeaponWeights.GetMoveWeight(WeaponKind.Grimoire, CombatAiMoveCode.TakeHighGround),
            Is.EqualTo(30f));
        Assert.That(
            CombatAiWeaponWeights.GetMoveWeight(WeaponKind.Bible, CombatAiMoveCode.TakeHighGround),
            Is.EqualTo(12f));
        Assert.That(
            CombatAiWeaponWeights.GetMoveWeight(WeaponKind.Rosary, CombatAiMoveCode.TakeHighGround),
            Is.EqualTo(20f));
    }
}
