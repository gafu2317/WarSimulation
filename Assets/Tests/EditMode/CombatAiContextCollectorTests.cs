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
        IReadOnlyList<Vector3> highGroundCandidates = null)
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
            System.Array.Empty<Vector3>(),
            highGroundCandidates ?? System.Array.Empty<Vector3>(),
            System.Array.Empty<Vector3>());
    }

    private static CombatCharacterIntel CreateIntel(
        Character character,
        bool hasKnownPosition,
        Vector3 knownPosition,
        bool hasDirectSight = true,
        bool hasMemory = false)
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
            statusEffects: System.Array.Empty<CombatStatusEffectSnapshot>(),
            hasObjective: false,
            objective: default);
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
