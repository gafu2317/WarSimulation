using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatCompositionSweepGeneratorTests
{
    [Test]
    public void Generate_RespectsPartySizeAndWeaponConstraints()
    {
        var config = new CombatCompositionSweepConfig
        {
            CandidateCount = 30,
            BaseSeed = 42,
            MinPartySize = 4,
            MaxPartySize = 6,
        };

        List<CombatCompositionCandidate> candidates = CombatCompositionSweepGenerator.Generate(config);
        Assert.That(candidates.Count, Is.GreaterThan(0));

        for (int i = 0; i < candidates.Count; i++)
        {
            CombatAutoBattleRole[] roles = candidates[i].Roles;
            Assert.That(roles.Length, Is.InRange(4, 6));

            var weapons = new List<WeaponKind>(roles.Length);
            for (int r = 0; r < roles.Length; r++)
            {
                weapons.Add(roles[r].Weapon);
                Assert.That(roles[r].Personality, Is.Not.EqualTo(CombatAiPersonalityKind.Neutral));
            }

            Assert.That(
                CombatCompositionSweepGenerator.IsLegalWeaponComposition(weapons, 4, 6),
                Is.True,
                $"illegal weapons at candidate {i}");
        }
    }

    [Test]
    public void Generate_UsesExplicitCandidatesWhenProvided()
    {
        var config = new CombatCompositionSweepConfig
        {
            MinPartySize = 4,
            MaxPartySize = 6,
            Candidates = new[]
            {
                new CombatCompositionCandidate
                {
                    Roles = new[]
                    {
                        new CombatAutoBattleRole { Weapon = WeaponKind.Sword, Personality = CombatAiPersonalityKind.BattleJunkie },
                        new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.Reckless },
                        new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.Devoted },
                        new CombatAutoBattleRole { Weapon = WeaponKind.Rosary, Personality = CombatAiPersonalityKind.Lonely },
                    },
                },
            },
        };

        List<CombatCompositionCandidate> candidates = CombatCompositionSweepGenerator.Generate(config);
        Assert.That(candidates.Count, Is.EqualTo(1));
        Assert.That(candidates[0].Roles[0].Weapon, Is.EqualTo(WeaponKind.Sword));
    }

    [Test]
    public void Generate_SkipsExplicitCandidatesOutsidePartySize()
    {
        var config = new CombatCompositionSweepConfig
        {
            MinPartySize = 4,
            MaxPartySize = 6,
            Candidates = new[]
            {
                new CombatCompositionCandidate
                {
                    Roles = new[]
                    {
                        new CombatAutoBattleRole { Weapon = WeaponKind.Sword, Personality = CombatAiPersonalityKind.BattleJunkie },
                        new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.Reckless },
                        new CombatAutoBattleRole { Weapon = WeaponKind.Rosary, Personality = CombatAiPersonalityKind.Lonely },
                    },
                },
            },
        };

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("明示候補0"));
        Assert.That(CombatCompositionSweepGenerator.Generate(config).Count, Is.EqualTo(0));
    }

    [Test]
    public void Outcomes_UseStableEnglishKeys()
    {
        Assert.That(
            CombatAutoBattleOutcomes.FromBattleState(CombatBattleState.Victory, timedOut: false),
            Is.EqualTo(CombatAutoBattleOutcomes.Victory));
        Assert.That(
            CombatAutoBattleOutcomes.FromBattleState(CombatBattleState.Defeat, timedOut: false),
            Is.EqualTo(CombatAutoBattleOutcomes.Defeat));
        Assert.That(
            CombatAutoBattleOutcomes.FromBattleState(CombatBattleState.Victory, timedOut: true),
            Is.EqualTo(CombatAutoBattleOutcomes.Timeout));
    }

    [Test]
    public void Generate_EnumeratesEveryLegalFiveMemberComposition()
    {
        var config = new CombatCompositionSweepConfig
        {
            EnumerateAllCandidates = true,
            MinPartySize = 5,
            MaxPartySize = 5,
        };

        List<CombatCompositionCandidate> candidates = CombatCompositionSweepGenerator.Generate(config);
        var keys = new HashSet<string>();
        for (int i = 0; i < candidates.Count; i++)
        {
            Assert.That(candidates[i].Roles.Length, Is.EqualTo(5));
            Assert.That(keys.Add(CombatCompositionSweepGenerator.BuildKey(candidates[i].Roles)), Is.True);
        }

        Assert.That(candidates.Count, Is.EqualTo(6993));
    }

    [Test]
    public void Generate_SlicesEnumeratedCandidatesWithoutChangingTheirOrder()
    {
        var allConfig = new CombatCompositionSweepConfig
        {
            EnumerateAllCandidates = true,
            MinPartySize = 5,
            MaxPartySize = 5,
        };
        List<CombatCompositionCandidate> all = CombatCompositionSweepGenerator.Generate(allConfig);

        var sliceConfig = new CombatCompositionSweepConfig
        {
            EnumerateAllCandidates = true,
            MinPartySize = 5,
            MaxPartySize = 5,
            CandidateOffset = 100,
            CandidateLimit = 3,
        };
        List<CombatCompositionCandidate> slice = CombatCompositionSweepGenerator.Generate(sliceConfig);

        Assert.That(slice.Count, Is.EqualTo(3));
        Assert.That(
            CombatCompositionSweepGenerator.BuildKey(slice[0].Roles),
            Is.EqualTo(CombatCompositionSweepGenerator.BuildKey(all[100].Roles)));
    }

    [Test]
    public void ResolveSweepSeed_KeepsCandidateSeedsStableAcrossMatchJobs()
    {
        var config = new CombatCompositionSweepConfig
        {
            BaseSeed = 1000,
            MatchOffset = 20,
            EnumerateAllCandidates = true,
            CandidateOffset = 50,
        };

        int seed = CombatAutoBattleRunner.ResolveSweepSeed(
            config,
            localCandidateIndex: 2,
            localMatchIndex: 3,
            totalMatchesPerCandidate: 100);

        Assert.That(seed, Is.EqualTo(6223));
    }

    [Test]
    public void ResolveSweepSeed_UsesTheSameMatchSeedForEveryCandidateWhenConfigured()
    {
        var config = new CombatCompositionSweepConfig
        {
            BaseSeed = 1000,
            MatchOffset = 20,
            UseCommonSeeds = true,
        };

        int first = CombatAutoBattleRunner.ResolveSweepSeed(config, 0, 3, 100);
        int another = CombatAutoBattleRunner.ResolveSweepSeed(config, 12, 3, 100);

        Assert.That(first, Is.EqualTo(1023));
        Assert.That(another, Is.EqualTo(first));
    }

    [Test]
    public void ResolveSingleSeed_RepeatsOrAdvancesAccordingToTheSetting()
    {
        Assert.That(CombatAutoBattleRunner.ResolveSingleSeed(100, 3, fixedSeed: true), Is.EqualTo(100));
        Assert.That(CombatAutoBattleRunner.ResolveSingleSeed(100, 3, fixedSeed: false), Is.EqualTo(103));
    }

    [Test]
    public void AutoBattleStatistics_ReportMedianMinimumAndMaximum()
    {
        float[] values = { 8f, 2f, 4f, 6f };

        Assert.That(CombatAutoBattleStatistics.Median(values), Is.EqualTo(5f));
        Assert.That(CombatAutoBattleStatistics.Min(values), Is.EqualTo(2f));
        Assert.That(CombatAutoBattleStatistics.Max(values), Is.EqualTo(8f));
    }
}
