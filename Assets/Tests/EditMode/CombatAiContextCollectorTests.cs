using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatAiContextCollectorTests
{
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
            Assert.That(context.VisibleEnemies, Does.Contain(fixture.Enemy));
            Assert.That(context.RememberedEnemies, Does.Contain(fixture.RememberedEnemy));
            Assert.That(context.Allies, Does.Contain(fixture.Owner));
            Assert.That(context.HasOwnStonePosition, Is.True);
            Assert.That(context.OwnStonePosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(context.HasEnemyStonePosition, Is.True);
            Assert.That(context.EnemyStonePosition, Is.EqualTo(new Vector3(8f, 0f, 8f)));
            Assert.That(context.Weather, Is.EqualTo(CombatMapSystem.Weather.Rainy));
            Assert.That(context.WindVector, Is.EqualTo(new Vector3(1f, 0f, 0.5f)));
            Assert.That(context.RockPositions, Does.Contain(new Vector3(2f, 0f, 2f)));
            Assert.That(context.BridgePositions, Does.Contain(new Vector3(3f, 0f, 3f)));
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
            Assert.That(rememberedIntel.HasLastKnownPosition, Is.True);
            Assert.That(rememberedIntel.LastKnownPosition, Is.EqualTo(new Vector3(4f, 0f, 7f)));
            Assert.That(rememberedIntel.RecognizesOwner, Is.False);
            Assert.That(rememberedIntel.HasObjective, Is.False);

            CombatCharacterIntel allyIntel = FindIntel(context.AllyIntel, fixture.Owner);
            Assert.That(allyIntel.HasObjective, Is.False);
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
            Assert.That(context.VisibleEnemies, Is.Empty);
            Assert.That(context.EnemyIntel, Is.Empty);
            Assert.That(context.HasOwnStonePosition, Is.False);
            Assert.That(context.HasEnemyStonePosition, Is.False);
            Assert.That(context.RockPositions, Is.Empty);
            Assert.That(context.HighGroundCandidates, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(mapGo);
            Object.DestroyImmediate(systemGo);
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
                visibleEnemies: System.Array.Empty<Character>(),
                rememberedEnemies: System.Array.Empty<Character>(),
                allies: System.Array.Empty<Character>(),
                enemyIntel: new[] { CreateIntel(enemy, hasKnownPosition: false, knownPosition: default) },
                allyIntel: System.Array.Empty<CombatCharacterIntel>(),
                weather: CombatMapSystem.Weather.Sunny,
                windVector: Vector3.zero,
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: false,
                enemyStonePosition: default,
                rockPositions: System.Array.Empty<Vector3>(),
                bridgePositions: System.Array.Empty<Vector3>(),
                highGroundCandidates: System.Array.Empty<Vector3>(),
                forestCandidates: System.Array.Empty<Vector3>());

            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);

            Assert.That(assessment.GetValue("OwnStoneThreat"), Is.EqualTo(0f));
            Assert.That(assessment.GetValue("ReachableEnemyValue"), Is.EqualTo(0f));
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

            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context, captureDebug: false);

            Assert.That(assessment.GetValue(CombatAiMetricIndex.WinProximity), Is.EqualTo(75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

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
                visibleEnemies: new[] { firstEnemy, secondEnemy },
                allies: new[] { ally },
                enemyIntel: new[]
                {
                    CreateIntel(firstEnemy, true, firstEnemyGo.transform.position),
                    CreateIntel(secondEnemy, true, secondEnemyGo.transform.position),
                },
                allyPendingDamage: new[] { new CombatAiPendingDamage(ally, firstEnemy, 5) });

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
                visibleEnemies: new[] { enemy },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position, moveSpeed: 0.5f) });
            float safeScore = FindSkillScore(CombatAiPlanner.BuildDebugSnapshot(safeContext, null), skill, enemy);

            CombatAiContext riskyContext = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
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
                allies: new[] { ally },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position, statusEffects: new[] { shortEffect }) });
            CombatAiContext longContext = CreatePlannerContext(
                owner,
                allies: new[] { ally },
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
    public void Planner_StableAllyFrontlineRaisesDamageDealerStoneObjectiveScore()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("FrontlineAlly");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            ally.EquipWeapon(new Sword());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            allyGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(4f, 0f, 0f);

            CombatAiContext withoutFrontline = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                allies: new[] { ally },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(15f, 0f, 0f));
            CombatAiContext withFrontline = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                allies: new[] { ally },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                allyIntel: new[]
                {
                    CreateIntel(
                        ally,
                        true,
                        allyGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy,
                        intendedTarget: enemy),
                },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(15f, 0f, 0f));

            float withoutScore = FindObjectiveScore(
                CombatAiPlanner.BuildDebugSnapshot(withoutFrontline, null),
                CombatObjective.DestroyEnemyStone);
            float withScore = FindObjectiveScore(
                CombatAiPlanner.BuildDebugSnapshot(withFrontline, null),
                CombatObjective.DestroyEnemyStone);

            Assert.That(withScore - withoutScore, Is.EqualTo(14f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_HighGroundScoreDropsWhenAllyIsAlreadySearchingThere()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("SearchingAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            Vector3 highGround = new Vector3(8f, 3f, 0f);

            CombatAiContext freeContext = CreatePlannerContext(
                owner,
                allies: new[] { ally },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                highGroundCandidates: new[] { highGround });
            CombatAiContext occupiedContext = CreatePlannerContext(
                owner,
                allies: new[] { ally },
                allyIntel: new[]
                {
                    CreateIntel(
                        ally,
                        true,
                        allyGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.Search,
                        hasIntendedDestination: true,
                        intendedDestination: highGround),
                },
                highGroundCandidates: new[] { highGround });

            float freeScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(freeContext, null),
                CombatAiMoveCode.TakeHighGround);
            float occupiedScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(occupiedContext, null),
                CombatAiMoveCode.TakeHighGround);

            Assert.That(freeScore - occupiedScore, Is.EqualTo(36f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void MoveScorer_RouteThroughEnemyRangeIsRiskierThanClearRoute()
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
            enemy.EquipWeapon(new Sword(range: 4f));
            enemyGo.transform.position = new Vector3(5f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) });

            float dangerous = CombatAiMoveScorer.EvaluateRouteRisk(
                context,
                Vector3.zero,
                new Vector3(10f, 0f, 0f));
            float clear = CombatAiMoveScorer.EvaluateRouteRisk(
                context,
                Vector3.zero,
                new Vector3(0f, 0f, 10f));

            Assert.That(dangerous, Is.GreaterThan(clear));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
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
                visibleEnemies: new[] { enemy },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) });
            CombatAiContext threatenedContext = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                enemyPendingDamage: new[] { new CombatAiPendingDamage(enemy, owner, 15) });

            float safeThreat = CombatAiAssessmentBuilder.Build(safeContext, false)
                .GetValue(CombatAiMetricIndex.SelfThreat);
            float incomingThreat = CombatAiAssessmentBuilder.Build(threatenedContext, false)
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
    public void Planner_BridgeDetourScoresHigherWhenDirectStoneRouteCrossesEnemyRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemy.EquipWeapon(new Sword(range: 4f));
            enemyGo.transform.position = new Vector3(5f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(10f, 0f, 0f),
                bridgePositions: new[] { new Vector3(0f, 0f, 10f) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            float directScore = FindMoveScore(snapshot, CombatAiMoveCode.AdvanceEnemyStone);
            float bridgeScore = FindMoveScore(snapshot, CombatAiMoveCode.AdvanceViaBridge);

            Assert.That(bridgeScore, Is.GreaterThan(directScore));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldCreatesInterceptionPointBetweenEnemyAndFragileAlly()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject allyGo = new GameObject("FragileAlly");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30, 5);
            ally.EquipWeapon(new Wand());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemy.EquipWeapon(new Sword());
            ownerGo.transform.position = new Vector3(-1f, 0f, 0f);
            allyGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(8f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                allies: new[] { ally },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry intercept = FindMove(snapshot, CombatAiMoveCode.InterceptThreat);

            Assert.That(intercept.Target.Destination.x, Is.GreaterThan(allyGo.transform.position.x));
            Assert.That(intercept.Target.Destination.x, Is.LessThan(enemyGo.transform.position.x));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldFollowsAdvancingFrontlineRegardlessOfWeaponKind()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject frontlineGo = new GameObject("WandFrontline");
        GameObject backlineGo = new GameObject("FragileBackline");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character frontline = frontlineGo.AddComponent<Character>();
            frontline.Health.Initialize(30);
            frontline.EquipWeapon(new Wand());
            Character backline = backlineGo.AddComponent<Character>();
            backline.Health.Initialize(30, 5);
            backline.EquipWeapon(new Rosary());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(10f, 0f, 0f);
            frontlineGo.transform.position = new Vector3(4f, 0f, 0f);
            backlineGo.transform.position = new Vector3(-2f, 0f, 0f);
            Vector3 frontlineDestination = new Vector3(8f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy },
                allies: new[] { frontline, backline },
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                allyIntel: new[]
                {
                    CreateIntel(
                        frontline,
                        true,
                        frontlineGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy,
                        intendedTarget: enemy,
                        hasIntendedDestination: true,
                        intendedDestination: frontlineDestination),
                    CreateIntel(backline, true, backlineGo.transform.position),
                },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry supportMove = FindMove(snapshot, CombatAiMoveCode.SupportAlly);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(supportMove.Target.Destination.x, Is.GreaterThan(frontlineGo.transform.position.x));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(backlineGo);
            Object.DestroyImmediate(frontlineGo);
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
                allies: new[] { firstAlly, secondAlly },
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
    public void Planner_SelectsHoldPositionWhenNoMoveTargetsExist()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            CombatAiContext context = CreatePlannerContext(owner);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo("HoldPosition"));
            Assert.That(snapshot.FinalPlan.MoveTarget.Kind, Is.EqualTo(CombatMoveTargetKind.None));
        }
        finally
        {
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
                visibleEnemies: new[] { enemy },
                enemyIntel: new[] { CreateIntel(enemy, hasKnownPosition: true, knownPosition: enemyGo.transform.position) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(basicSkill));
            Assert.That(snapshot.SelectedSkill.Evaluation.CanUse, Is.True);
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
                allies: new[] { healthy, lowHp },
                allyIntel: new[]
                {
                    CreateIntel(healthy, hasKnownPosition: true, knownPosition: healthyGo.transform.position),
                    CreateIntel(lowHp, hasKnownPosition: true, knownPosition: lowHpGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(healSkill));
            Assert.That(snapshot.SelectedSkill.SkillContext.PrimaryTarget, Is.EqualTo(lowHp));
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
            enemyGo.transform.position = new Vector3(2f, 0f, 0f);
            secondEnemyGo.transform.position = new Vector3(3f, 0f, 0f);
            system.AllyCharacters.Add(owner);
            system.EnemyCharacters.Add(enemy);
            system.EnemyCharacters.Add(secondEnemy);
            system.AssignTeamsFromLists();

            var areaSkill = new AiPlannerAreaBlastSkill();
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, areaSkill);
            CombatAiContext context = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy, secondEnemy },
                enemyIntel: new[]
                {
                    CreateIntel(enemy, hasKnownPosition: true, knownPosition: enemyGo.transform.position),
                    CreateIntel(secondEnemy, hasKnownPosition: true, knownPosition: secondEnemyGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
            Assert.That(snapshot.SelectedSkill.Skill, Is.EqualTo(areaSkill));
            Assert.That(snapshot.SelectedSkill.Evaluation.ResolvedTargets.Count, Is.EqualTo(2));
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
    public void Planner_SearchPrefersAdvancingOverHighGroundWhenNoEnemyInfo()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            ownerGo.transform.position = Vector3.zero;

            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(18f, 0f, 0f),
                highGroundCandidates: new[] { new Vector3(2f, 3f, 0f) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.Search));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo("AdvanceEnemyStone"));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_DoesNotSelectHighGroundWhenAlreadyAtCandidate()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            ownerGo.transform.position = new Vector3(6f, 0f, 5f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                highGroundCandidates: new[] { new Vector3(6f, 4f, 5f) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.MoveEntries.Exists(entry => entry.Code == "TakeHighGround"), Is.False);
        }
        finally
        {
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
                visibleEnemies: new[] { enemy },
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
                visibleEnemies: new[] { enemy },
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
    public void Planner_UsesWeaponWeightsProfileOverride()
    {
        GameObject ownerGo = new GameObject("WeightedSwordOwner");
        CombatAiWeaponWeightsProfile profile = ScriptableObject.CreateInstance<CombatAiWeaponWeightsProfile>();
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            profile.ApplyCurrentDefaults();
            profile.SetObjectiveWeight(WeaponKind.Sword, CombatObjective.AttackEnemy, -100f);
            profile.SetObjectiveWeight(WeaponKind.Sword, CombatObjective.Search, 120f);

            CombatAiContext context = CreatePlannerContext(owner);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null, profile);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.Search));
        }
        finally
        {
            Object.DestroyImmediate(profile);
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
        }
        finally
        {
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
                allies: new[] { wand, sword },
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
                visibleEnemies: new[] { wand, sword },
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
    public void Planner_SwordPrefersPursuitWithAllySupportOverIsolatedPursuit()
    {
        GameObject ownerGo = new GameObject("SwordOwner");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");
        GameObject secondEnemyGo = new GameObject("SecondEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.Health.Initialize(30);
            Character secondEnemy = secondEnemyGo.AddComponent<Character>();
            secondEnemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(10f, 0f, 0f);
            secondEnemyGo.transform.position = new Vector3(12f, 0f, 0f);
            allyGo.transform.position = new Vector3(-10f, 0f, 0f);

            CombatAiContext isolated = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy, secondEnemy },
                allies: new[] { ally },
                enemyIntel: new[]
                {
                    CreateIntel(enemy, true, enemyGo.transform.position),
                    CreateIntel(secondEnemy, true, secondEnemyGo.transform.position),
                },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) });
            allyGo.transform.position = new Vector3(8f, 0f, 0f);
            CombatAiContext supported = CreatePlannerContext(
                owner,
                visibleEnemies: new[] { enemy, secondEnemy },
                allies: new[] { ally },
                enemyIntel: new[]
                {
                    CreateIntel(enemy, true, enemyGo.transform.position),
                    CreateIntel(secondEnemy, true, secondEnemyGo.transform.position),
                },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) });

            float isolatedScore = FindMoveScore(CombatAiPlanner.BuildDebugSnapshot(isolated, null), CombatAiMoveCode.PursueEnemy);
            float supportedScore = FindMoveScore(CombatAiPlanner.BuildDebugSnapshot(supported, null), CombatAiMoveCode.PursueEnemy);

            Assert.That(supportedScore, Is.GreaterThan(isolatedScore));
        }
        finally
        {
            Object.DestroyImmediate(secondEnemyGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_MoveScoreDropsWhenAllyAlreadyMovesToSameDestination()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            Vector3 forest = new Vector3(6f, 0f, 0f);

            CombatAiContext free = CreatePlannerContext(
                owner,
                allies: new[] { ally },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                forestCandidates: new[] { forest });
            CombatAiContext occupied = CreatePlannerContext(
                owner,
                allies: new[] { ally },
                allyIntel: new[]
                {
                    CreateIntel(
                        ally,
                        true,
                        allyGo.transform.position,
                        hasIntendedDestination: true,
                        intendedDestination: forest),
                },
                forestCandidates: new[] { forest });

            float freeScore = FindMoveScore(CombatAiPlanner.BuildDebugSnapshot(free, null), CombatAiMoveCode.MoveForest);
            float occupiedScore = FindMoveScore(CombatAiPlanner.BuildDebugSnapshot(occupied, null), CombatAiMoveCode.MoveForest);

            Assert.That(freeScore - occupiedScore, Is.EqualTo(12f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_RosaryForestPositionKeepsAllyWithinSupportRange()
    {
        GameObject ownerGo = new GameObject("RosaryOwner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Rosary());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            allyGo.transform.position = new Vector3(5f, 0f, 0f);
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new AiPlannerHealSkill());
            Vector3 validForest = new Vector3(8f, 0f, 0f);
            Vector3 distantForest = new Vector3(20f, 0f, 0f);

            CombatAiContext context = CreatePlannerContext(
                owner,
                allies: new[] { ally },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                forestCandidates: new[] { distantForest, validForest });

            CombatAiMoveCandidateEntry move = FindMove(
                CombatAiPlanner.BuildDebugSnapshot(context, null),
                CombatAiMoveCode.MoveForest);

            Assert.That(move.Target.Destination, Is.EqualTo(validForest));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    private static AiContextFixture CreateFixture()
    {
        var fixture = new AiContextFixture();
        fixture.SystemGo = new GameObject("CombatCharacterSystem");
        fixture.MapGo = new GameObject("CombatMapSystem");
        fixture.OwnerGo = new GameObject("Owner");
        fixture.ObserverGo = new GameObject("Observer");
        fixture.EnemyGo = new GameObject("VisibleEnemy");
        fixture.RememberedEnemyGo = new GameObject("RememberedEnemy");

        fixture.System = fixture.SystemGo.AddComponent<CombatCharacterSystem>();
        fixture.MapSystem = fixture.MapGo.AddComponent<CombatMapSystem>();
        fixture.Map = CreateMap();
        fixture.MapSystem.SetCurrentMap(fixture.Map);
        CombatEditModeTestUtil.SetPrivateField(fixture.MapSystem, "<CurrentWeather>k__BackingField", CombatMapSystem.Weather.Rainy);
        CombatEditModeTestUtil.SetPrivateField(fixture.MapSystem, "<WindVector>k__BackingField", new Vector3(1f, 0f, 0.5f));
        CombatEditModeTestUtil.WireMapSystem(fixture.System, fixture.MapSystem);

        fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
        fixture.Observer = fixture.ObserverGo.AddComponent<Character>();
        fixture.Enemy = fixture.EnemyGo.AddComponent<Character>();
        fixture.RememberedEnemy = fixture.RememberedEnemyGo.AddComponent<Character>();

        fixture.Owner.Health.Initialize(30);
        fixture.Observer.Health.Initialize(30);
        fixture.Enemy.Health.Initialize(30);
        fixture.RememberedEnemy.Health.Initialize(30);
        fixture.Owner.EquipWeapon(new Sword());

        fixture.OwnerGo.transform.position = new Vector3(5f, 0f, 4f);
        fixture.ObserverGo.transform.position = new Vector3(5f, 0f, 5f);
        fixture.EnemyGo.transform.position = new Vector3(5f, 0f, 9f);
        fixture.RememberedEnemyGo.transform.position = new Vector3(5f, 0f, -5f);
        AddCharacterCollider(fixture.ObserverGo);
        AddCharacterCollider(fixture.EnemyGo);
        AddCharacterCollider(fixture.RememberedEnemyGo);
        Physics.SyncTransforms();

        fixture.System.AllyCharacters.Add(fixture.Owner);
        fixture.System.AllyCharacters.Add(fixture.Observer);
        fixture.System.EnemyCharacters.Add(fixture.Enemy);
        fixture.System.EnemyCharacters.Add(fixture.RememberedEnemy);
        fixture.System.AssignTeamsFromLists();

        CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
        fixture.Owner.Vision.Initialize();

        fixture.ObserverVision = fixture.Observer.Vision;
        CombatEditModeTestUtil.WireVision(fixture.ObserverVision, fixture.System);
        fixture.ObserverVision.Initialize();

        fixture.Collector = fixture.ObserverGo.GetComponent<CombatAiContextCollector>() ??
            fixture.ObserverGo.AddComponent<CombatAiContextCollector>();
        CombatEditModeTestUtil.WireCollector(fixture.Collector, fixture.System, fixture.MapSystem);
        return fixture;
    }

    private static MapData CreateMap()
    {
        var height = new HeightMap(12, 12, 1f);
        var ground = new GroundStateGrid(12, 12, 1f);
        height.SetHeight(6, 5, 4f);
        height.SetHeight(9, 9, 2f);

        var map = new MapData(height, ground, seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(8f, 0f, 8f)));
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(2f, 0f, 2f)));
        map.AddFeature(new PlacedFeature(FeatureType.Bridge, new Vector3(3f, 0f, 3f)));
        map.AddForestRegion(new ForestRegion(new Vector2(5f, 7f), 1.25f, 0f, 1f));
        map.AddMountain(new MountainRegion(
            MountainKind.Large,
            new Vector2(6f, 5f),
            2f,
            new Vector2(1.2f, 1.2f),
            0f,
            null));
        map.AddMountain(new MountainRegion(
            MountainKind.Small,
            new Vector2(9f, 9f),
            1f,
            new Vector2(0.8f, 0.8f),
            0f,
            null));
        return map;
    }

    private static void AddCharacterCollider(GameObject go)
    {
        var collider = go.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.height = 2f;
    }

    private static CombatCharacterIntel FindIntel(IReadOnlyList<CombatCharacterIntel> intel, Character character)
    {
        for (int i = 0; i < intel.Count; i++)
        {
            if (intel[i].Character == character) return intel[i];
        }

        Assert.Fail($"No intel found for {character.name}.");
        return default;
    }

    private static CombatAiContext CreatePlannerContext(
        Character owner,
        IReadOnlyList<Character> visibleEnemies = null,
        IReadOnlyList<Character> rememberedEnemies = null,
        IReadOnlyList<Character> allies = null,
        IReadOnlyList<CombatCharacterIntel> enemyIntel = null,
        IReadOnlyList<CombatCharacterIntel> allyIntel = null,
        bool hasOwnStonePosition = false,
        Vector3 ownStonePosition = default,
        bool hasEnemyStonePosition = false,
        Vector3 enemyStonePosition = default,
        IReadOnlyList<Vector3> highGroundCandidates = null,
        bool hasEnemyStoneHealth = false,
        int enemyStoneHP = 0,
        int enemyStoneMaxHP = 0,
        IReadOnlyList<CombatAiPendingDamage> allyPendingDamage = null,
        IReadOnlyList<CombatAiPendingDamage> enemyPendingDamage = null,
        IReadOnlyList<Vector3> bridgePositions = null,
        IReadOnlyList<Vector3> forestCandidates = null)
    {
        return new CombatAiContext(
            owner,
            visibleEnemies ?? System.Array.Empty<Character>(),
            rememberedEnemies ?? System.Array.Empty<Character>(),
            allies ?? System.Array.Empty<Character>(),
            enemyIntel ?? System.Array.Empty<CombatCharacterIntel>(),
            allyIntel ?? System.Array.Empty<CombatCharacterIntel>(),
            CombatMapSystem.Weather.Sunny,
            Vector3.zero,
            hasOwnStonePosition,
            ownStonePosition,
            hasEnemyStonePosition,
            enemyStonePosition,
            System.Array.Empty<Vector3>(),
            bridgePositions ?? System.Array.Empty<Vector3>(),
            highGroundCandidates ?? System.Array.Empty<Vector3>(),
            forestCandidates ?? System.Array.Empty<Vector3>(),
            hasEnemyStoneHealth,
            enemyStoneHP,
            enemyStoneMaxHP,
            allyPendingDamage,
            enemyPendingDamage);
    }

    private static float FindObjectiveScore(CombatAiDebugSnapshot snapshot, CombatObjective objective)
    {
        for (int i = 0; i < snapshot.ObjectiveEntries.Count; i++)
        {
            if (snapshot.ObjectiveEntries[i].Objective == objective)
            {
                return snapshot.ObjectiveEntries[i].Breakdown.Total;
            }
        }

        Assert.Fail("目的候補が見つかりません: " + objective);
        return 0f;
    }

    private static float FindSkillScore(CombatAiDebugSnapshot snapshot, SkillBase skill, Character target)
    {
        for (int i = 0; i < snapshot.SkillEntries.Count; i++)
        {
            if (snapshot.SkillEntries[i].Skill == skill &&
                snapshot.SkillEntries[i].SkillContext.PrimaryTarget == target)
            {
                return snapshot.SkillEntries[i].Breakdown.Total;
            }
        }

        Assert.Fail("スキル候補が見つかりません: " + skill.Name);
        return 0f;
    }

    private static float FindMoveScore(CombatAiDebugSnapshot snapshot, string code)
    {
        return FindMove(snapshot, code).Breakdown.Total;
    }

    private static CombatAiMoveCandidateEntry FindMove(CombatAiDebugSnapshot snapshot, string code)
    {
        for (int i = 0; i < snapshot.MoveEntries.Count; i++)
        {
            if (snapshot.MoveEntries[i].Code == code)
            {
                return snapshot.MoveEntries[i];
            }
        }

        Assert.Fail("移動候補が見つかりません: " + code);
        return null;
    }

    private static CombatCharacterIntel CreateIntel(
        Character character,
        bool hasKnownPosition,
        Vector3 knownPosition,
        bool hasDirectSight = true,
        bool hasMemory = false,
        IReadOnlyList<CombatStatusEffectSnapshot> statusEffects = null,
        bool hasObjective = false,
        CombatObjective objective = default,
        Character intendedTarget = null,
        bool hasIntendedDestination = false,
        Vector3 intendedDestination = default,
        float moveSpeed = 3.5f)
    {
        CombatHealth health = character != null ? character.Health : null;
        WeaponBase weapon = character != null ? character.EquippedWeapon ?? WeaponBase.Unarmed : WeaponBase.Unarmed;
        return new CombatCharacterIntel(
            character,
            character != null ? character.Team : default,
            character != null ? character.transform.position : default,
            hasDirectSight,
            hasMemory,
            hasKnownPosition,
            knownPosition,
            hasLastKnownPosition: hasMemory,
            lastKnownPosition: hasMemory ? knownPosition : default,
            memoryAgeSeconds: hasMemory ? 0f : float.PositiveInfinity,
            recognizesOwner: false,
            hp: health != null ? health.HP : 0,
            maxHp: health != null ? health.MaxHP : 0,
            canAct: health != null && health.CanAct,
            weaponKind: weapon.Kind,
            weaponRange: weapon.Range,
            statusEffects: statusEffects ?? System.Array.Empty<CombatStatusEffectSnapshot>(),
            hasObjective: hasObjective,
            objective: objective,
            moveSpeed: moveSpeed,
            intendedTarget: intendedTarget,
            hasIntendedDestination: hasIntendedDestination,
            intendedDestination: intendedDestination);
    }

    private sealed class AiPlannerBasicAttackSkill : SkillBase
    {
        public override string Name => "通常攻撃";
        public override float MaxRange => 3f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    private sealed class AiPlannerBoltCooldownSkill : SkillBase
    {
        public override string Name => "BoltCooldown";
        public override float CooldownSeconds => 10f;
        public override float MaxRange => 3f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    private sealed class AiPlannerHealSkill : SkillBase
    {
        public override string Name => "PlannerHeal";
        public override SkillTargetKind TargetKind => SkillTargetKind.Ally;
        public override float MaxRange => 10f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    private sealed class AiPlannerAreaBlastSkill : SkillBase
    {
        public override string Name => "PlannerAreaBlast";
        public override SkillTargetKind TargetKind => SkillTargetKind.Area;
        public override float MaxRange => 10f;
        public override float AreaRadius => 2f;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    private sealed class AiPlannerLongCastBoltSkill : SkillBase
    {
        public override string Name => "長詠唱攻撃";
        public override float CastTimeSeconds => 2.5f;
        public override float MaxRange => 30f;
        public override int EstimateDamage(Character self, SkillExecutionContext context, Character target) => 5;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }

    private sealed class AiContextFixture
    {
        public GameObject SystemGo;
        public GameObject MapGo;
        public GameObject OwnerGo;
        public GameObject ObserverGo;
        public GameObject EnemyGo;
        public GameObject RememberedEnemyGo;
        public CombatCharacterSystem System;
        public CombatMapSystem MapSystem;
        public MapData Map;
        public Character Owner;
        public Character Observer;
        public Character Enemy;
        public Character RememberedEnemy;
        public CombatVision ObserverVision;
        public CombatAiContextCollector Collector;

        public void Destroy()
        {
            if (RememberedEnemyGo != null) Object.DestroyImmediate(RememberedEnemyGo);
            if (EnemyGo != null) Object.DestroyImmediate(EnemyGo);
            if (ObserverGo != null) Object.DestroyImmediate(ObserverGo);
            if (OwnerGo != null) Object.DestroyImmediate(OwnerGo);
            if (MapGo != null) Object.DestroyImmediate(MapGo);
            if (SystemGo != null) Object.DestroyImmediate(SystemGo);
        }
    }
}
