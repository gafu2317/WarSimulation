using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatAiStatePlannerTests
{
    private readonly List<UnityEngine.Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
        {
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
    }

    [Test]
    public void Planner_SelectsEmergencyRetreatAtOrBelowFifteenPercentBeforePersonality()
    {
        Character owner = CreateCharacter("Owner", new Shield(), new Vector3(30f, 0f, 0f), 100, 15);
        CombatAiContext context = Context(
            owner,
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EmergencyRetreat));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.ReturnOwnStone));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void Planner_DoesNotEmergencyRetreatAboveFifteenPercentWhenEnemyApproaches()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(30f, 0f, 0f), 100, 16);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(31f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.Objective, Is.Not.EqualTo(CombatObjective.EmergencyRetreat));
    }

    [Test]
    public void Planner_ReleasesEmergencyRetreatInsideOwnStoneArea()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(17f, 0f, 0f), 100, 15);
        CombatAiContext context = Context(
            owner,
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            null,
            previousObjective: CombatObjective.EmergencyRetreat,
            previousMoveTarget: CombatMoveTarget.ForPosition(Vector3.zero));

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.Objective, Is.Not.EqualTo(CombatObjective.EmergencyRetreat));
    }

    [Test]
    public void Planner_ReentersEmergencyRetreatAfterLeavingOwnStoneArea()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(19f, 0f, 0f), 100, 15);
        CombatAiContext context = Context(
            owner,
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            null,
            previousObjective: CombatObjective.AttackEnemy);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.ReturnOwnStone));
    }

    [Test]
    public void Planner_RetainsRosaryEmergencyRetreatUntilHpReachesFiftyPercent()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(30f, 0f, 0f), 100, 49);
        Character rosary = CreateCharacter("Rosary", new Rosary(), new Vector3(25f, 0f, 0f));
        CombatAiContext context = Context(
            owner,
            allies: new[] { Intel(rosary) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));

        CombatAiPlan initialPlan = CombatAiPlanner.BuildPlan(context, null);
        owner.transform.position = rosary.transform.position;
        CombatAiPlan waitingPlan = CombatAiPlanner.BuildPlan(
            context,
            null,
            previousObjective: CombatObjective.EmergencyRetreat,
            previousMoveTarget: initialPlan.MoveTarget);

        Assert.That(initialPlan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(initialPlan.MoveTarget.TargetCharacter, Is.SameAs(rosary));
        Assert.That(waitingPlan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(waitingPlan.MoveTarget.TargetCharacter, Is.SameAs(rosary));

        owner.Health.Initialize(100, 50);
        CombatAiPlan recoveredPlan = CombatAiPlanner.BuildPlan(
            context,
            null,
            previousObjective: CombatObjective.EmergencyRetreat,
            previousMoveTarget: waitingPlan.MoveTarget);

        Assert.That(recoveredPlan.Objective, Is.Not.EqualTo(CombatObjective.EmergencyRetreat));
    }

    [Test]
    public void Planner_ReusesEmergencyRetreatDestinationUntilItBecomesInvalid()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(30f, 0f, 0f), 100, 15);
        Character firstRosary = CreateCharacter("FirstRosary", new Rosary(), new Vector3(25f, 0f, 0f));
        Character secondRosary = CreateCharacter("SecondRosary", new Rosary(), new Vector3(20f, 0f, 0f));
        CombatAiContext context = Context(
            owner,
            allies: new[] { Intel(firstRosary), Intel(secondRosary) },
            ownStone: Vector3.zero);

        CombatAiPlan initialPlan = CombatAiPlanner.BuildPlan(context, null);
        secondRosary.transform.position = new Vector3(29f, 0f, 0f);
        CombatAiContext closerAlternativeContext = Context(
            owner,
            allies: new[] { Intel(firstRosary), Intel(secondRosary) },
            ownStone: Vector3.zero);
        CombatAiPlan retainedPlan = CombatAiPlanner.BuildPlan(
            closerAlternativeContext,
            null,
            previousObjective: CombatObjective.EmergencyRetreat,
            previousMoveTarget: initialPlan.MoveTarget);

        Assert.That(initialPlan.MoveTarget.TargetCharacter, Is.SameAs(firstRosary));
        Assert.That(retainedPlan.MoveTarget.TargetCharacter, Is.SameAs(firstRosary));

        firstRosary.Health.Initialize(100, 0);
        CombatAiContext invalidTargetContext = Context(
            owner,
            allies: new[] { Intel(firstRosary), Intel(secondRosary) },
            ownStone: Vector3.zero);
        CombatAiPlan fallbackPlan = CombatAiPlanner.BuildPlan(
            invalidTargetContext,
            null,
            previousObjective: CombatObjective.EmergencyRetreat,
            previousMoveTarget: retainedPlan.MoveTarget);

        Assert.That(fallbackPlan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(fallbackPlan.MoveTarget.TargetCharacter, Is.SameAs(secondRosary));
    }

    [Test]
    public void Planner_HoldsEmergencyRetreatWhenNoDestinationIsAvailable()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(30f, 0f, 0f), 100, 15);
        CombatAiContext context = Context(owner);

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.HoldPosition));
        Assert.That(plan.MoveTarget.HasDestination, Is.False);
    }

    [Test]
    public void Planner_DoesNotSelectAnAttackSkillDuringEmergencyRetreat()
    {
        Character owner = CreateCharacter("Owner", new Sword(), new Vector3(30f, 0f, 0f), 100, 15);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(31f, 0f, 0f), team: CombatTeam.Enemy);
        var attack = new CombatEditModeTestUtil.AiPlannerBasicAttackSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, attack);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: Vector3.zero);

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.EmergencyRetreat));
        Assert.That(plan.Skill, Is.Null);
    }

    [Test]
    public void Planner_DoesNotEmergencyRetreatFromEnemiesRememberedWithoutDirectSight()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        var memories = new List<CombatCharacterIntel>();
        for (int i = 0; i < 3; i++)
        {
            Character enemy = CreateCharacter(
                $"RememberedEnemy{i}",
                new Sword(),
                new Vector3(100f + i, 0f, 0f),
                team: CombatTeam.Enemy);
            memories.Add(CombatEditModeTestUtil.CreateIntel(
                enemy,
                true,
                new Vector3(i + 1f, 0f, 0f),
                hasDirectSight: false,
                hasMemory: true));
        }

        CombatAiContext context = Context(
            owner,
            enemies: memories,
            enemyStone: new Vector3(20f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
    }

    [Test]
    public void Planner_DoesNotEmergencyRetreatAboveFifteenPercentWithoutActiveThreat()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero, 30, 5);
        CombatAiContext context = Context(
            owner,
            ownStone: new Vector3(-10f, 0f, 0f),
            enemyStone: new Vector3(10f, 0f, 0f),
            forests: new[] { new Vector3(-4f, 0f, 0f) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
    }

    [Test]
    public void Planner_SelectsDefendOwnStoneForKnownStoneThreat()
    {
        Character owner = CreateCharacter("Owner", new Shield(), new Vector3(5f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DefendOwnStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.OwnStoneThreatHigh));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.InterceptThreat));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_GatekeeperReturnsToOwnStoneWhenNoThreatIsKnown()
    {
        Character owner = CreateCharacter("Gatekeeper", new Sword(), new Vector3(8f, 0f, 0f));
        CombatAiContext context = Context(
            owner,
            ownStone: Vector3.zero,
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DefendOwnStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.ReturnOwnStone));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void Planner_GatekeeperReturnsToOwnStoneWhenEnemyIsOutsideDefenseRadius()
    {
        Character owner = CreateCharacter("Gatekeeper", new Sword(), new Vector3(8f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(30f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DefendOwnStone));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.ReturnOwnStone));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(Vector3.zero));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.Null);
    }

    [Test]
    public void Planner_GatekeeperInterceptsAThreatNearOwnStone()
    {
        Character owner = CreateCharacter("Gatekeeper", new Shield(), new Vector3(1f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DefendOwnStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.InterceptThreat));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_GatekeeperUsesDefensiveStateSkillSelection()
    {
        Character owner = CreateCharacter("Gatekeeper", new Shield(), new Vector3(1f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        var attack = new CombatEditModeTestUtil.AiPlannerBasicAttackSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, attack);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(40f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DefendOwnStone));
        Assert.That(plan.Skill, Is.SameAs(attack));
        Assert.That(plan.SkillTarget, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_GatekeeperFallsBackToStandardStateWithoutOwnStonePosition()
    {
        Character owner = CreateCharacter("Gatekeeper", new Sword(), Vector3.zero);
        CombatAiContext context = Context(owner, enemyStone: new Vector3(20f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
    }

    [Test]
    public void DebugSnapshotMatchesGatekeeperPlan()
    {
        Character owner = CreateCharacter("Gatekeeper", new Sword(), new Vector3(8f, 0f, 0f));
        CombatAiContext context = Context(owner, ownStone: Vector3.zero);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper));

        CombatEditModeTestUtil.AssertPlanMatchesDebugSnapshot(context, profile);
    }

    [Test]
    public void Planner_TagalongCopiesTheAssignedAllyObjectiveAndTarget()
    {
        Character owner = CreateCharacter("Tagalong", new Sword(), Vector3.zero);
        Character nearerLeader = CreateCharacter("NearLeader", new Sword(), new Vector3(4f, 0f, 0f));
        Character fartherLeader = CreateCharacter("FarLeader", new Sword(), new Vector3(9f, 0f, 0f));
        Character focusedEnemy = CreateCharacter("Focused", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatCharacterIntel fartherIntel = CombatEditModeTestUtil.CreateIntel(
            fartherLeader,
            true,
            fartherLeader.transform.position,
            hasObjective: true,
            objective: CombatObjective.DestroyEnemyStone,
            hasIntendedDestination: true,
            intendedDestination: new Vector3(14f, 0f, 0f));
        CombatCharacterIntel nearerIntel = CombatEditModeTestUtil.CreateIntel(
            nearerLeader,
            true,
            nearerLeader.transform.position,
            hasObjective: true,
            objective: CombatObjective.AttackEnemy,
            intendedTarget: focusedEnemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(focusedEnemy) },
            allies: new[] { fartherIntel, nearerIntel },
            enemyStone: new Vector3(30f, 0f, 0f),
            tagalongTarget: fartherLeader);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.Null);
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(new Vector3(14f, 0f, 0f)));
    }

    [Test]
    public void Planner_TagalongUsesTheAssignedAllyTargetForSkillSelection()
    {
        Character owner = CreateCharacter("Tagalong", new Sword(), Vector3.zero);
        Character leader = CreateCharacter("Leader", new Sword(), new Vector3(4f, 0f, 0f));
        Character focusedEnemy = CreateCharacter("Focused", new Sword(), new Vector3(2f, 0f, 0f), team: CombatTeam.Enemy);
        Character temptingEnemy = CreateCharacter("Tempting", new Sword(), new Vector3(2f, 0f, 2f), 30, 1, CombatTeam.Enemy);
        var attack = new CombatEditModeTestUtil.AiPlannerBasicAttackSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, attack);
        CombatCharacterIntel leaderIntel = CombatEditModeTestUtil.CreateIntel(
            leader,
            true,
            leader.transform.position,
            hasObjective: true,
            objective: CombatObjective.AttackEnemy,
            intendedTarget: focusedEnemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(focusedEnemy), Intel(temptingEnemy) },
            allies: new[] { leaderIntel },
            tagalongTarget: leader);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(attack));
        Assert.That(plan.SkillTarget, Is.SameAs(focusedEnemy));
    }

    [Test]
    public void Planner_TagalongFallsBackToStandardPlanWithoutAnAllyObjective()
    {
        Character owner = CreateCharacter("Tagalong", new Sword(), Vector3.zero);
        Character ally = CreateCharacter("IdleAlly", new Sword(), new Vector3(4f, 0f, 0f));
        CombatAiContext context = Context(
            owner,
            allies: new[] { Intel(ally) },
            enemyStone: new Vector3(20f, 0f, 0f),
            tagalongTarget: ally);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
    }

    [Test]
    public void Planner_TagalongDoesNotRetargetWhenAssignedAllyIsUnavailable()
    {
        Character owner = CreateCharacter("Tagalong", new Sword(), Vector3.zero);
        Character assignedAlly = CreateCharacter("AssignedAlly", new Sword(), new Vector3(4f, 0f, 0f), 30, 0);
        Character otherAlly = CreateCharacter("OtherAlly", new Sword(), new Vector3(2f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatCharacterIntel assignedIntel = CombatEditModeTestUtil.CreateIntel(
            assignedAlly,
            true,
            assignedAlly.transform.position,
            hasObjective: true,
            objective: CombatObjective.AttackEnemy,
            intendedTarget: enemy);
        CombatCharacterIntel otherIntel = CombatEditModeTestUtil.CreateIntel(
            otherAlly,
            true,
            otherAlly.transform.position,
            hasObjective: true,
            objective: CombatObjective.AttackEnemy,
            intendedTarget: enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            allies: new[] { assignedIntel, otherIntel },
            enemyStone: new Vector3(20f, 0f, 0f),
            tagalongTarget: assignedAlly);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.TransitionReason, Is.Not.EqualTo(CombatAiReasonCode.PersonalityPreference));
    }

    [Test]
    public void Planner_AvengerChasesTheRecentAttackerAndKeepsItsTarget()
    {
        Character owner = CreateCharacter("Avenger", new Sword(), Vector3.zero);
        Character attacker = CreateCharacter("Attacker", new Sword(), new Vector3(10f, 0f, 0f), team: CombatTeam.Enemy);
        Character decoy = CreateCharacter("Decoy", new Sword(), new Vector3(2f, 0f, 0f), 30, 1, CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(attacker), Intel(decoy) },
            ownStone: new Vector3(-8f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f),
            recentAttacker: attacker);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(attacker));
    }

    [Test]
    public void Planner_AvengerUsesTheRecentAttackerForSkillSelection()
    {
        Character owner = CreateCharacter("Avenger", new Sword(), Vector3.zero);
        Character attacker = CreateCharacter("Attacker", new Sword(), new Vector3(2f, 0f, 0f), team: CombatTeam.Enemy);
        Character decoy = CreateCharacter("Decoy", new Sword(), new Vector3(2f, 0f, 2f), 30, 1, CombatTeam.Enemy);
        var attack = new CombatEditModeTestUtil.AiPlannerBasicAttackSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, attack);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(attacker), Intel(decoy) },
            recentAttacker: attacker);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(attack));
        Assert.That(plan.SkillTarget, Is.SameAs(attacker));
    }

    [Test]
    public void Planner_AvengerFallsBackToStandardPlanWithoutARecentAttacker()
    {
        Character owner = CreateCharacter("Avenger", new Sword(), Vector3.zero);
        CombatAiContext context = Context(
            owner,
            enemyStone: new Vector3(20f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
    }

    [Test]
    public void Planner_AvengerStopsChasingWhenTheRecentAttackerIsLost()
    {
        Character owner = CreateCharacter("Avenger", new Sword(), Vector3.zero);
        Character attacker = CreateCharacter("LostAttacker", new Sword(), new Vector3(10f, 0f, 0f), team: CombatTeam.Enemy);
        CombatCharacterIntel lostAttacker = CombatEditModeTestUtil.CreateIntel(
            attacker,
            hasKnownPosition: false,
            knownPosition: default,
            hasDirectSight: false);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { lostAttacker },
            enemyStone: new Vector3(20f, 0f, 0f),
            recentAttacker: attacker);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
    }

    [Test]
    public void Planner_AvengerFollowsTheAttackerAboveTheEmergencyThreshold()
    {
        Character owner = CreateCharacter("Avenger", new Shield(), Vector3.zero, 30, 5);
        Character attacker = CreateCharacter("Attacker", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(attacker) },
            ownStone: new Vector3(-8f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f),
            forests: new[] { new Vector3(-4f, 0f, 0f) },
            recentAttacker: attacker);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(attacker));
    }

    [Test]
    public void DebugSnapshotMatchesAvengerPlan()
    {
        Character owner = CreateCharacter("Avenger", new Sword(), Vector3.zero);
        Character attacker = CreateCharacter("Attacker", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(attacker) },
            recentAttacker: attacker);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger));

        CombatEditModeTestUtil.AssertPlanMatchesDebugSnapshot(context, profile);
    }

    [Test]
    public void Planner_UsesStandardSkillPriorityForWand()
    {
        Character owner = CreateCharacter("Owner", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase basic = CombatSkillFactory.Create(SkillId.Wand_Bolt, owner.EquippedWeapon);
        SkillBase godsHand = CombatSkillFactory.Create(SkillId.Wand_GodsHand, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, basic, godsHand);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyInRange));
        Assert.That(plan.ActionCode, Is.Not.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(basic));
        Assert.That(plan.SkillTarget, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_HighGroundObsessiveMovesToAHighGroundCandidateWithSignature()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(10f, 0f, 0f), team: CombatTeam.Enemy);
        Vector3 highGround = new Vector3(5f, 3f, 0f);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            highGround: new[] { highGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(highGround));
    }

    [Test]
    public void Planner_HighGroundObsessiveKeepsThePreviousHighGroundDestination()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), Vector3.zero);
        Vector3 nearerHighGround = new Vector3(5f, 3f, 0f);
        Vector3 previousHighGround = new Vector3(10f, 3f, 0f);
        CombatAiContext context = Context(
            owner,
            highGround: new[] { nearerHighGround, previousHighGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            profile,
            previousMoveTarget: CombatMoveTarget.ForPosition(previousHighGround));

        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(previousHighGround));
    }

    [Test]
    public void Planner_HighGroundObsessivePursuesEnemyAfterReachingHighGround()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), new Vector3(5f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(14f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner);
        Vector3 highGround = new Vector3(5f, 4f, 0f);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            highGround: new[] { highGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PursueEnemy));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_HighGroundObsessiveAdvancesOnStoneAfterReachingHighGround()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), new Vector3(5f, 0f, 0f));
        Vector3 highGround = new Vector3(5f, 4f, 0f);
        CombatAiContext context = Context(
            owner,
            enemyStone: new Vector3(30f, 0f, 0f),
            highGround: new[] { highGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_HighGroundObsessiveDoesNotClimbAgainAfterLeavingHighGroundToFight()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), new Vector3(10f, 0f, 0f));
        Vector3 highGround = new Vector3(5f, 4f, 0f);
        Vector3 enemyStone = new Vector3(30f, 0f, 0f);
        CombatAiContext context = Context(
            owner,
            enemyStone: enemyStone,
            highGround: new[] { highGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            profile,
            previousObjective: CombatObjective.AttackEnemy,
            hasReachedHighGround: true);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.ActionCode, Is.Not.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.Destination, Is.Not.EqualTo(highGround));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_HighGroundObsessiveKeepsWeaponSkillWhileHoldingPosition()
    {
        Character owner = CreateCharacter("HighGround", new Bible(), new Vector3(5f, 0f, 0f));
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(6f, 0f, 0f), 30, 5);
        var heal = new CombatEditModeTestUtil.AiPlannerHealSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, heal);
        Vector3 highGround = new Vector3(5f, 2f, 0f);
        CombatAiContext context = Context(
            owner,
            allies: new[] { Intel(ally) },
            highGround: new[] { highGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.HasDestination, Is.False);
        Assert.That(plan.Skill, Is.SameAs(heal));
        Assert.That(plan.SkillTarget, Is.SameAs(ally));
    }

    [Test]
    public void Planner_HighGroundObsessiveFallsBackWithoutAHighGroundCandidate()
    {
        Character owner = CreateCharacter("HighGround", new Sword(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PursueEnemy));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_HighGroundObsessiveKeepsSignatureWhenCandidateMoveIsBlocked()
    {
        Character owner = CreateCharacter("HighGround", new Sword(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        Vector3 highGround = new Vector3(5f, 3f, 0f);
        CombatAiContext context = CombatEditModeTestUtil.CreatePlannerContext(
            owner,
            new[] { Intel(enemy) },
            highGroundCandidates: new[] { highGround },
            hasBlockedMoveDestination: true,
            blockedMoveDestination: highGround);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.HasDestination, Is.False);
    }

    [Test]
    public void DebugSnapshotMatchesHighGroundObsessivePlan()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), Vector3.zero);
        Vector3 highGround = new Vector3(5f, 3f, 0f);
        CombatAiContext context = Context(owner, highGround: new[] { highGround });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatEditModeTestUtil.AssertPlanMatchesDebugSnapshot(context, profile);
    }

    [Test]
    public void Planner_TagalongCanFollowAnUnsafeLeaderInsteadOfRetreating()
    {
        Character owner = CreateCharacter("Tagalong", new Sword(), Vector3.zero, 30, 5);
        Character leader = CreateCharacter("Leader", new Sword(), new Vector3(4f, 0f, 0f));
        Character focusedEnemy = CreateCharacter("Focused", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatCharacterIntel leaderIntel = CombatEditModeTestUtil.CreateIntel(
            leader,
            true,
            leader.transform.position,
            hasObjective: true,
            objective: CombatObjective.AttackEnemy,
            intendedTarget: focusedEnemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(focusedEnemy) },
            allies: new[] { leaderIntel },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(30f, 0f, 0f),
            forests: new[] { new Vector3(-4f, 0f, 0f) },
            tagalongTarget: leader);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(focusedEnemy));
    }

    [Test]
    public void DebugSnapshotMatchesTagalongPlan()
    {
        Character owner = CreateCharacter("Tagalong", new Sword(), Vector3.zero);
        Character leader = CreateCharacter("Leader", new Sword(), new Vector3(4f, 0f, 0f));
        Vector3 destination = new Vector3(8f, 0f, 0f);
        CombatCharacterIntel leaderIntel = CombatEditModeTestUtil.CreateIntel(
            leader,
            true,
            leader.transform.position,
            hasObjective: true,
            objective: CombatObjective.Search,
            hasIntendedDestination: true,
            intendedDestination: destination);
        CombatAiContext context = Context(
            owner,
            allies: new[] { leaderIntel },
            tagalongTarget: leader);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatEditModeTestUtil.AssertPlanMatchesDebugSnapshot(context, profile);
    }

    [Test]
    public void Planner_RecklessCentersAreaDamageOnEnemyStone()
    {
        GameObject systemGo = Track(new GameObject("MagicStoneSystem"));
        CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
        var map = new MapData(
            new HeightMap(4, 4, 1f),
            new GroundStateGrid(4, 4, 1f),
            seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(-20f, 0f, 0f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(10f, 0f, 0f)));
        system.Initialize(map);

        GameObject stoneGo = Track(new GameObject("EnemyMainStone"));
        MagicStone stone = stoneGo.AddComponent<MagicStone>();
        stone.Setup(featureIndex: 1, FeatureType.EnemyMainStone, stoneHeight: 3f);
        stoneGo.transform.position = new Vector3(10f, 0f, 0f);

        Character owner = CreateCharacter("Reckless", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(9f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase areaSkill = CombatSkillFactory.Create(SkillId.Wand_AreaBlast, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, areaSkill);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: stoneGo.transform.position);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Reckless));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.Skill, Is.SameAs(areaSkill));
        Assert.That(plan.SkillContext.TargetPoint, Is.EqualTo(stoneGo.transform.position));
        Assert.That(plan.SkillContext.ResolvedStones, Does.Contain(stone));
    }

    [Test]
    public void Planner_SelectsSupportAllyForRosaryAndFragileAlly()
    {
        Character owner = CreateCharacter("Owner", new Rosary(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(4f, 0f, 0f), 30, 5);
        CombatAiContext context = Context(owner, allies: new[] { Intel(ally) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.SupportAlly));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_AssaultWeaponAttacksInsteadOfSupportingWithoutSupportSkill()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(2f, 0f, 0f), 30, 5);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(
            owner,
            new CombatEditModeTestUtil.AiPlannerBasicAttackSkill());
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            allies: new[] { Intel(ally) },
            enemyStone: new Vector3(20f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_SupportThreatUsesMostFragileAllyInsteadOfTeamTotal()
    {
        Character owner = CreateCharacter("Owner", new Rosary(), Vector3.zero);
        Character firstAlly = CreateCharacter("FirstAlly", new Sword(), new Vector3(1f, 0f, 0f));
        Character secondAlly = CreateCharacter("SecondAlly", new Sword(), new Vector3(2f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(7f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            allies: new[] { Intel(firstAlly), Intel(secondAlly) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
    }

    [Test]
    public void Planner_SupportMoveDoesNotFollowAnAllyAlreadySupporting()
    {
        Character owner = CreateCharacter("Owner", WeaponBase.Unarmed, Vector3.zero);
        Character supportingAlly = CreateCharacter("SupportingAlly", new Rosary(), new Vector3(2f, 0f, 0f), 30, 1);
        Character advancingAlly = CreateCharacter("AdvancingAlly", new Sword(), new Vector3(4f, 0f, 0f), 30, 5);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(6f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(
            owner,
            new CombatEditModeTestUtil.AiPlannerHealSkill());
        CombatCharacterIntel supportingIntel = CombatEditModeTestUtil.CreateIntel(
            supportingAlly,
            true,
            supportingAlly.transform.position,
            hasObjective: true,
            objective: CombatObjective.SupportAlly,
            intendedTarget: advancingAlly);
        CombatCharacterIntel advancingIntel = CombatEditModeTestUtil.CreateIntel(
            advancingAlly,
            true,
            advancingAlly.transform.position,
            hasObjective: true,
            objective: CombatObjective.DestroyEnemyStone,
            hasIntendedDestination: true,
            intendedDestination: new Vector3(20f, 0f, 0f));
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            allies: new[] { supportingIntel, advancingIntel },
            enemyStone: new Vector3(20f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(advancingAlly));
    }

    [Test]
    public void Planner_SupportingAllyDoesNotTriggerAnotherSupportPlan()
    {
        Character owner = CreateCharacter("Owner", new Rosary(), Vector3.zero);
        Character supportingAlly = CreateCharacter("SupportingAlly", new Rosary(), new Vector3(2f, 0f, 0f), 30, 1);
        Character healthyAlly = CreateCharacter("HealthyAlly", new Sword(), new Vector3(3f, 0f, 0f));
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(6f, 0f, 0f), team: CombatTeam.Enemy);
        CombatCharacterIntel supportingIntel = CombatEditModeTestUtil.CreateIntel(
            supportingAlly,
            true,
            supportingAlly.transform.position,
            hasObjective: true,
            objective: CombatObjective.SupportAlly,
            intendedTarget: healthyAlly);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            allies: new[] { supportingIntel, Intel(healthyAlly) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
    }

    [Test]
    public void Planner_SelectsAttackEnemyOnlyInsideSwordEngagementRange()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            enemyStone: new Vector3(20f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_SelectsDestroyEnemyStoneWhenNoHigherPriorityStateApplies()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        CombatAiContext context = Context(owner, enemyStone: new Vector3(20f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
    }

    [Test]
    public void Planner_SelectsSearchWhenEnemyAndStoneLocationsAreUnknown()
    {
        Character owner = CreateCharacter("Owner", new Grimoire(), Vector3.zero);
        CombatAiContext context = Context(owner, highGround: new[] { new Vector3(5f, 2f, 0f) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Search));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.TakeHighGround));
    }

    [TestCase(WeaponKind.Sword, CombatObjective.DestroyEnemyStone, CombatAiMoveCode.AdvanceEnemyStone)]
    [TestCase(WeaponKind.Wand, CombatObjective.DestroyEnemyStone, CombatAiMoveCode.AdvanceEnemyStone)]
    [TestCase(WeaponKind.Grimoire, CombatObjective.AttackEnemy, CombatAiMoveCode.PursueEnemy)]
    public void Planner_AssaultAndControlWeaponsProduceTheirRolePlan(
        WeaponKind weaponKind,
        CombatObjective expectedState,
        string expectedAction)
    {
        Character owner = CreateCharacter("Owner", CreateWeapon(weaponKind), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(15f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            enemyStone: new Vector3(30f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(expectedState));
        Assert.That(plan.ActionCode, Is.EqualTo(expectedAction));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [TestCase(WeaponKind.Shield, CombatAiMoveCode.SupportAlly, true)]
    [TestCase(WeaponKind.Bible, CombatAiMoveCode.SupportAlly, true)]
    [TestCase(WeaponKind.Rosary, CombatAiMoveCode.HoldPosition, false)]
    public void Planner_SupportWeaponsProduceSupportPlan(
        WeaponKind weaponKind,
        string expectedAction,
        bool expectedDestination)
    {
        Character owner = CreateCharacter("Owner", CreateWeapon(weaponKind), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(5f, 0f, 0f), 30, 5);
        CombatCharacterIntel allyIntel = CombatEditModeTestUtil.CreateIntel(
            ally,
            true,
            ally.transform.position,
            hasObjective: true,
            objective: CombatObjective.DestroyEnemyStone);
        CombatAiContext context = Context(owner, allies: new[] { allyIntel });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.ActionCode, Is.EqualTo(expectedAction));
        Assert.That(plan.MoveTarget.HasDestination, Is.EqualTo(expectedDestination));
    }

    [Test]
    public void Planner_ShieldDoesNotSupportAnUnthreatenedFullHealthAlly()
    {
        Character owner = CreateCharacter("Owner", new Shield(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(5f, 0f, 0f));
        CombatCharacterIntel allyIntel = CombatEditModeTestUtil.CreateIntel(
            ally,
            true,
            ally.transform.position,
            hasObjective: true,
            objective: CombatObjective.DestroyEnemyStone);
        CombatAiContext context = Context(
            owner,
            allies: new[] { allyIntel },
            enemyStone: new Vector3(30f, 0f, 0f));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
    }

    [Test]
    public void Planner_AttacksTheMarkedStoneAttackerOutsideNormalEngagementRange()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character marked = CreateCharacter("Marked", new Sword(), new Vector3(15f, 0f, 0f), team: CombatTeam.Enemy);
        Character weaker = CreateCharacter("Weaker", new Sword(), new Vector3(2f, 0f, 0f), 30, 5, CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(marked), Intel(weaker) },
            enemyStone: new Vector3(30f, 0f, 0f),
            markedStoneAttacker: marked);

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.OwnStoneAttackerMarked));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(marked));
    }

    [TestCase(CombatAiPersonalityKind.Neutral, CombatObjective.DestroyEnemyStone, CombatAiMoveCode.AdvanceEnemyStone)]
    [TestCase(CombatAiPersonalityKind.BattleJunkie, CombatObjective.AttackEnemy, CombatAiMoveCode.PursueEnemy)]
    [TestCase(CombatAiPersonalityKind.Cunning, CombatObjective.DestroyEnemyStone, CombatAiMoveCode.PersonalitySignature)]
    [TestCase(CombatAiPersonalityKind.Devoted, CombatObjective.SupportAlly, CombatAiMoveCode.PersonalitySignature)]
    [TestCase(CombatAiPersonalityKind.Lonely, CombatObjective.Search, CombatAiMoveCode.PersonalitySignature)]
    [TestCase(CombatAiPersonalityKind.Reckless, CombatObjective.DestroyEnemyStone, CombatAiMoveCode.AdvanceEnemyStone)]
    public void Planner_BuiltInPersonalityHasVisibleStateSignature(
        CombatAiPersonalityKind kind,
        CombatObjective expectedState,
        string expectedAction)
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero, 30, kind == CombatAiPersonalityKind.Reckless ? 5 : 30);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(15f, 0f, 0f), team: CombatTeam.Enemy);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(8f, 0f, 0f), 30, 5);
        IReadOnlyList<CombatCharacterIntel> allies = kind == CombatAiPersonalityKind.Devoted ||
            kind == CombatAiPersonalityKind.Lonely
            ? new[] { Intel(ally) }
            : Array.Empty<CombatCharacterIntel>();
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            allies: allies,
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(CombatAiPersonalityProfile.CreateBuiltInProfile(kind));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(expectedState));
        Assert.That(plan.ActionCode, Is.EqualTo(expectedAction));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_WandTakesHighGroundBeforePursuingAnEnemyOutsideReadySkillRange()
    {
        Character owner = CreateCharacter("Owner", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(
            owner,
            new CombatEditModeTestUtil.AiPlannerBoltCooldownSkill());
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            highGround: new[] { new Vector3(4f, 3f, 1f) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.TakeHighGround));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(new Vector3(4f, 3f, 1f)));
    }

    [Test]
    public void Planner_BibleTakesHighGroundInsideSupportRange()
    {
        Character owner = CreateCharacter("Owner", new Bible(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(6f, 0f, 0f), 30, 5);
        CombatEditModeTestUtil.SetAvailableCombatSkills(
            owner,
            new CombatEditModeTestUtil.AiPlannerHealSkill());
        Vector3 highGround = new Vector3(3f, 3f, 1f);
        CombatAiContext context = Context(
            owner,
            allies: new[] { Intel(ally) },
            highGround: new[] { highGround });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.TakeHighGround));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(highGround));
    }

    [TestCase(WeaponKind.Grimoire)]
    [TestCase(WeaponKind.Bible)]
    [TestCase(WeaponKind.Rosary)]
    public void Planner_ControlAndSupportWeaponsSearchBeforeAttackingAKnownStoneWhenEnemiesAreUnknown(
        WeaponKind weaponKind)
    {
        Character owner = CreateCharacter("Owner", CreateWeapon(weaponKind), Vector3.zero);
        Vector3 highGround = new Vector3(5f, 2f, 0f);
        CombatAiContext context = Context(
            owner,
            enemyStone: new Vector3(20f, 0f, 0f),
            highGround: new[] { highGround });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Search));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.TakeHighGround));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(highGround));
    }

    [Test]
    public void Planner_BattleJunkieKeepsItsFocusedEnemyWhileTheFocusIsValid()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character focused = CreateCharacter("Focused", new Sword(), new Vector3(8f, 0f, 0f), team: CombatTeam.Enemy);
        Character weaker = CreateCharacter("Weaker", new Sword(), new Vector3(2f, 0f, 0f), 30, 5, CombatTeam.Enemy);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(focused), Intel(weaker) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BattleJunkie));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            profile,
            focusEnemy: focused,
            focusCommitmentRemainingSeconds: 1f);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(focused));
    }

    [Test]
    public void Planner_BattleJunkiePursuesRememberedFocusedEnemyPosition()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character focused = CreateCharacter("Focused", new Sword(), new Vector3(100f, 0f, 0f), team: CombatTeam.Enemy);
        Vector3 rememberedPosition = new Vector3(6f, 0f, 2f);
        CombatCharacterIntel memory = CombatEditModeTestUtil.CreateIntel(
            focused,
            true,
            rememberedPosition,
            hasDirectSight: false,
            hasMemory: true);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { memory },
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BattleJunkie));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            profile,
            focusEnemy: focused,
            focusCommitmentRemainingSeconds: 1f);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PursueEnemy));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(rememberedPosition));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.Null);
    }

    [Test]
    public void Planner_AttentionSeekerMovesTowardCrowdWhileSearching()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(6f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.AttentionSeeker));
        CombatAiContext context = Context(owner, allies: new[] { Intel(ally) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Search));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
    }

    [Test]
    public void Planner_SoftSplitsToTheLessCongestedAuthoredRoute()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(1f, 0f, 0f));
        var routeA = new CombatAiAssaultRoute("A", "A", new[] { Vector3.zero, new Vector3(5f, 0f, 0f), new Vector3(10f, 0f, 0f) });
        var routeB = new CombatAiAssaultRoute("B", "B", new[] { Vector3.zero, new Vector3(5f, 0f, 5f), new Vector3(10f, 0f, 0f) });
        CombatCharacterIntel allyIntel = CombatEditModeTestUtil.CreateIntel(
            ally,
            true,
            ally.transform.position,
            hasObjective: true,
            objective: CombatObjective.DestroyEnemyStone,
            hasIntendedDestination: true,
            intendedDestination: new Vector3(5f, 0f, 0f));
        CombatAiContext context = Context(
            owner,
            allies: new[] { allyIntel },
            enemyStone: new Vector3(10f, 0f, 0f),
            routes: new[] { routeA, routeB });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.MoveTarget.AssaultRouteKey, Is.EqualTo("B"));
    }

    [Test]
    public void Planner_RecklessAdvancesDirectlyToEnemyStoneInsteadOfUsingAssaultRoute()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        var route = new CombatAiAssaultRoute(
            "Bridge",
            "Bridge",
            new[] { Vector3.zero, new Vector3(5f, 0f, 5f), new Vector3(10f, 0f, 0f) });
        CombatAiContext context = Context(
            owner,
            enemyStone: new Vector3(10f, 0f, 0f),
            routes: new[] { route });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Reckless));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
        Assert.That(plan.MoveTarget.HasAssaultRouteKey, Is.False);
    }

    [Test]
    public void Planner_CunningChoosesTheLowerRiskAuthoredRoute()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(5f, 0f, 0f), team: CombatTeam.Enemy);
        var exposed = new CombatAiAssaultRoute(
            "Exposed",
            "Exposed",
            new[] { Vector3.zero, new Vector3(5f, 0f, 0f), new Vector3(10f, 0f, 0f) });
        var covered = new CombatAiAssaultRoute(
            "Covered",
            "Covered",
            new[] { Vector3.zero, new Vector3(0f, 0f, 6f), new Vector3(10f, 0f, 0f) });
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            enemyStone: new Vector3(10f, 0f, 0f),
            routes: new[] { exposed, covered });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Cunning));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceAssaultRoute));
        Assert.That(plan.MoveTarget.AssaultRouteKey, Is.EqualTo("Covered"));
    }

    [Test]
    public void Planner_SearchesTheRememberedPositionInsteadOfTheEnemiesCurrentPosition()
    {
        Character owner = CreateCharacter("Owner", new Grimoire(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(100f, 0f, 0f), team: CombatTeam.Enemy);
        Vector3 rememberedPosition = new Vector3(6f, 0f, 2f);
        CombatCharacterIntel memory = CombatEditModeTestUtil.CreateIntel(
            enemy,
            true,
            rememberedPosition,
            hasDirectSight: false,
            hasMemory: true);
        CombatAiContext context = Context(owner, enemies: new[] { memory });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Search));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.SearchLastKnown));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(rememberedPosition));
        Assert.That(plan.MoveTarget.Destination, Is.Not.EqualTo(enemy.transform.position));
    }

    [Test]
    public void Planner_AttackPlanSelectsUsableDamageSkillAndTarget()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(2f, 0f, 0f), team: CombatTeam.Enemy);
        var attack = new CombatEditModeTestUtil.AiPlannerBasicAttackSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, attack);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.Skill, Is.SameAs(attack));
        Assert.That(plan.SkillTarget, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_DoesNotDuplicateHealingAlreadyReservedByAlly()
    {
        Character owner = CreateCharacter("Owner", new Rosary(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(4f, 0f, 0f), 30, 10);
        var heal = new CombatEditModeTestUtil.AiPlannerHealSkill();
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, heal);
        CombatAiPersonalityProfile devoted = Track(CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Devoted));
        CombatAiContext context = Context(
            owner,
            allies: new[] { Intel(ally) },
            pendingHealing: new[] { new CombatAiPendingHealing(ally, 20) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, devoted);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.Skill, Is.Null);
    }

    [Test]
    public void Planner_DoesNotAttackAnEnemyAlreadyCoveredByPendingLethalDamage()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Shield(), new Vector3(1f, 0f, 0f));
        Character covered = CreateCharacter("Covered", WeaponBase.Unarmed, new Vector3(3f, 0f, 0f), 30, 5, CombatTeam.Enemy);
        Character available = CreateCharacter("Available", WeaponBase.Unarmed, new Vector3(3.5f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(covered), Intel(available) },
            allies: new[] { Intel(ally) },
            pendingDamage: new[] { new CombatAiPendingDamage(covered, 5) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(available));
    }

    [Test]
    public void DebugSnapshotContainsSelectedStateReasonAndAction()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero);
        CombatAiContext context = Context(owner, enemyStone: new Vector3(10f, 0f, 0f));

        CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(
            context,
            null,
            previousObjective: CombatObjective.Search);

        Assert.That(snapshot.PreviousState, Is.EqualTo(CombatObjective.Search));
        Assert.That(snapshot.SelectedState, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(snapshot.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
        Assert.That(snapshot.ActionCode, Is.Not.Empty);
    }

    private CombatAiContext Context(
        Character owner,
        IReadOnlyList<CombatCharacterIntel> enemies = null,
        IReadOnlyList<CombatCharacterIntel> allies = null,
        Vector3? ownStone = null,
        Vector3? enemyStone = null,
        IReadOnlyList<Vector3> highGround = null,
        IReadOnlyList<Vector3> forests = null,
        IReadOnlyList<CombatAiAssaultRoute> routes = null,
        IReadOnlyList<CombatAiPendingHealing> pendingHealing = null,
        IReadOnlyList<CombatAiPendingDamage> pendingDamage = null,
        Character recentAttacker = null,
        Character markedStoneAttacker = null,
        Character tagalongTarget = null)
    {
        return CombatEditModeTestUtil.CreatePlannerContext(
            owner,
            enemies,
            allies,
            hasOwnStonePosition: ownStone.HasValue,
            ownStonePosition: ownStone ?? default,
            hasEnemyStonePosition: enemyStone.HasValue,
            enemyStonePosition: enemyStone ?? default,
            highGroundCandidates: highGround,
            allyPendingDamage: pendingDamage,
            allyPendingHealing: pendingHealing,
            assaultRoutes: routes,
            forestCandidates: forests,
            recentAttacker: recentAttacker,
            markedStoneAttacker: markedStoneAttacker,
            tagalongTarget: tagalongTarget);
    }

    private Character CreateCharacter(
        string name,
        WeaponBase weapon,
        Vector3 position,
        int maxHp = 30,
        int hp = 30,
        CombatTeam team = CombatTeam.Ally)
    {
        var go = Track(new GameObject(name));
        Character character = go.AddComponent<Character>();
        character.Health.Initialize(maxHp, hp);
        character.EquipWeapon(weapon);
        character.SetTeam(team);
        character.transform.position = position;
        return character;
    }

    private static CombatCharacterIntel Intel(Character character) =>
        CombatEditModeTestUtil.CreateIntel(character, true, character.transform.position);

    private static WeaponBase CreateWeapon(WeaponKind kind)
    {
        return kind switch
        {
            WeaponKind.Sword => new Sword(),
            WeaponKind.Shield => new Shield(),
            WeaponKind.Wand => new Wand(),
            WeaponKind.Grimoire => new Grimoire(),
            WeaponKind.Bible => new Bible(),
            WeaponKind.Rosary => new Rosary(),
            _ => WeaponBase.Unarmed,
        };
    }

    private static SkillBase CreateMagicStoneDamageSkill(WeaponKind kind)
    {
        SkillId skillId = kind switch
        {
            WeaponKind.Sword => SkillId.Sword_Slash,
            WeaponKind.Shield => SkillId.Shield_Slash,
            WeaponKind.Wand => SkillId.Wand_Bolt,
            WeaponKind.Grimoire => SkillId.Grimoire_Bolt,
            WeaponKind.Bible => SkillId.Bible_Smite,
            WeaponKind.Rosary => SkillId.Rosary_Strike,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return CombatSkillFactory.Create(skillId, CreateWeapon(kind));
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
