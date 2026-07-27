using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class DesignerComboMetricsTests
{
    [Test]
    public void StandaloneArguments_RoundTripEveryRunSetting()
    {
        var expected = new DesignerComboRunSettings
        {
            Combo = DesignerComboKind.DecoyBombardment,
            RunAllCombos = true,
            DisableRendering = true,
            UseStoneAttackDiagnosticMap = true,
            DiagnosticAttackRoleCount = 3,
            Scope = DesignerComboTestScope.Counter,
            BaseSeed = 34567,
            BattleTimeoutSeconds = 240f,
            TimeScale = 4f,
            OutputDirectory = "/tmp/designer-combo-results",
            QuitWhenFinished = true,
        };

        bool parsed = DesignerComboRunRequest.TryReadCommandLine(
            new[]
            {
                "DesignerComboBenchmark",
                DesignerComboRunRequest.CommandLineArgument,
                DesignerComboRunRequest.Encode(expected),
            },
            out DesignerComboRunSettings actual);

        Assert.That(parsed, Is.True);
        Assert.That(actual.Combo, Is.EqualTo(expected.Combo));
        Assert.That(actual.RunAllCombos, Is.EqualTo(expected.RunAllCombos));
        Assert.That(actual.DisableRendering, Is.EqualTo(expected.DisableRendering));
        Assert.That(actual.UseStoneAttackDiagnosticMap, Is.EqualTo(expected.UseStoneAttackDiagnosticMap));
        Assert.That(actual.DiagnosticAttackRoleCount, Is.EqualTo(expected.DiagnosticAttackRoleCount));
        Assert.That(actual.Scope, Is.EqualTo(expected.Scope));
        Assert.That(actual.BaseSeed, Is.EqualTo(expected.BaseSeed));
        Assert.That(actual.BattleTimeoutSeconds, Is.EqualTo(expected.BattleTimeoutSeconds));
        Assert.That(actual.TimeScale, Is.EqualTo(expected.TimeScale));
        Assert.That(actual.OutputDirectory, Is.EqualTo(expected.OutputDirectory));
        Assert.That(actual.QuitWhenFinished, Is.True);
    }

    [Test]
    public void FlatStoneAttackDiagnosticMap_HasFlatNormalGroundAndTwoMainStones()
    {
        MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
        try
        {
            MapData map = DesignerComboDiagnosticMapFactory.CreateFlatStoneAttackMap(config, 123);

            Assert.That(map.Seed, Is.EqualTo(123));
            Assert.That(map.Features.Count, Is.EqualTo(2));
            Assert.That(map.Features[0].Type, Is.EqualTo(FeatureType.OwnMainStone));
            Assert.That(map.Features[1].Type, Is.EqualTo(FeatureType.EnemyMainStone));
            for (int z = 0; z < map.Height.Height; z++)
            {
                for (int x = 0; x < map.Height.Width; x++)
                {
                    Assert.That(map.Height.GetHeight(x, z), Is.EqualTo(config.BaseHeight));
                    Assert.That(map.GroundStates.GetCell(x, z), Is.EqualTo(GroundState.Normal));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void AsymmetricStoneDefenseMap_ProtectsOnlyOwnMainStoneSideWithRockBarrier()
    {
        MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
        try
        {
            MapData map = DesignerComboDiagnosticMapFactory.CreateAsymmetricStoneDefenseMap(config, 123);
            float worldSize = config.HeightMapResolution * config.HeightMapCellSize;
            int rockCount = 0;

            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature feature = map.Features[i];
                if (feature.Type != FeatureType.Rock) continue;
                rockCount++;
                Assert.That(feature.WorldPosition.z, Is.LessThan(worldSize * 0.5f));
            }

            Assert.That(rockCount, Is.EqualTo(9));
            Assert.That(map.Features, Has.Exactly(1).Matches<PlacedFeature>(
                feature => feature.Type == FeatureType.OwnMainStone));
            Assert.That(map.Features, Has.Exactly(1).Matches<PlacedFeature>(
                feature => feature.Type == FeatureType.EnemyMainStone));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void RunSettings_DefaultBattleTimeoutIsThreeMinutes()
    {
        Assert.That(new DesignerComboRunSettings().BattleTimeoutSeconds, Is.EqualTo(180f));
    }

    [Test]
    public void FiveCharacterDiagnosticBaseline_HasTwoAttackersAndThreeUtilityRoles()
    {
        List<DesignerComboRoleDefinition> roles =
            DesignerComboDiagnosticMapFactory.CreateFiveCharacterBaselineRoles(2);

        Assert.That(roles.Count, Is.EqualTo(5));
        Assert.That(roles[0].Weapon, Is.EqualTo(WeaponKind.Sword));
        Assert.That(roles[1].Weapon, Is.EqualTo(WeaponKind.Wand));
        Assert.That(roles[2].Weapon, Is.EqualTo(WeaponKind.Shield));
        Assert.That(roles[3].Weapon, Is.EqualTo(WeaponKind.Grimoire));
        Assert.That(roles[4].Weapon, Is.EqualTo(WeaponKind.Rosary));
        Assert.That(roles, Has.All.Matches<DesignerComboRoleDefinition>(
            role => role.Personality == CombatAiPersonalityKind.Neutral));
    }

    [Test]
    public void FiveCharacterDiagnosticThreeAttackerVariant_ReplacesDisruptorWithSecondSword()
    {
        List<DesignerComboRoleDefinition> roles =
            DesignerComboDiagnosticMapFactory.CreateFiveCharacterBaselineRoles(3);

        Assert.That(roles.Count, Is.EqualTo(5));
        Assert.That(roles[0].Weapon, Is.EqualTo(WeaponKind.Sword));
        Assert.That(roles[1].Weapon, Is.EqualTo(WeaponKind.Wand));
        Assert.That(roles[2].Weapon, Is.EqualTo(WeaponKind.Sword));
        Assert.That(roles[3].Weapon, Is.EqualTo(WeaponKind.Shield));
        Assert.That(roles[4].Weapon, Is.EqualTo(WeaponKind.Rosary));
        Assert.That(roles, Has.All.Matches<DesignerComboRoleDefinition>(
            role => role.Personality == CombatAiPersonalityKind.Neutral));
    }

    [Test]
    public void RuntimeIdentityConfiguration_SetsGenderAndLover()
    {
        CharacterData owner = ScriptableObject.CreateInstance<CharacterData>();
        CharacterData lover = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            owner.ConfigureIdentity(CharacterGender.Female, lover);

            Assert.That(owner.Gender, Is.EqualTo(CharacterGender.Female));
            Assert.That(owner.Lover, Is.SameAs(lover));
        }
        finally
        {
            Object.DestroyImmediate(lover);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void RuntimeFeatureCountConfiguration_ClampsAndAppliesCounts()
    {
        MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
        try
        {
            config.ConfigureFeatureCounts(6, 50, 2, -1, 1);

            Assert.That(config.ForestClusterCount, Is.EqualTo(6));
            Assert.That(config.ScatterTreeCount, Is.EqualTo(50));
            Assert.That(config.CrossMapRiverCount, Is.EqualTo(2));
            Assert.That(config.LakeCount, Is.Zero);
            Assert.That(config.BridgesPerRiver, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void CharacterPoolOrder_FollowsHierarchyInsteadOfDiscoveryOrder()
    {
        GameObject firstObject = new GameObject("First");
        GameObject secondObject = new GameObject("Second");
        try
        {
            Character first = firstObject.AddComponent<Character>();
            Character second = secondObject.AddComponent<Character>();
            var characters = new List<Character> { second, first };
            MethodInfo compare = typeof(DesignerComboBenchmarkRunner).GetMethod(
                "CompareCharactersByHierarchy",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(compare, Is.Not.Null);
            characters.Sort((left, right) => (int)compare.Invoke(null, new object[] { left, right }));

            Assert.That(characters, Is.EqualTo(new[] { first, second }));
        }
        finally
        {
            Object.DestroyImmediate(secondObject);
            Object.DestroyImmediate(firstObject);
        }
    }

    [TestCase(0.85f, false)]
    [TestCase(1.15f, true)]
    public void IsRelationshipAbilityIncrease_DistinguishesDebuffFromBuff(float multiplier, bool expected)
    {
        var snapshot = new CombatStatusEffectSnapshot(
            "PersonalityRelationship_STR",
            CombatStatusEffects.EffectType.StatModifier,
            CombatStatusEffects.StatKind.STR,
            multiplier,
            0f,
            0f,
            1f);

        Assert.That(DesignerComboMetricRules.IsRelationshipAbilityIncrease(snapshot), Is.EqualTo(expected));
    }

    [Test]
    public void ScenarioCatalog_ContainsEveryDocumentedFormation()
    {
        DesignerComboScenarioDefinition magicStone = DesignerComboScenarioCatalog.Get(DesignerComboKind.MagicStoneAssaultRosary);
        DesignerComboScenarioDefinition breakthrough = DesignerComboScenarioCatalog.Get(DesignerComboKind.FrontlineBreakthroughBible);
        DesignerComboScenarioDefinition escort = DesignerComboScenarioCatalog.Get(DesignerComboKind.OppositeGenderEscortBible);

        Assert.That(magicStone.Roles[1].Weapon, Is.EqualTo(WeaponKind.Rosary));
        Assert.That(magicStone.Roles[1].Personality, Is.EqualTo(CombatAiPersonalityKind.Lonely));
        Assert.That(breakthrough.Roles[1].Weapon, Is.EqualTo(WeaponKind.Bible));
        Assert.That(breakthrough.Roles[1].Personality, Is.EqualTo(CombatAiPersonalityKind.Lonely));
        Assert.That(escort.Roles[1].Weapon, Is.EqualTo(WeaponKind.Bible));
        Assert.That(escort.Roles[1].Personality, Is.EqualTo(CombatAiPersonalityKind.Lecherous));
    }

    [TestCase(DesignerComboTestScope.BehaviorCheck, 5)]
    [TestCase(DesignerComboTestScope.Comparison, 240)]
    [TestCase(DesignerComboTestScope.ExtendedComparison, 800)]
    [TestCase(DesignerComboTestScope.Counter, 120)]
    public void EstimateMatchCount_ReturnsPlannedMatchesForTwoRoleCombo(DesignerComboTestScope scope, int expected)
    {
        DesignerComboScenarioDefinition scenario = DesignerComboScenarioCatalog.Get(DesignerComboKind.BindFollowUp);

        int count = DesignerComboBenchmarkRunner.EstimateMatchCount(scenario, scope);

        Assert.That(count, Is.EqualTo(expected));
    }

    [TestCase(DesignerComboTestScope.BehaviorCheck, DesignerComboTerrainKind.Open)]
    [TestCase(DesignerComboTestScope.Comparison, DesignerComboTerrainKind.Production)]
    [TestCase(DesignerComboTestScope.ExtendedComparison, DesignerComboTerrainKind.Production)]
    [TestCase(DesignerComboTestScope.AddedMembers, DesignerComboTerrainKind.Production)]
    [TestCase(DesignerComboTestScope.Counter, DesignerComboTerrainKind.Production)]
    public void TerrainForScope_UsesProductionSettingsWheneverBattleResultsAreCompared(
        DesignerComboTestScope scope,
        DesignerComboTerrainKind expected)
    {
        Assert.That(DesignerComboBenchmarkRunner.GetTerrainForScope(scope), Is.EqualTo(expected));
    }

    [Test]
    public void EstimateMatchCount_ExcludesAddedMembersWhenScalableRoleIsUndefined()
    {
        DesignerComboScenarioDefinition scenario = DesignerComboScenarioCatalog.Get(DesignerComboKind.BindFollowUp);

        int count = DesignerComboBenchmarkRunner.EstimateMatchCount(scenario, DesignerComboTestScope.AddedMembers);

        Assert.That(count, Is.Zero);
    }

    [Test]
    public void BuildSummaries_AggregatesOnlyValidMatches()
    {
        var results = new List<DesignerComboMatchResult>
        {
            Match("連携あり", 10f, "勝利"),
            Match("連携あり", 20f, "敗北"),
            Match("連携あり", 1000f, "勝利", error: "失敗"),
        };

        List<DesignerComboReportWriter.Summary> summaries = DesignerComboReportWriter.BuildSummaries(results);

        Assert.That(summaries, Has.Count.EqualTo(1));
        Assert.That(summaries[0].Matches, Is.EqualTo(3));
        Assert.That(summaries[0].Failures, Is.EqualTo(1));
        Assert.That(summaries[0].AveragePrimaryMetric, Is.EqualTo(15f));
        Assert.That(summaries[0].WinRate, Is.EqualTo(0.5f));
    }

    [Test]
    public void BuildPairedComparisons_PairsOnlySameTerrainSeedAndSide()
    {
        var results = new List<DesignerComboMatchResult>
        {
            Match("連携あり", 120f, "勝利", seed: 1),
            Match("片側解除:支援役", 100f, "敗北", seed: 1),
            Match("連携あり", 80f, "敗北", seed: 2),
            Match("片側解除:支援役", 500f, "勝利", seed: 3),
        };

        List<DesignerComboReportWriter.PairedComparison> comparisons = DesignerComboReportWriter.BuildPairedComparisons(
            DesignerComboTestScope.Comparison,
            results);

        Assert.That(comparisons, Has.Count.EqualTo(1));
        Assert.That(comparisons[0].Pairs, Is.EqualTo(1));
        Assert.That(comparisons[0].AverageBaselineMetric, Is.EqualTo(120f));
        Assert.That(comparisons[0].AverageComparisonMetric, Is.EqualTo(100f));
        Assert.That(comparisons[0].AverageDifference, Is.EqualTo(20f));
        Assert.That(comparisons[0].AverageWinDifference, Is.EqualTo(1f));
    }

    [Test]
    public void BuildEvaluations_RequiresFifteenPercentDropAfterRequiredRoleLoss()
    {
        var results = new List<DesignerComboMatchResult>
        {
            BrokenMatch(requiredRoleLost: false, before: 10f, after: 1f),
            BrokenMatch(requiredRoleLost: true, before: 10f, after: 9f),
        };

        List<string> evaluations = DesignerComboReportWriter.BuildEvaluations(
            DesignerComboTestScope.BehaviorCheck,
            new List<DesignerComboReportWriter.Summary>(),
            new List<DesignerComboReportWriter.PairedComparison>(),
            results);

        Assert.That(evaluations, Has.Count.EqualTo(1));
        Assert.That(evaluations[0], Does.StartWith("不合格:"));
        Assert.That(evaluations[0], Does.Contain("15%以上"));
    }

    [Test]
    public void BuildEvaluations_PassesClearDropAfterRequiredRoleLoss()
    {
        var results = new List<DesignerComboMatchResult>
        {
            BrokenMatch(requiredRoleLost: true, before: 10f, after: 8.5f),
        };

        List<string> evaluations = DesignerComboReportWriter.BuildEvaluations(
            DesignerComboTestScope.BehaviorCheck,
            new List<DesignerComboReportWriter.Summary>(),
            new List<DesignerComboReportWriter.PairedComparison>(),
            results);

        Assert.That(evaluations[0], Does.StartWith("合格:"));
    }

    [Test]
    public void BuildEvaluations_UsesPairedMetricsForComparisonThreshold()
    {
        var results = new List<DesignerComboMatchResult>
        {
            Match("連携あり", 115f, "勝利", seed: 1),
            Match("片側解除:支援役", 100f, "勝利", seed: 1),
        };
        List<DesignerComboReportWriter.Summary> summaries = DesignerComboReportWriter.BuildSummaries(results);
        List<DesignerComboReportWriter.PairedComparison> comparisons = DesignerComboReportWriter.BuildPairedComparisons(
            DesignerComboTestScope.Comparison,
            results);

        List<string> evaluations = DesignerComboReportWriter.BuildEvaluations(
            DesignerComboTestScope.Comparison,
            summaries,
            comparisons,
            results);

        Assert.That(evaluations, Has.Member("合格: 主指標が片側解除:支援役より15%以上高い"));
        Assert.That(evaluations, Has.Member("合格: 勝率が片側解除:支援役より10ポイント以上低くない"));
    }

    private static DesignerComboMatchResult Match(
        string variant,
        float metric,
        string outcome,
        int seed = 1,
        string error = null)
    {
        return new DesignerComboMatchResult
        {
            Variant = variant,
            Terrain = DesignerComboTerrainKind.Open.ToString(),
            Seed = seed,
            Outcome = outcome,
            PrimaryMetric = metric,
            PrimaryMetricPerSecond = metric / 10f,
            ComboOccurred = true,
            Error = error,
        };
    }

    private static DesignerComboMatchResult BrokenMatch(bool requiredRoleLost, float before, float after)
    {
        return new DesignerComboMatchResult
        {
            IsLinkedVariant = true,
            ComboBrokenAt = 5f,
            RequiredRoleLost = requiredRoleLost,
            RequiredRoleLostAt = requiredRoleLost ? 5f : -1f,
            MetricRateBeforeBreak = before,
            MetricRateAfterBreak = after,
        };
    }

}
