using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;
using static CombatEditModeTestUtil;

public sealed class CombatAiSkillTests
{
    [Test]
    public void Planner_SelectsAnotherEnemyWhenAllyCastingWillDefeatTarget()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject firstEnemyGo = new GameObject("PendingDefeatEnemy");
        GameObject secondEnemyGo = new GameObject("AvailableEnemy");
        GameObject allyGo = new GameObject("CastingAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character firstEnemy = firstEnemyGo.AddComponent<Character>();
            firstEnemy.SetTeam(CombatTeam.Enemy);
            firstEnemy.Health.Initialize(30, 5);
            Character secondEnemy = secondEnemyGo.AddComponent<Character>();
            secondEnemy.SetTeam(CombatTeam.Enemy);
            secondEnemy.Health.Initialize(30, 20);
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            SkillBase slash = CombatSkillFactory.Create(SkillId.Sword_Slash);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, slash);
            firstEnemyGo.transform.position = new Vector3(1f, 0f, 0f);
            secondEnemyGo.transform.position = new Vector3(2f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[]
                {
                    CreateIntel(firstEnemy, true, firstEnemyGo.transform.position),
                    CreateIntel(secondEnemy, true, secondEnemyGo.transform.position),
                },
                allyPendingDamage: new[] { new CombatAiPendingDamage(firstEnemy, 5) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(secondEnemy));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(secondEnemyGo);
            Object.DestroyImmediate(firstEnemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_LongCastScoreDropsWhenEnemyCanEnterRangeBeforeCompletion()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemy.EquipWeapon(new Sword(range: 1f));
            SkillBase skill = new AiPlannerLongCastBoltSkill();
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, skill);

            enemyGo.transform.position = new Vector3(5f, 0f, 0f);
            CombatAiContext safeContext = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position, moveSpeed: 0.5f) });
            float safeScore = FindSkillScore(CombatAiPlanner.BuildDebugSnapshot(safeContext, null), skill, enemy);

            CombatAiContext riskyContext = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position, moveSpeed: 3.5f) });
            float riskyScore = FindSkillScore(CombatAiPlanner.BuildDebugSnapshot(riskyContext, null), skill, enemy);

            Assert.That(riskyScore, Is.LessThan(safeScore - 10f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_EquivalentStatusWithLongRemainingTimeGetsLargerPenalty()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Bible());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            ally.EquipWeapon(new Sword());
            SkillBase skill = CombatSkillFactory.Create(SkillId.Bible_StrBuff);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, skill);
            var shortEffect = new CombatStatusEffectSnapshot(
                "Short", CombatStatusEffects.EffectType.StatModifier, CombatStatusEffects.StatKind.STR,
                1.25f, 0f, 0f, 0.5f);
            var longEffect = new CombatStatusEffectSnapshot(
                "Long", CombatStatusEffects.EffectType.StatModifier, CombatStatusEffects.StatKind.STR,
                1.25f, 0f, 0f, 4f);

            CombatAiContext shortContext = CreatePlannerContext(
                owner,
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position, statusEffects: new[] { shortEffect }) });
            CombatAiContext longContext = CreatePlannerContext(
                owner,
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position, statusEffects: new[] { longEffect }) });

            float shortScore = FindSkillScore(CombatAiPlanner.BuildDebugSnapshot(shortContext, null), skill, ally);
            float longScore = FindSkillScore(CombatAiPlanner.BuildDebugSnapshot(longContext, null), skill, ally);

            Assert.That(longScore, Is.LessThan(shortScore - 40f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_HealingAreaChoosesMidpointThatCoversTwoInjuredAllies()
    {
        GameObject ownerGo = new GameObject("RosaryOwner");
        GameObject firstAllyGo = new GameObject("FirstInjuredAlly");
        GameObject secondAllyGo = new GameObject("SecondInjuredAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Rosary());
            Character firstAlly = firstAllyGo.AddComponent<Character>();
            firstAlly.Health.Initialize(30, 5);
            Character secondAlly = secondAllyGo.AddComponent<Character>();
            secondAlly.Health.Initialize(30, 5);
            firstAllyGo.transform.position = new Vector3(2f, 0f, 0f);
            secondAllyGo.transform.position = new Vector3(7f, 0f, 0f);
            SkillBase healingArea = new RosaryHealingAreaSkill(radius: 3f);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, healingArea);
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(firstAlly, true, firstAllyGo.transform.position),
                    CreateIntel(secondAlly, true, secondAllyGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(healingArea));
            Assert.That(snapshot.SelectedSkill.SkillContext.TargetPoint.x, Is.EqualTo(4.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(secondAllyGo);
            Object.DestroyImmediate(firstAllyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ProductionAndDebugPlansMatchWhenNoSkillsAreAvailable()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            CombatAiContext context = CreatePlannerContext(owner);

            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ProductionAndDebugPlansMatchWhenSkillHasMultipleTargets()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject healthyGo = new GameObject("HealthyAlly");
        GameObject lowHpGo = new GameObject("LowHpAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Rosary());
            Character healthy = healthyGo.AddComponent<Character>();
            healthy.Health.Initialize(30, 30);
            Character lowHp = lowHpGo.AddComponent<Character>();
            lowHp.Health.Initialize(30, 5);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new AiPlannerHealSkill());
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(healthy, true, healthyGo.transform.position),
                    CreateIntel(lowHp, true, lowHpGo.transform.position),
                });

            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(lowHpGo);
            Object.DestroyImmediate(healthyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ProductionAndDebugPlansMatchWhenPersonalityRejectsSkill()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = Vector3.forward;
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new AiPlannerBasicAttackSkill());
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Lonely);
            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) });

            AssertPlanMatchesDebugSnapshot(context, profile);
            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, profile);
            Assert.That(snapshot.SelectedSkill.Skill, Is.Null);
            Assert.That(snapshot.SelectedSkill.Breakdown.BaseScore, Is.EqualTo(3f));
        }
        finally
        {
            if (profile != null) Object.DestroyImmediate(profile);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_DoesNotSelectCooldownSkill()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = Vector3.forward;

            var cooldownSkill = new AiPlannerBoltCooldownSkill();
            var basicSkill = new AiPlannerBasicAttackSkill();
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, cooldownSkill, basicSkill);
            owner.SkillCooldowns.StartCooldown(cooldownSkill);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, hasKnownPosition: true, knownPosition: enemyGo.transform.position) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(basicSkill));
            Assert.That(snapshot.SelectedSkill.Evaluation.CanUse, Is.True);
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ChoosesLowHpAllyForHealSkill()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject healthyGo = new GameObject("HealthyAlly");
        GameObject lowHpGo = new GameObject("LowHpAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Rosary());
            Character healthy = healthyGo.AddComponent<Character>();
            healthy.Health.Initialize(30, 30);
            Character lowHp = lowHpGo.AddComponent<Character>();
            lowHp.Health.Initialize(30, 5);
            healthyGo.transform.position = Vector3.right;
            lowHpGo.transform.position = Vector3.left;

            var healSkill = new AiPlannerHealSkill();
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, healSkill);

            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(healthy, hasKnownPosition: true, knownPosition: healthyGo.transform.position),
                    CreateIntel(lowHp, hasKnownPosition: true, knownPosition: lowHpGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(healSkill));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(lowHp));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(lowHpGo);
            Object.DestroyImmediate(healthyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_AreaSkillPrefersPointThatHitsMultipleEnemies()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("Enemy");
        GameObject secondEnemyGo = new GameObject("Enemy2");
        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            Character secondEnemy = secondEnemyGo.AddComponent<Character>();
            secondEnemy.SetTeam(CombatTeam.Enemy);
            secondEnemy.Health.Initialize(30);

            ownerGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(0f, 0f, 2f);
            secondEnemyGo.transform.position = new Vector3(1f, 0f, 3f);
            system.AllyCharacters.Add(owner);
            system.EnemyCharacters.Add(enemy);
            system.EnemyCharacters.Add(secondEnemy);
            system.AssignTeamsFromLists();
            CombatEditModeTestUtil.WireVision(owner.Vision, system);
            owner.Vision.Initialize();
            owner.Vision.UpdateVision();

            var areaSkill = new AiPlannerAreaBlastSkill();
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, areaSkill);
            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[]
                {
                    CreateIntel(enemy, hasKnownPosition: true, knownPosition: enemyGo.transform.position),
                    CreateIntel(secondEnemy, hasKnownPosition: true, knownPosition: secondEnemyGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(areaSkill));
            Assert.That(snapshot.SelectedSkill.Evaluation.ResolvedTargets.Count, Is.EqualTo(2));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(secondEnemyGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void Planner_HealPrefersForwardAllyWhenHealingNeedIsEqual()
    {
        GameObject ownerGo = new GameObject("RosaryOwner");
        GameObject rearGo = new GameObject("RearAlly");
        GameObject forwardGo = new GameObject("ForwardAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Rosary());
            Character rear = rearGo.AddComponent<Character>();
            rear.Health.Initialize(30, 15);
            Character forward = forwardGo.AddComponent<Character>();
            forward.Health.Initialize(30, 15);
            rearGo.transform.position = new Vector3(2f, 0f, 0f);
            forwardGo.transform.position = new Vector3(8f, 0f, 0f);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new AiPlannerHealSkill());
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(rear, true, rearGo.transform.position),
                    CreateIntel(forward, true, forwardGo.transform.position),
                },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(10f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(forward));
        }
        finally
        {
            Object.DestroyImmediate(forwardGo);
            Object.DestroyImmediate(rearGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_BibleBuffsAllyWhoseRoleMatchesStat()
    {
        GameObject ownerGo = new GameObject("BibleOwner");
        GameObject swordGo = new GameObject("SwordAlly");
        GameObject wandGo = new GameObject("WandAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Bible());
            Character sword = swordGo.AddComponent<Character>();
            sword.Health.Initialize(30);
            sword.EquipWeapon(new Sword());
            Character wand = wandGo.AddComponent<Character>();
            wand.Health.Initialize(30);
            wand.EquipWeapon(new Wand());
            var strBuff = CombatSkillFactory.Create(SkillId.Bible_StrBuff);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, strBuff);

            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(wand, true, wandGo.transform.position),
                    CreateIntel(sword, true, swordGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(strBuff));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(sword));
        }
        finally
        {
            Object.DestroyImmediate(wandGo);
            Object.DestroyImmediate(swordGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldProtectsAdvancingAllyBeforeRearSupporter()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject attackerGo = new GameObject("Attacker");
        GameObject supporterGo = new GameObject("Supporter");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character attacker = attackerGo.AddComponent<Character>();
            attacker.Health.Initialize(30);
            Character supporter = supporterGo.AddComponent<Character>();
            supporter.Health.Initialize(30);
            attackerGo.transform.position = new Vector3(4f, 0f, 0f);
            supporterGo.transform.position = new Vector3(-2f, 0f, 0f);
            SkillBase guard = CombatSkillFactory.Create(SkillId.Shield_ShoulderGuard);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, guard);
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(
                        attacker,
                        true,
                        attackerGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy),
                    CreateIntel(
                        supporter,
                        true,
                        supporterGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.SupportAlly),
                },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(guard));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(attacker));
        }
        finally
        {
            Object.DestroyImmediate(supporterGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_GrimoireDebuffsEnemyWhoseRoleMatchesStat()
    {
        GameObject ownerGo = new GameObject("GrimoireOwner");
        GameObject swordGo = new GameObject("SwordEnemy");
        GameObject wandGo = new GameObject("WandEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Grimoire());
            Character sword = swordGo.AddComponent<Character>();
            sword.SetTeam(CombatTeam.Enemy);
            sword.Health.Initialize(30);
            sword.EquipWeapon(new Sword());
            Character wand = wandGo.AddComponent<Character>();
            wand.SetTeam(CombatTeam.Enemy);
            wand.Health.Initialize(30);
            wand.EquipWeapon(new Wand());
            var strDebuff = CombatSkillFactory.Create(SkillId.Grimoire_StrDebuff);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, strDebuff);

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[]
                {
                    CreateIntel(wand, true, wandGo.transform.position),
                    CreateIntel(sword, true, swordGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(strDebuff));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(sword));
        }
        finally
        {
            Object.DestroyImmediate(wandGo);
            Object.DestroyImmediate(swordGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_PrefersInRangeEnemyStoneOverNearbyEnemy()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject enemyGo = new GameObject("NearbyEnemy");
        GameObject stoneGo = new GameObject("EnemyMainStone");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            SkillBase slash = CombatSkillFactory.Create(SkillId.Sword_Slash);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, slash);

            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(1.5f, 0f, 1.2f);

            MagicStone stone = stoneGo.AddComponent<MagicStone>();
            stone.Setup(featureIndex: 1, FeatureType.EnemyMainStone, isMainStone: true, stoneHeight: 3f);
            stoneGo.transform.position = new Vector3(0f, 0f, 1.2f);
            stoneGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: stoneGo.transform.position,
                hasEnemyStoneHealth: true,
                enemyStoneHP: 100,
                enemyStoneMaxHP: 100);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(slash));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryStone, Is.EqualTo(stone));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(stoneGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }
}
