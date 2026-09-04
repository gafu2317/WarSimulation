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
    public void Planner_SelectsRetreatBeforeOtherStatesWhenSelfThreatIsHigh()
    {
        Character owner = CreateCharacter("Owner", new Sword(), Vector3.zero, 30, 5);
        Character enemy = CreateCharacter("Enemy", new Sword(), Vector3.one, 30, 30, CombatTeam.Enemy);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-10f, 0f, 0f),
            enemyStone: new Vector3(10f, 0f, 0f),
            forests: new[] { new Vector3(-4f, 0f, 0f) });

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, null);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Retreat));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.SelfThreatHigh));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.MoveForest));
    }

    [Test]
    public void Planner_DoesNotRetreatFromEnemiesRememberedWithoutDirectSight()
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
    public void Planner_DoesNotRetreatWhenLowHpHasNoActiveThreat()
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
    public void Planner_TagalongCopiesTheNearestAllyObjectiveAndTarget()
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
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(focusedEnemy));
    }

    [Test]
    public void Planner_TagalongUsesTheNearestAllyTargetForSkillSelection()
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
            allies: new[] { leaderIntel });
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
            enemyStone: new Vector3(20f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyStoneKnown));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
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
    public void Planner_AvengerFollowsTheAttackerInsteadOfRetreatingWhenCriticallyHurt()
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
    public void Planner_BigMagicChoosesTheHighestImpactSkillInsteadOfBasicAttack()
    {
        Character owner = CreateCharacter("BigMagic", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase basic = CombatSkillFactory.Create(SkillId.Wand_Bolt, owner.EquippedWeapon);
        SkillBase arcaneBlast = CombatSkillFactory.Create(SkillId.Wand_ArcaneBlast, owner.EquippedWeapon);
        SkillBase godsHand = CombatSkillFactory.Create(SkillId.Wand_GodsHand, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, basic, arcaneBlast, godsHand);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(godsHand));
        Assert.That(plan.SkillTarget, Is.SameAs(enemy));
        Assert.That(plan.Skill.CastTimeSeconds, Is.EqualTo(2.5f));
    }

    [Test]
    public void Planner_BigMagicWaitsForHighImpactSkillWhenItIsOutOfRange()
    {
        Character owner = CreateCharacter("BigMagic", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(30f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase basic = CombatSkillFactory.Create(SkillId.Wand_Bolt, owner.EquippedWeapon);
        SkillBase godsHand = CombatSkillFactory.Create(SkillId.Wand_GodsHand, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, basic, godsHand);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.Null);
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_BigMagicFallsBackToStandardPlanWithoutAHighImpactSkill()
    {
        Character owner = CreateCharacter("BigMagic", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase basic = CombatSkillFactory.Create(SkillId.Wand_Bolt, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, basic);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyInRange));
        Assert.That(plan.ActionCode, Is.Not.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(basic));
    }

    [Test]
    public void Planner_BigMagicRetainsItsAttackPriorityWhenCriticallyHurt()
    {
        Character owner = CreateCharacter("BigMagic", new Wand(), Vector3.zero, 30, 5);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase godsHand = CombatSkillFactory.Create(SkillId.Wand_GodsHand, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, godsHand);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-8f, 0f, 0f),
            forests: new[] { new Vector3(-4f, 0f, 0f) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(godsHand));
    }

    [Test]
    public void Planner_BigMagicUsesAHighImpactHealInsteadOfRosaryBasicAttack()
    {
        Character owner = CreateCharacter("BigMagic", new Rosary(), Vector3.zero);
        Character ally = CreateCharacter("Ally", new Sword(), new Vector3(2f, 0f, 0f), 30, 5);
        SkillBase basic = CombatSkillFactory.Create(SkillId.Rosary_Strike, owner.EquippedWeapon);
        SkillBase distantHeal = CombatSkillFactory.Create(SkillId.Rosary_DistantHeal, owner.EquippedWeapon);
        SkillBase closeHeal = CombatSkillFactory.Create(SkillId.Rosary_CloseHeal, owner.EquippedWeapon);
        SkillBase regeneration = CombatSkillFactory.Create(SkillId.Rosary_Regeneration, owner.EquippedWeapon);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, basic, distantHeal, closeHeal, regeneration);
        CombatAiContext context = Context(owner, allies: new[] { Intel(ally) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.Skill, Is.SameAs(closeHeal));
        Assert.That(plan.SkillTarget, Is.SameAs(ally));
    }

    [Test]
    public void DebugSnapshotMatchesBigMagicPlan()
    {
        Character owner = CreateCharacter("BigMagic", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(
            owner,
            CombatSkillFactory.Create(SkillId.Wand_Bolt, owner.EquippedWeapon),
            CombatSkillFactory.Create(SkillId.Wand_GodsHand, owner.EquippedWeapon));
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic));

        CombatEditModeTestUtil.AssertPlanMatchesDebugSnapshot(context, profile);
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
    public void Planner_HighGroundObsessiveRetreatsAfterReachingHighGroundWhenThreatened()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), new Vector3(5f, 0f, 0f), 30, 5);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(6f, 0f, 0f), team: CombatTeam.Enemy);
        Vector3 highGround = new Vector3(5f, 4f, 0f);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            highGround: new[] { highGround },
            forests: new[] { new Vector3(2f, 0f, 0f) },
            ownStone: Vector3.zero,
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Retreat));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.MoveForest));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(new Vector3(2f, 0f, 0f)));
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
    public void Planner_HighGroundObsessiveRetreatsWithoutClimbingAgainAfterLeavingHighGround()
    {
        Character owner = CreateCharacter("HighGround", new Grimoire(), new Vector3(10f, 0f, 0f), 30, 5);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(11f, 0f, 0f), team: CombatTeam.Enemy);
        Vector3 highGround = new Vector3(5f, 4f, 0f);
        Vector3 forest = new Vector3(7f, 0f, 0f);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            highGround: new[] { highGround },
            forests: new[] { forest },
            ownStone: Vector3.zero);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            profile,
            previousObjective: CombatObjective.AttackEnemy,
            hasReachedHighGround: true);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Retreat));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.MoveForest));
        Assert.That(plan.MoveTarget.Destination, Is.EqualTo(forest));
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
            forests: new[] { new Vector3(-4f, 0f, 0f) });
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
        CombatAiContext context = Context(owner, allies: new[] { leaderIntel });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong));

        CombatEditModeTestUtil.AssertPlanMatchesDebugSnapshot(context, profile);
    }

    [Test]
    public void Planner_StandoffSiegeAdvancesToTheEnemyStoneWithoutChasingAThreat()
    {
        Character owner = CreateCharacter("StandoffSiege", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(WeaponKind.Wand));
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
        Assert.That(plan.MoveTarget.TargetCharacter, Is.Null);
        Assert.That(plan.MoveTarget.Destination.x, Is.GreaterThan(owner.transform.position.x));
        Assert.That(Vector3.Distance(plan.MoveTarget.Destination, enemy.transform.position), Is.GreaterThanOrEqualTo(7f));
    }

    [TestCase(WeaponKind.Sword)]
    [TestCase(WeaponKind.Shield)]
    [TestCase(WeaponKind.Wand)]
    [TestCase(WeaponKind.Grimoire)]
    [TestCase(WeaponKind.Bible)]
    [TestCase(WeaponKind.Rosary)]
    public void Planner_StandoffSiegeUsesTheSameBehaviorForEveryWeapon(WeaponKind weaponKind)
    {
        Character owner = CreateCharacter("StandoffSiege", CreateWeapon(weaponKind), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(weaponKind));
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
        Assert.That(plan.MoveTarget.TargetCharacter, Is.Null);
        Assert.That(plan.MoveTarget.Destination.x, Is.GreaterThan(owner.transform.position.x));
        Assert.That(Vector3.Distance(plan.MoveTarget.Destination, enemy.transform.position), Is.GreaterThanOrEqualTo(7f));
    }

    [Test]
    public void Planner_StandoffSiegeRetreatsOnlyWhenEveryStoneApproachIsUnsafe()
    {
        Wand wand = new Wand();
        Character owner = CreateCharacter("StandoffSiege", wand, Vector3.zero);
        Character closeEnemy = CreateCharacter("CloseEnemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(WeaponKind.Wand));
        var enemies = new List<CombatCharacterIntel> { Intel(closeEnemy) };
        float ringDistance = wand.Range - 1f;
        Vector3 awayFromStone = Vector3.left;
        for (int i = 0; i < 8; i++)
        {
            Vector3 ringPosition = new Vector3(30f, 0f, 0f) +
                Quaternion.Euler(0f, i * 45f, 0f) * awayFromStone * ringDistance;
            Character ringEnemy = CreateCharacter($"RingEnemy{i}", new Sword(), ringPosition, team: CombatTeam.Enemy);
            enemies.Add(Intel(ringEnemy));
        }

        CombatAiContext context = Context(
            owner,
            enemies: enemies,
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
        Assert.That(plan.MoveTarget.Destination.x, Is.LessThan(owner.transform.position.x));
    }

    [Test]
    public void Planner_StandoffSiegeReturnsToStoneProgressAfterThreatClearance()
    {
        Character owner = CreateCharacter("StandoffSiege", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(WeaponKind.Wand));
        Wand wand = (Wand)owner.EquippedWeapon;
        var blockingEnemies = new List<CombatCharacterIntel> { Intel(enemy) };
        float ringDistance = wand.Range - 1f;
        Vector3 awayFromStone = Vector3.left;
        for (int i = 0; i < 8; i++)
        {
            Vector3 ringPosition = new Vector3(30f, 0f, 0f) +
                Quaternion.Euler(0f, i * 45f, 0f) * awayFromStone * ringDistance;
            Character ringEnemy = CreateCharacter($"RecoveryRingEnemy{i}", new Sword(), ringPosition, team: CombatTeam.Enemy);
            blockingEnemies.Add(Intel(ringEnemy));
        }

        CombatAiContext context = Context(
            owner,
            enemies: blockingEnemies,
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan firstPlan = CombatAiPlanner.BuildPlan(context, profile);
        Vector3 firstDestination = firstPlan.MoveTarget.Destination;
        owner.transform.position = firstDestination;
        CombatAiContext recoveredContext = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPlan recoveredPlan = CombatAiPlanner.BuildPlan(
            recoveredContext,
            profile,
            previousObjective: firstPlan.Objective,
            previousMoveTarget: firstPlan.MoveTarget);

        Assert.That(firstDestination.x, Is.LessThan(0f));
        Assert.That(recoveredPlan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(recoveredPlan.MoveTarget.HasDestination, Is.True);
        Assert.That(recoveredPlan.MoveTarget.Destination.x, Is.GreaterThan(owner.transform.position.x));
    }

    [Test]
    public void Planner_StandoffSiegeDoesNotKeepAnOldAssaultRouteTarget()
    {
        Character owner = CreateCharacter("StandoffSiege", new Wand(), Vector3.zero);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(WeaponKind.Wand));
        var route = new CombatAiAssaultRoute(
            "Direct",
            "Direct",
            new[] { Vector3.zero, new Vector3(5f, 0f, 0f), new Vector3(30f, 0f, 0f) });
        Vector3 previousDestination = new Vector3(5f, 0f, 0f);
        CombatAiContext context = Context(
            owner,
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f),
            routes: new[] { route });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(
            context,
            profile,
            previousObjective: CombatObjective.DestroyEnemyStone,
            previousMoveTarget: CombatMoveTarget.ForPosition(previousDestination, route.RouteId));

        Assert.That(plan.MoveTarget.HasDestination, Is.True);
        Assert.That(plan.MoveTarget.HasAssaultRouteKey, Is.False);
        Assert.That(plan.MoveTarget.Destination, Is.Not.EqualTo(previousDestination));
    }

    [Test]
    public void Planner_StandoffSiegeFallsBackToTheStandardPlanWithoutAMagicStoneDamageSkill()
    {
        Character owner = CreateCharacter("StandoffSiege", new Sword(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new CombatEditModeTestUtil.AiPlannerBasicAttackSkill());
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyInRange));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PursueEnemy));
        Assert.That(plan.MoveTarget.TargetCharacter, Is.SameAs(enemy));
    }

    [Test]
    public void Planner_StandoffSiegeFallsBackToTheStandardPlanWithoutAnEnemyStone()
    {
        Character owner = CreateCharacter("StandoffSiege", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        CombatAiContext context = Context(owner, enemies: new[] { Intel(enemy) });
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.EnemyInRange));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PursueEnemy));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_StandoffSiegeStillUsesThePersonalityWhenTheRecommendedWeaponChanges()
    {
        Character owner = CreateCharacter("StandoffSiege", new Bible(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(4f, 0f, 0f), team: CombatTeam.Enemy);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(WeaponKind.Bible));
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(30f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.TransitionReason, Is.EqualTo(CombatAiReasonCode.PersonalityPreference));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.PersonalitySignature));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
    }

    [Test]
    public void Planner_StandoffSiegeTargetsTheEnemyStoneWithAUsableDamageSkill()
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

        Character owner = CreateCharacter("StandoffSiege", new Wand(), Vector3.zero);
        Character enemy = CreateCharacter("Enemy", new Sword(), new Vector3(3f, 0f, 0f), team: CombatTeam.Enemy);
        SkillBase stoneSkill = CreateMagicStoneDamageSkill(WeaponKind.Wand);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, stoneSkill);
        CombatAiContext context = Context(
            owner,
            enemies: new[] { Intel(enemy) },
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: new Vector3(10f, 0f, 0f));
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.Skill, Is.SameAs(stoneSkill));
        Assert.That(plan.SkillContext.PrimaryStone, Is.SameAs(stone));
        Assert.That(plan.SkillContext.PrimaryTarget, Is.Null);
    }

    [Test]
    public void Planner_StandoffSiegeApproachesStoneWhenPreferredRangeCannotAttack()
    {
        Character owner = CreateCharacter("StandoffSiege", new Wand(), Vector3.zero);
        CombatEditModeTestUtil.SetAvailableCombatSkills(owner, CreateMagicStoneDamageSkill(WeaponKind.Wand));
        Vector3 enemyStone = new Vector3(10f, 0f, 0f);
        CombatAiContext context = Context(
            owner,
            ownStone: new Vector3(-20f, 0f, 0f),
            enemyStone: enemyStone);
        CombatAiPersonalityProfile profile = Track(
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege));

        CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

        Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        Assert.That(plan.ActionCode, Is.EqualTo(CombatAiMoveCode.AdvanceEnemyStone));
        Assert.That(plan.MoveTarget.HasDestination, Is.True);
        Assert.That(
            Vector3.Distance(plan.MoveTarget.Destination, enemyStone),
            Is.LessThan(Vector3.Distance(owner.transform.position, enemyStone)));
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
        Character markedStoneAttacker = null)
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
            markedStoneAttacker: markedStoneAttacker);
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
