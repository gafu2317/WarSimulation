using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;
using static CombatEditModeTestUtil;

public sealed class CombatAiMoveTests
{
    [Test]
    public void Planner_DoesNotMoveTowardDeadSupportTarget()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject deadAllyGo = new GameObject("DeadAlly");
        GameObject livingAllyGo = new GameObject("LivingAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character deadAlly = deadAllyGo.AddComponent<Character>();
            deadAlly.Health.Initialize(30);
            deadAlly.Health.TakeDamage(30);
            Character livingAlly = livingAllyGo.AddComponent<Character>();
            livingAlly.Health.Initialize(30, 20);
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(deadAlly, true, deadAllyGo.transform.position),
                    CreateIntel(livingAlly, true, livingAllyGo.transform.position),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry supportMove = FindMove(
                snapshot,
                CombatAiMoveCode.SupportAlly);

            Assert.That(
                supportMove.Target.TargetCharacter,
                Is.SameAs(livingAlly));
        }
        finally
        {
            Object.DestroyImmediate(livingAllyGo);
            Object.DestroyImmediate(deadAllyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ExcludesTemporarilyBlockedMoveDestination()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Vector3 stone = new Vector3(8f, 0f, 0f);
            Vector3 blocked = new Vector3(5.5f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: stone,
                hasBlockedMoveDestination: true,
                blockedMoveDestination: blocked);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(
                snapshot.MoveEntries.Exists(entry =>
                    entry.Code == CombatAiMoveCode.AdvanceEnemyStone),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_StoneAdvanceTargetsAttackPositionOutsideStoneCenter()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(8f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry advance = FindMove(
                snapshot,
                CombatAiMoveCode.AdvanceEnemyStone);

            Assert.That(advance.Target.Destination, Is.EqualTo(new Vector3(5.5f, 0f, 0f)));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Brain_BlocksDestinationAfterRepeatedMoveFailures()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            Vector3 destination = new Vector3(8f, 0f, 0f);
            var plan = new CombatAiPlan(
                CombatObjective.Search,
                CombatMoveTarget.ForPosition(destination),
                null,
                SkillExecutionContext.None);
            CombatBattleRandom.Initialize(1);
            CombatBattleRandom.SetDecisionTick(owner, 1);

            brain.ExecutePlan(plan);
            brain.ExecutePlan(plan);

            Assert.That(brain.HasBlockedMove, Is.True);
            Assert.That(brain.BlockedMoveDestination, Is.EqualTo(destination));
        }
        finally
        {
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
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                highGroundCandidates: new[] { highGround });
            CombatAiContext occupiedContext = CreatePlannerContext(
                owner,
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
    public void MoveScorer_HighGroundUsesWeaponSeekBias()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            Vector3 highGround = new Vector3(8f, 3f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                highGroundCandidates: new[] { highGround });

            owner.EquipWeapon(new Grimoire(seekHighGroundBias: 0f));
            float unbiasedScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(context, null),
                CombatAiMoveCode.TakeHighGround);

            owner.EquipWeapon(new Grimoire(seekHighGroundBias: 50f));
            float biasedScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(context, null),
                CombatAiMoveCode.TakeHighGround);

            Assert.That(biasedScore - unbiasedScore, Is.EqualTo(20f).Within(0.001f));
        }
        finally
        {
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
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(10f, 0f, 0f),
                assaultRoutes: new[]
                {
                    new CombatAiAssaultRoute(
                        bridgeFeatureIndex: 0,
                        hasBridgeWaypoints: true,
                        enterWorld: new Vector3(0f, 0f, 10f),
                        exitWorld: new Vector3(2f, 0f, 10f)),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            float directScore = FindMoveScore(snapshot, CombatAiMoveCode.AdvanceEnemyStone);
            float bridgeScore = FindMoveScore(snapshot, CombatAiMoveCode.AdvanceViaBridge);

            Assert.That(bridgeScore, Is.GreaterThan(directScore));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_AssaultRouteSoftSplitPenalizesCrowdedRoute()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Sword());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            ally.EquipWeapon(new Sword());
            allyGo.transform.position = new Vector3(-2f, 0f, 0f);

            Vector3 routeAEnter = new Vector3(0f, 0f, 12f);
            Vector3 routeBEnter = new Vector3(12f, 0f, 0f);
            CombatAiAssaultRoute[] routes =
            {
                new CombatAiAssaultRoute(0, true, routeAEnter, new Vector3(0f, 0f, 14f)),
                new CombatAiAssaultRoute(1, true, routeBEnter, new Vector3(14f, 0f, 0f)),
            };
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(
                        ally,
                        true,
                        allyGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.DestroyEnemyStone,
                        hasIntendedDestination: true,
                        intendedDestination: routeAEnter),
                },
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 20f),
                assaultRoutes: routes);

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            float crowdedScore = FindMoveScoreNear(snapshot, CombatAiMoveCode.AdvanceViaBridge, routeAEnter);
            float openScore = FindMoveScoreNear(snapshot, CombatAiMoveCode.AdvanceViaBridge, routeBEnter);

            Assert.That(openScore, Is.GreaterThan(crowdedScore));
            Assert.That(
                FindMoveNear(snapshot, CombatAiMoveCode.AdvanceViaBridge, routeAEnter)
                    .Breakdown.ReasonCodes,
                Does.Contain(CombatAiReasonCode.AssaultRouteCongested));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    private static float FindMoveScoreNear(CombatAiDebugSnapshot snapshot, string code, Vector3 destination)
    {
        return FindMoveNear(snapshot, code, destination).Breakdown.Total;
    }

    private static CombatAiMoveCandidateEntry FindMoveNear(
        CombatAiDebugSnapshot snapshot,
        string code,
        Vector3 destination)
    {
        for (int i = 0; i < snapshot.MoveEntries.Count; i++)
        {
            CombatAiMoveCandidateEntry entry = snapshot.MoveEntries[i];
            if (entry.Code != code || !entry.Target.HasDestination) continue;
            Vector3 delta = entry.Target.Destination - destination;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.01f) return entry;
        }

        Assert.Fail($"移動候補が見つかりません: {code} near {destination}");
        return null;
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
                });

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
    public void Planner_ShieldFollowsAdvancingRangedAttackerInsteadOfFragileSupporter()
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
                enemyIntel: new[] { CreateIntel(enemy, true, enemyGo.transform.position) },
                allyIntel: new[]
                {
                    CreateIntel(
                        frontline,
                        false,
                        default,
                        hasDirectSight: false,
                        hasMemory: false,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy,
                        intendedTarget: enemy,
                        hasIntendedDestination: true,
                        intendedDestination: frontlineDestination),
                    CreateIntel(backline, true, backlineGo.transform.position),
                });

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
            Assert.That(snapshot.SelectedMove.Target.Kind, Is.EqualTo(CombatMoveTargetKind.None));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
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
                highGroundCandidates: new[] { new Vector3(4f, 3f, 0f) });

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
    public void Planner_RangedSearchPrefersHighGroundWhenEnemyInfoIsMissing()
    {
        GameObject ownerGo = new GameObject("WandOwner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());

            CombatAiContext context = CreatePlannerContext(
                owner,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(18f, 0f, 0f),
                highGroundCandidates: new[] { new Vector3(4f, 3f, 0f) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.Search));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo(CombatAiMoveCode.TakeHighGround));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void MoveScorer_HighGroundScoresHigherWhenItAddsActionableTargets()
    {
        GameObject ownerGo = new GameObject("WandOwner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(70f, 0f, 0f);
            CombatCharacterIntel enemyIntel = CreateIntel(
                enemy,
                true,
                enemyGo.transform.position,
                hasDirectSight: false,
                hasMemory: true);

            CombatAiContext usefulContext = CreatePlannerContext(
                owner,
                enemyIntel: new[] { enemyIntel },
                highGroundCandidates: new[] { new Vector3(30f, 4f, 0f) });
            CombatAiContext uselessContext = CreatePlannerContext(
                owner,
                enemyIntel: new[] { enemyIntel },
                highGroundCandidates: new[] { new Vector3(-40f, 4f, 0f) });

            float usefulScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(usefulContext, null),
                CombatAiMoveCode.TakeHighGround);
            float uselessScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(uselessContext, null),
                CombatAiMoveCode.TakeHighGround);

            Assert.That(usefulScore, Is.GreaterThan(uselessScore + 20f));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ScoresEveryHighGroundCandidate()
    {
        GameObject ownerGo = new GameObject("WandOwner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30);
            enemyGo.transform.position = new Vector3(70f, 0f, 0f);
            CombatCharacterIntel enemyIntel = CreateIntel(
                enemy,
                true,
                enemyGo.transform.position,
                hasDirectSight: false,
                hasMemory: true);
            Vector3 nearest = new Vector3(-3f, 3f, 0f);
            Vector3 useful = new Vector3(30f, 4f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                enemyIntel: new[] { enemyIntel },
                highGroundCandidates: new[] { nearest, useful });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry nearestEntry = snapshot.MoveEntries.Find(entry =>
                entry.Code == CombatAiMoveCode.TakeHighGround && entry.Target.Destination == nearest);
            CombatAiMoveCandidateEntry usefulEntry = snapshot.MoveEntries.Find(entry =>
                entry.Code == CombatAiMoveCode.TakeHighGround && entry.Target.Destination == useful);

            Assert.That(nearestEntry, Is.Not.Null);
            Assert.That(usefulEntry, Is.Not.Null);
            Assert.That(usefulEntry.Breakdown.Total, Is.GreaterThan(nearestEntry.Breakdown.Total));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void MoveScorer_HighGroundValuesExpandedSightWhenEnemyLocationIsUncertain()
    {
        GameObject mapSystemGo = new GameObject("CombatMapSystem");
        GameObject ownerGo = new GameObject("WandOwner");
        try
        {
            CombatMapSystem mapSystem = mapSystemGo.AddComponent<CombatMapSystem>();
            var heightMap = new WarSimulation.Combat.Map.HeightMap(4, 4, 1f);
            for (int z = 0; z < 4; z++)
            {
                heightMap.SetHeight(3, z, 10f);
            }
            mapSystem.SetCurrentMap(new WarSimulation.Combat.Map.MapData(
                heightMap,
                new WarSimulation.Combat.Map.GroundStateGrid(4, 4, 1f),
                1));

            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Wand());
            Vector3 flatGround = new Vector3(0f, 0f, 3f);
            Vector3 highGround = new Vector3(3f, 10f, 0f);
            CombatAiContext flatContext = CreatePlannerContext(owner, highGroundCandidates: new[] { flatGround });
            CombatAiContext highContext = CreatePlannerContext(owner, highGroundCandidates: new[] { highGround });

            float flatScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(flatContext, null),
                CombatAiMoveCode.TakeHighGround);
            float highScore = FindMoveScore(
                CombatAiPlanner.BuildDebugSnapshot(highContext, null),
                CombatAiMoveCode.TakeHighGround);

            Assert.That(highScore, Is.GreaterThan(flatScore + 20f));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(mapSystemGo);
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
    public void Planner_ShieldDoesNotCreateHighGroundMoveCandidate()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            CombatAiContext context = CreatePlannerContext(
                owner,
                highGroundCandidates: new[] { new Vector3(3f, 5f, 0f) });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.MoveEntries.Exists(entry => entry.Code == CombatAiMoveCode.TakeHighGround), Is.False);
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldFollowsForwardSwordInsteadOfNearbyWand()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject swordGo = new GameObject("ForwardSword");
        GameObject wandGo = new GameObject("NearbyWand");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character sword = swordGo.AddComponent<Character>();
            sword.Health.Initialize(30);
            sword.EquipWeapon(new Sword());
            Character wand = wandGo.AddComponent<Character>();
            wand.Health.Initialize(30);
            wand.EquipWeapon(new Wand());
            ownerGo.transform.position = new Vector3(1f, 0f, 0f);
            swordGo.transform.position = new Vector3(8f, 0f, 0f);
            wandGo.transform.position = new Vector3(3f, 0f, 0f);
            Vector3 swordDestination = new Vector3(9f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(
                        sword,
                        true,
                        swordGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy,
                        hasIntendedDestination: true,
                        intendedDestination: swordDestination),
                    CreateIntel(
                        wand,
                        true,
                        wandGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy,
                        hasIntendedDestination: true,
                        intendedDestination: new Vector3(4f, 0f, 0f)),
                },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(10f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry move = FindMove(snapshot, CombatAiMoveCode.SupportAlly);

            Assert.That(move.Target.Destination, Is.EqualTo(swordDestination));
        }
        finally
        {
            Object.DestroyImmediate(wandGo);
            Object.DestroyImmediate(swordGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldFollowsAdvancingAttackerInsteadOfAdvancingSupporter()
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
            attacker.EquipWeapon(new Sword());
            Character supporter = supporterGo.AddComponent<Character>();
            supporter.Health.Initialize(30);
            supporter.EquipWeapon(new Bible());
            Vector3 attackerDestination = new Vector3(10f, 0f, 0f);
            Vector3 supporterDestination = new Vector3(0f, 0f, 4f);
            attackerGo.transform.position = new Vector3(8f, 0f, 0f);
            supporterGo.transform.position = new Vector3(0f, 0f, 2f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(
                        attacker,
                        true,
                        attackerGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.AttackEnemy,
                        hasIntendedDestination: true,
                        intendedDestination: attackerDestination),
                    CreateIntel(
                        supporter,
                        true,
                        supporterGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.SupportAlly,
                        hasIntendedDestination: true,
                        intendedDestination: supporterDestination),
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);
            CombatAiMoveCandidateEntry move = FindMove(snapshot, CombatAiMoveCode.SupportAlly);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo(CombatAiMoveCode.SupportAlly));
            Assert.That(move.Target.Destination, Is.EqualTo(attackerDestination));
        }
        finally
        {
            Object.DestroyImmediate(supporterGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldFollowsAllySearchingTowardEnemySide()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject allyGo = new GameObject("SearchingAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            Vector3 allyDestination = new Vector3(8f, 0f, 0f);
            allyGo.transform.position = new Vector3(2f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
                allyIntel: new[]
                {
                    CreateIntel(
                        ally,
                        true,
                        allyGo.transform.position,
                        hasObjective: true,
                        objective: CombatObjective.Search,
                        hasIntendedDestination: true,
                        intendedDestination: allyDestination),
                },
                hasOwnStonePosition: true,
                ownStonePosition: Vector3.zero,
                hasEnemyStonePosition: true,
                enemyStonePosition: new Vector3(20f, 0f, 0f));

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo(CombatAiMoveCode.SupportAlly));
            Assert.That(snapshot.SelectedMove.Target.Destination, Is.EqualTo(allyDestination));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void Planner_ShieldKeepsSupportingAttackerWhenKillableEnemyIsVisible()
    {
        GameObject ownerGo = new GameObject("ShieldOwner");
        GameObject allyGo = new GameObject("Attacker");
        GameObject enemyGo = new GameObject("KillableEnemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            owner.EquipWeapon(new Shield());
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(30);
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            enemy.Health.Initialize(30, 1);
            allyGo.transform.position = new Vector3(2f, 0f, 0f);
            enemyGo.transform.position = new Vector3(3f, 0f, 0f);
            CombatAiContext context = CreatePlannerContext(
                owner,
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
                });

            CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(context, null);

            Assert.That(snapshot.SelectedObjective.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(snapshot.SelectedMove.Code, Is.EqualTo(CombatAiMoveCode.SupportAlly));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
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
                enemyIntel: new[]
                {
                    CreateIntel(enemy, true, enemyGo.transform.position),
                    CreateIntel(secondEnemy, true, secondEnemyGo.transform.position),
                },
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) });
            allyGo.transform.position = new Vector3(8f, 0f, 0f);
            CombatAiContext supported = CreatePlannerContext(
                owner,
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
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                forestCandidates: new[] { forest });
            CombatAiContext occupied = CreatePlannerContext(
                owner,
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
                allyIntel: new[] { CreateIntel(ally, true, allyGo.transform.position) },
                forestCandidates: new[] { distantForest, validForest });

            CombatAiMoveCandidateEntry move = FindMove(
                CombatAiPlanner.BuildDebugSnapshot(context, null),
                CombatAiMoveCode.MoveForest);

            Assert.That(move.Target.Destination, Is.EqualTo(validForest));
            AssertPlanMatchesDebugSnapshot(context, null);
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }
}
