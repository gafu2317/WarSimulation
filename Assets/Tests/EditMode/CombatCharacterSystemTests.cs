using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

public sealed class CombatCharacterSystemTests
{
    [Test]
    public void MagicStoneDamage_MarksTheAttackerForTheDefendingTeam()
    {
        GameObject stoneSystemObject = new GameObject("MagicStoneSystem");
        GameObject characterSystemObject = new GameObject("CharacterSystem");
        GameObject allyObject = new GameObject("Ally");
        GameObject enemyObject = new GameObject("Enemy");
        try
        {
            CombatMagicStoneSystem stoneSystem = stoneSystemObject.AddComponent<CombatMagicStoneSystem>();
            stoneSystem.Initialize(CreateStoneTestMap());
            CombatCharacterSystem characterSystem = characterSystemObject.AddComponent<CombatCharacterSystem>();
            Character ally = allyObject.AddComponent<Character>();
            Character enemy = enemyObject.AddComponent<Character>();
            ally.Health.Initialize(30);
            enemy.Health.Initialize(30);
            characterSystem.SetParticipants(new[] { ally }, new[] { enemy });
            characterSystem.ResetCharactersForBattle();

            stoneSystem.TakeDamage(0, 1, enemy);

            Assert.That(characterSystem.GetMarkedStoneAttacker(ally), Is.SameAs(enemy));
            Assert.That(characterSystem.GetMarkedStoneAttacker(enemy), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(characterSystemObject);
            Object.DestroyImmediate(stoneSystemObject);
        }
    }

    [Test]
    public void GenerateCandidates_CreatesTenCharactersForEachTeamFromPrefab()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        GameObject mapObject = new GameObject("MapSystem");
        GameObject prefabObject = new GameObject("CharacterPrefab");
        try
        {
            Character prefab = prefabObject.AddComponent<Character>();
            prefabObject.AddComponent<CombatAiBrain>();
            CombatMapSystem mapSystem = mapObject.AddComponent<CombatMapSystem>();
            mapSystem.SetCurrentMap(CreateStoneTestMap());
            CombatEditModeTestUtil.SetPrivateField(mapSystem, "_isNavMeshReady", true);
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            CombatEditModeTestUtil.WireMapSystem(system, mapSystem);
            var serializedSystem = new UnityEditor.SerializedObject(system);
            serializedSystem.FindProperty("_generateCandidatesAtRuntime").boolValue = true;
            serializedSystem.FindProperty("_characterPrefab").objectReferenceValue = prefab;
            serializedSystem.ApplyModifiedPropertiesWithoutUndo();
            MethodInfo awake = typeof(CombatCharacterSystem).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(system, null);

            Assert.That(system.AllyCharacters, Has.Count.EqualTo(10));
            Assert.That(system.EnemyCharacters, Has.Count.EqualTo(10));
            Assert.That(system.AllyCharacters, Has.All.Matches<Character>(character =>
                character.Team == CombatTeam.Ally && character.GetComponent<CombatAiBrain>() != null));
            Assert.That(system.EnemyCharacters, Has.All.Matches<Character>(character =>
                character.Team == CombatTeam.Enemy && character.GetComponent<CombatAiBrain>() != null));
            Assert.That(
                system.AllyCharacters.ConvertAll(character => character.DisplayName),
                Is.EqualTo(new[] { "砂狼シロコ", "小鳥遊ホシノ", "陸八魔アル", "空崎ヒナ", "浅黄ムツキ", "黒見セリカ", "十六夜ノノミ", "奥空アヤネ", "聖園ミカ", "早瀬ユウカ" }));
            Assert.That(
                system.EnemyCharacters.ConvertAll(character => character.DisplayName),
                Is.EqualTo(new[] { "杏山カズサ", "才羽モモイ", "才羽ミドリ", "天雨アコ", "銀鏡イオリ", "火宮チナツ", "愛清フウカ", "棗イロハ", "下江コハル", "浦和ハナコ" }));
            Assert.That(system.transform.Find("GeneratedCombatCharacters"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(prefabObject);
            Object.DestroyImmediate(mapObject);
            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void TickAiDecisionsNow_PlansEveryParticipantBeforeExecutingInParticipantOrder()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        var characterObjects = new List<GameObject>();
        var selectedParticipantIds = new List<int>();

        try
        {
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            Character allyA = CreateAiCharacter("AllyA", characterObjects);
            Character allyB = CreateAiCharacter("AllyB", characterObjects);
            Character enemyA = CreateAiCharacter("EnemyA", characterObjects);
            Character enemyB = CreateAiCharacter("EnemyB", characterObjects);
            system.SetParticipants(
                new[] { allyB, allyA },
                new[] { enemyB, enemyA });
            system.ResetCharactersForBattle();

            void Capture(Character owner, CombatAiPlan _, CombatAiPlan __)
            {
                selectedParticipantIds.Add(owner.BattleParticipantId);
            }

            CombatAiDecisionEvents.PlanSelected += Capture;
            try
            {
                int preparedCount = system.TickAiDecisionsNow(Time.time);

                Assert.That(preparedCount, Is.EqualTo(4));
            }
            finally
            {
                CombatAiDecisionEvents.PlanSelected -= Capture;
            }

            Assert.That(selectedParticipantIds, Is.EqualTo(new[] { 1, 2, -1, -2 }));
            Assert.That(allyA.GetComponent<CombatAiBrain>().LastContext.AllyIntel[0].HasObjective, Is.False);
            Assert.That(allyB.GetComponent<CombatAiBrain>().LastContext.AllyIntel[0].HasObjective, Is.True);
            Assert.That(enemyA.GetComponent<CombatAiBrain>().LastContext.AllyIntel[0].HasObjective, Is.False);
            Assert.That(enemyB.GetComponent<CombatAiBrain>().LastContext.AllyIntel[0].HasObjective, Is.True);
        }
        finally
        {
            for (int i = characterObjects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(characterObjects[i]);
            }

            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void TickAiDecisionsNow_RefreshesTheCachedParticipantsWhenTheRosterChanges()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        var characterObjects = new List<GameObject>();
        var selectedParticipantIds = new List<int>();

        try
        {
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            Character allyA = CreateAiCharacter("AllyA", characterObjects);
            Character enemy = CreateAiCharacter("Enemy", characterObjects);
            system.SetParticipants(new[] { allyA }, new[] { enemy });
            system.ResetCharactersForBattle();
            system.TickAiDecisionsNow(Time.time);

            allyA.GetComponent<CombatAiBrain>().enabled = false;
            Character allyB = CreateAiCharacter("AllyB", characterObjects);
            system.AllyCharacters.Add(allyB);
            system.AssignTeamsFromLists();

            void Capture(Character owner, CombatAiPlan _, CombatAiPlan __)
            {
                selectedParticipantIds.Add(owner.BattleParticipantId);
            }

            CombatAiDecisionEvents.PlanSelected += Capture;
            try
            {
                int preparedCount = system.TickAiDecisionsNow(Time.time + 0.5f);

                Assert.That(preparedCount, Is.EqualTo(2));
            }
            finally
            {
                CombatAiDecisionEvents.PlanSelected -= Capture;
            }

            Assert.That(selectedParticipantIds, Is.EqualTo(new[] { 2, -1 }));
        }
        finally
        {
            for (int i = characterObjects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(characterObjects[i]);
            }

            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void SetParticipants_AppliesEachCharactersWeaponAndPersonalitySetup()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        GameObject allyObject = new GameObject("Ally");
        GameObject enemyObject = new GameObject("Enemy");
        WeaponConfig allyWeapon = ScriptableObject.CreateInstance<WeaponConfig>();
        WeaponConfig enemyWeapon = ScriptableObject.CreateInstance<WeaponConfig>();
        CombatAiPersonalityProfile allyPersonality = ScriptableObject.CreateInstance<CombatAiPersonalityProfile>();
        CombatAiPersonalityProfile enemyPersonality = ScriptableObject.CreateInstance<CombatAiPersonalityProfile>();

        try
        {
            allyWeapon.ApplyKindDefaults(WeaponKind.Sword);
            enemyWeapon.ApplyKindDefaults(WeaponKind.Wand);
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            Character ally = allyObject.AddComponent<Character>();
            Character enemy = enemyObject.AddComponent<Character>();

            system.SetParticipants(
                new[] { new CombatParticipantSetup(ally, allyWeapon, allyPersonality) },
                new[] { new CombatParticipantSetup(enemy, enemyWeapon, enemyPersonality) });

            Assert.That(ally.EquippedWeaponConfig, Is.SameAs(allyWeapon));
            Assert.That(ally.EquippedWeapon.Kind, Is.EqualTo(WeaponKind.Sword));
            Assert.That(ally.PersonalityProfile, Is.SameAs(allyPersonality));
            Assert.That(ally.Team, Is.EqualTo(CombatTeam.Ally));
            Assert.That(enemy.EquippedWeaponConfig, Is.SameAs(enemyWeapon));
            Assert.That(enemy.EquippedWeapon.Kind, Is.EqualTo(WeaponKind.Wand));
            Assert.That(enemy.PersonalityProfile, Is.SameAs(enemyPersonality));
            Assert.That(enemy.Team, Is.EqualTo(CombatTeam.Enemy));

            ally.InitializeOnBattleStart();
            enemy.InitializeOnBattleStart();

            Assert.That(ally.EquippedWeaponConfig, Is.SameAs(allyWeapon));
            Assert.That(enemy.EquippedWeaponConfig, Is.SameAs(enemyWeapon));
        }
        finally
        {
            Object.DestroyImmediate(enemyPersonality);
            Object.DestroyImmediate(allyPersonality);
            Object.DestroyImmediate(enemyWeapon);
            Object.DestroyImmediate(allyWeapon);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void SetParticipants_AppliesStatAdjustmentsWithoutChangingBaseStats()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        GameObject allyObject = new GameObject("Ally");
        WeaponConfig weapon = ScriptableObject.CreateInstance<WeaponConfig>();

        try
        {
            weapon.ApplyKindDefaults(WeaponKind.Sword);
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            Character ally = allyObject.AddComponent<Character>();
            typeof(Character).GetProperty("STR").SetValue(ally, 20);
            typeof(Character).GetProperty("INT").SetValue(ally, 20);
            typeof(Character).GetProperty("FAI").SetValue(ally, 20);
            typeof(Character).GetProperty("AGI").SetValue(ally, 20);
            var adjustments = new Dictionary<CombatStat, int>
            {
                [CombatStat.STR] = 3,
                [CombatStat.INT] = 4,
                [CombatStat.FAI] = 5,
                [CombatStat.AGI] = 6,
            };
            CombatParticipantSetup setup = new CombatParticipantSetup(
                ally,
                weapon,
                null,
                statAdjustments: adjustments);

            adjustments[CombatStat.STR] = 10;
            system.SetParticipants(
                new[] { setup },
                System.Array.Empty<CombatParticipantSetup>());

            Assert.That(setup.StatAdjustments[CombatStat.STR], Is.EqualTo(3));
            Assert.That(setup.StatAdjustments[CombatStat.INT], Is.EqualTo(4));
            Assert.That(setup.StatAdjustments[CombatStat.FAI], Is.EqualTo(5));
            Assert.That(setup.StatAdjustments[CombatStat.AGI], Is.EqualTo(6));
            Assert.That(ally.STR, Is.EqualTo(20));
            Assert.That(ally.INT, Is.EqualTo(20));
            Assert.That(ally.FAI, Is.EqualTo(20));
            Assert.That(ally.AGI, Is.EqualTo(20));
            Assert.That(ally.GetEffectiveStat(CombatStat.STR), Is.EqualTo(35f));
            Assert.That(ally.GetEffectiveStat(CombatStat.INT), Is.EqualTo(24f));
            Assert.That(ally.GetEffectiveStat(CombatStat.FAI), Is.EqualTo(25f));
            Assert.That(ally.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(26f));

            ally.ConfigureForBattle(
                weapon,
                null,
                statAdjustments: new Dictionary<CombatStat, int>
                {
                    [CombatStat.STR] = -100,
                });
            Assert.That(ally.GetEffectiveStat(CombatStat.STR), Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void SetParticipants_AppliesAdjustedAgiToMovementSpeed()
    {
        GameObject characterObject = new GameObject("Character");

        try
        {
            Character character = characterObject.AddComponent<Character>();
            NavMeshAgent agent = characterObject.GetComponent<NavMeshAgent>();
            CombatCharacterBody body = characterObject.GetComponent<CombatCharacterBody>();
            typeof(CombatCharacterBody).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(body, null);
            typeof(Character).GetProperty("AGI").SetValue(character, 30);
            body.BaseSpeed = 4f;

            character.ConfigureForBattle(
                null,
                null,
                statAdjustments: new Dictionary<CombatStat, int>
                {
                    [CombatStat.AGI] = 10,
                });

            Assert.That(agent.speed, Is.EqualTo(4f * 40f / 30f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void CombatCharacterBody_ResolvesRecklessRiverCostAndRestoresItForNeutral()
    {
        GameObject characterObject = new GameObject("Character");
        CombatAiPersonalityProfile reckless = CombatAiPersonalityProfile.CreateBuiltInProfile(
            CombatAiPersonalityKind.Reckless);
        try
        {
            Character character = characterObject.AddComponent<Character>();
            CombatCharacterBody body = characterObject.GetComponent<CombatCharacterBody>();
            typeof(CombatCharacterBody).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(body, null);
            MethodInfo resolveCost = typeof(CombatCharacterBody).GetMethod(
                "ResolveRiverNavigationCost",
                BindingFlags.Instance | BindingFlags.NonPublic);
            int riverArea = NavMesh.GetAreaFromName("River");
            int walkableArea = NavMesh.GetAreaFromName("Walkable");

            Assert.That(riverArea, Is.GreaterThanOrEqualTo(0));
            Assert.That(walkableArea, Is.GreaterThanOrEqualTo(0));
            character.ConfigureForBattle(null, reckless);
            float recklessCost = (float)resolveCost.Invoke(body, new object[] { riverArea });
            Assert.That(recklessCost, Is.EqualTo(NavMesh.GetAreaCost(walkableArea)).Within(0.001f));

            character.ConfigureForBattle(null, null);
            float neutralCost = (float)resolveCost.Invoke(body, new object[] { riverArea });
            Assert.That(neutralCost, Is.EqualTo(NavMesh.GetAreaCost(riverArea)).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(reckless);
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void SetParticipants_AppliesAndClearsTagalongTarget()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        GameObject allyObject = new GameObject("Ally");
        GameObject targetObject = new GameObject("Target");
        CombatAiPersonalityProfile tagalong = CombatAiPersonalityProfile.CreateBuiltInProfile(
            CombatAiPersonalityKind.Tagalong);

        try
        {
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            Character ally = allyObject.AddComponent<Character>();
            Character target = targetObject.AddComponent<Character>();

            system.SetParticipants(
                new[]
                {
                    new CombatParticipantSetup(
                        ally,
                        null,
                        tagalong,
                        tagalongTarget: target),
                    new CombatParticipantSetup(target, null, null),
                },
                System.Array.Empty<CombatParticipantSetup>());

            Assert.That(ally.TagalongTarget, Is.SameAs(target));

            system.SetParticipants(
                new[] { new CombatParticipantSetup(ally, null, null) },
                System.Array.Empty<CombatParticipantSetup>());

            Assert.That(ally.TagalongTarget, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(tagalong);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void SetParticipants_RegistersSelectedCharactersAndDisablesOthers()
    {
        GameObject systemObject = new GameObject("CharacterSystem");
        GameObject selectedObject = new GameObject("SelectedAlly");
        GameObject unselectedObject = new GameObject("UnselectedAlly");
        GameObject enemyObject = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            Character selected = selectedObject.AddComponent<Character>();
            Character unselected = unselectedObject.AddComponent<Character>();
            Character enemy = enemyObject.AddComponent<Character>();
            system.AllyCharacters.Add(selected);
            system.AllyCharacters.Add(unselected);
            system.EnemyCharacters.Add(enemy);

            system.SetParticipants(new List<Character> { selected }, new List<Character> { enemy });

            Assert.That(system.AllyCharacters, Is.EqualTo(new[] { selected }));
            Assert.That(system.EnemyCharacters, Is.EqualTo(new[] { enemy }));
            Assert.That(selected.Team, Is.EqualTo(CombatTeam.Ally));
            Assert.That(enemy.Team, Is.EqualTo(CombatTeam.Enemy));
            Assert.That(selectedObject.activeSelf, Is.True);
            Assert.That(enemyObject.activeSelf, Is.True);
            Assert.That(unselectedObject.activeSelf, Is.False);

            system.SetParticipants(
                new List<Character> { selected, unselected },
                new List<Character> { enemy });

            Assert.That(unselectedObject.activeSelf, Is.True);
            Assert.That(system.AllyCharacters, Does.Contain(unselected));
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(unselectedObject);
            Object.DestroyImmediate(selectedObject);
            Object.DestroyImmediate(systemObject);
        }
    }

    [Test]
    public void TryRelocateCharactersNearMainStones_MovesTeamsNearOwnStoneWithoutFeatureOverlap()
    {
        GameObject mapObject = new GameObject("MapSystem");
        GameObject characterSystemObject = new GameObject("CharacterSystem");
        GameObject allyObjectA = new GameObject("AllyA");
        GameObject allyObjectB = new GameObject("AllyB");
        GameObject enemyObjectA = new GameObject("EnemyA");
        GameObject enemyObjectB = new GameObject("EnemyB");
        AuthoredMapDefinition definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        BakedMapData bakedMap = ScriptableObject.CreateInstance<BakedMapData>();

        try
        {
            CombatMapSystem mapSystem = mapObject.AddComponent<CombatMapSystem>();
            MapData map = CreateStoneTestMap();
            bakedMap.Capture(map, definition.ComputeBakeFingerprint());
            definition.SetBakedMapData(bakedMap);
            CombatEditModeTestUtil.SetPrivateField(mapSystem, "_authoredMap", definition);
            mapSystem.SetCurrentMap(map);

            CombatCharacterSystem characterSystem = characterSystemObject.AddComponent<CombatCharacterSystem>();
            CombatEditModeTestUtil.WireMapSystem(characterSystem, mapSystem);

            Character allyA = allyObjectA.AddComponent<Character>();
            Character allyB = allyObjectB.AddComponent<Character>();
            Character enemyA = enemyObjectA.AddComponent<Character>();
            Character enemyB = enemyObjectB.AddComponent<Character>();
            allyA.SetTeam(CombatTeam.Ally);
            allyB.SetTeam(CombatTeam.Ally);
            enemyA.SetTeam(CombatTeam.Enemy);
            enemyB.SetTeam(CombatTeam.Enemy);
            characterSystem.AllyCharacters.Add(allyA);
            characterSystem.AllyCharacters.Add(allyB);
            characterSystem.EnemyCharacters.Add(enemyA);
            characterSystem.EnemyCharacters.Add(enemyB);

            bool moved = characterSystem.TryRelocateCharactersNearMainStones();

            Assert.That(moved, Is.True);
            Assert.That(HorizontalDistance(allyA.transform.position, new Vector3(2.5f, 0f, 1.5f)), Is.LessThan(6f));
            Assert.That(HorizontalDistance(allyB.transform.position, new Vector3(2.5f, 0f, 1.5f)), Is.LessThan(6f));
            Assert.That(HorizontalDistance(enemyA.transform.position, new Vector3(5.5f, 0f, 6.5f)), Is.LessThan(6f));
            Assert.That(HorizontalDistance(enemyB.transform.position, new Vector3(5.5f, 0f, 6.5f)), Is.LessThan(6f));

            Assert.That(HorizontalDistance(allyA.transform.position, allyB.transform.position), Is.GreaterThanOrEqualTo(1.5f));
            Assert.That(HorizontalDistance(enemyA.transform.position, enemyB.transform.position), Is.GreaterThanOrEqualTo(1.5f));

            AssertValidTerrain(mapSystem, allyA.transform.position);
            AssertValidTerrain(mapSystem, allyB.transform.position);
            AssertValidTerrain(mapSystem, enemyA.transform.position);
            AssertValidTerrain(mapSystem, enemyB.transform.position);
            AssertClearOfSolidFeatures(mapSystem.CurrentMap, allyA.transform.position);
            AssertClearOfSolidFeatures(mapSystem.CurrentMap, allyB.transform.position);
            AssertClearOfSolidFeatures(mapSystem.CurrentMap, enemyA.transform.position);
            AssertClearOfSolidFeatures(mapSystem.CurrentMap, enemyB.transform.position);
        }
        finally
        {
            Object.DestroyImmediate(bakedMap);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(enemyObjectB);
            Object.DestroyImmediate(enemyObjectA);
            Object.DestroyImmediate(allyObjectB);
            Object.DestroyImmediate(allyObjectA);
            Object.DestroyImmediate(characterSystemObject);
            Object.DestroyImmediate(mapObject);
        }
    }

    [Test]
    public void RenderedRock_UsesNotWalkableNavMeshArea()
    {
        GameObject mapObject = new GameObject("Map");

        try
        {
            var map = new MapData(new HeightMap(1, 1, 1f), new GroundStateGrid(1, 1, 1f), seed: 1);
            map.AddFeature(new PlacedFeature(FeatureType.Rock, Vector3.zero));

            FeatureRenderer renderer = mapObject.AddComponent<FeatureRenderer>();
            renderer.Render(map);

            Transform rock = mapObject.transform.Find("GeneratedFeatures/Rock_0");
            Assert.That(rock, Is.Not.Null);
            NavMeshModifier modifier = rock.GetComponent<NavMeshModifier>();

            Assert.That(modifier, Is.Not.Null);
            Assert.That(modifier.overrideArea, Is.True);
            Assert.That(modifier.area, Is.EqualTo(NavMesh.GetAreaFromName("Not Walkable")));
        }
        finally
        {
            Object.DestroyImmediate(mapObject);
        }
    }

    private static MapData CreateStoneTestMap()
    {
        var height = new HeightMap(8, 8, 1f);
        var ground = new GroundStateGrid(8, 8, 1f);

        for (int z = 0; z < 8; z++)
        {
            for (int x = 0; x < 8; x++)
            {
                height.SetHeight(x, z, 0f);
            }
        }

        ground.SetCell(0, 0, GroundState.Water);
        height.CliffFaces.MarkCliff(1, 0);
        ground.SetCell(0, 7, GroundState.Water);
        height.CliffFaces.MarkCliff(1, 7);

        var map = new MapData(height, ground, seed: 7);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(2.5f, 0f, 1.5f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(5.5f, 0f, 6.5f)));
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(3.5f, 0f, 1.5f)));
        map.AddFeature(new PlacedFeature(FeatureType.Tree, new Vector3(4.5f, 0f, 6.5f)));
        return map;
    }

    private static Character CreateAiCharacter(string name, List<GameObject> characterObjects)
    {
        GameObject characterObject = new GameObject(name);
        characterObjects.Add(characterObject);
        Character character = characterObject.AddComponent<Character>();
        character.Health.Initialize(100);
        characterObject.AddComponent<CombatAiBrain>();
        return character;
    }

    private static void AssertClearOfSolidFeatures(MapData map, Vector3 position)
    {
        for (int i = 0; i < map.Features.Count; i++)
        {
            PlacedFeature feature = map.Features[i];
            if (feature.Type == FeatureType.Bridge) continue;

            Assert.That(HorizontalDistance(position, feature.WorldPosition), Is.GreaterThanOrEqualTo(3f));
        }
    }

    private static void AssertValidTerrain(CombatMapSystem mapSystem, Vector3 position)
    {
        TerrainInfo terrain = mapSystem.GetTerrainInfo(position);
        Assert.That(terrain.GroundState, Is.Not.EqualTo(GroundState.Water));
        Assert.That(terrain.IsCliffFace, Is.False);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

}
