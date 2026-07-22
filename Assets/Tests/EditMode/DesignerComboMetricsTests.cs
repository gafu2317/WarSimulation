using System.Collections.Generic;
using NUnit.Framework;

public sealed class DesignerComboMetricsTests
{
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
