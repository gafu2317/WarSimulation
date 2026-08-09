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
                Assert.That((int)roles[r].Personality, Is.Not.EqualTo(6));
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
}
