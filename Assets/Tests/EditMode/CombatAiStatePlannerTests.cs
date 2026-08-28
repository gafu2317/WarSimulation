using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
        IReadOnlyList<CombatAiPendingDamage> pendingDamage = null)
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
            forestCandidates: forests);
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

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
