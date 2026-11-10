using NUnit.Framework;
using UnityEngine;

public sealed class CombatAutoBattleBalanceTests
{
    [Test]
    public void PenaltyTargetsRecklessAndIncludesWeaponBonusWithoutAccumulating()
    {
        var instance = new GameObject("Balance test");
        var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
        var reckless = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Reckless);
        try
        {
            var character = instance.AddComponent<Character>();
            typeof(Character).GetProperty("STR").SetValue(character, 30);
            int originalHP = character.MaxHP;
            float originalReviveDelay = character.Health.ReviveDelayAfterArrival;
            int originalSTR = character.STR + weapon.CreateWeapon().GetStatBonus(CombatStat.STR);
            var balance = new CombatAutoBattleBalanceSettings
            {
                RecklessHPMultiplier = 0.8f,
                RecklessSTRMultiplier = 0.7f,
                ReviveDelayAfterArrivalSeconds = 30f,
            };
            var setup = balance.CreateSetup(character, weapon, reckless);
            Assert.That(setup.StatAdjustments[CombatStat.STR], Is.EqualTo(Mathf.RoundToInt(originalSTR * 0.7f) - originalSTR));
            character.ConfigureForBattle(weapon, reckless, statAdjustments: setup.StatAdjustments);
            Assert.That(character.MaxHP, Is.EqualTo(Mathf.RoundToInt(originalHP * 0.8f)));
            setup = balance.CreateSetup(character, weapon, reckless);
            Assert.That(setup.StatAdjustments[CombatStat.STR], Is.EqualTo(Mathf.RoundToInt(originalSTR * 0.7f) - originalSTR));
            setup = balance.CreateSetup(character, weapon, null);
            Assert.That(setup.StatAdjustments, Is.Empty);
            Assert.That(character.MaxHP, Is.EqualTo(originalHP));
            Assert.That(character.Health.ReviveDelayAfterArrival, Is.EqualTo(30f));
            new CombatAutoBattleBalanceSettings().CreateSetup(character, weapon, null);
            Assert.That(character.Health.ReviveDelayAfterArrival, Is.EqualTo(originalReviveDelay));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(reckless);
        }
    }

    [TestCase(30, 24)]
    [TestCase(31, 25)]
    [TestCase(1, 1)]
    public void RecklessExperimentReplacesItsPenaltyAndRestoresTheDefault(int agi, int expected)
    {
        var instance = new GameObject("Reckless AGI test");
        var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
        var reckless = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Reckless);
        try
        {
            var character = instance.AddComponent<Character>();
            typeof(Character).GetProperty("AGI").SetValue(character, agi);
            character.ConfigureForBattle(weapon, reckless);
            float defaultAgi = character.GetEffectiveStat(CombatStat.AGI);
            var balance = new CombatAutoBattleBalanceSettings { RecklessAGIMultiplier = 0.8f };
            var setup = balance.CreateSetup(character, weapon, reckless);
            character.ConfigureForBattle(weapon, reckless, statAdjustments: setup.StatAdjustments);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(expected));
            balance.RecklessAGIMultiplier = 1f;
            setup = balance.CreateSetup(character, weapon, reckless);
            character.ConfigureForBattle(weapon, reckless, statAdjustments: setup.StatAdjustments);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(agi));
            setup = new CombatAutoBattleBalanceSettings().CreateSetup(character, weapon, reckless);
            character.ConfigureForBattle(weapon, reckless, statAdjustments: setup.StatAdjustments);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(defaultAgi));
            character.ConfigureForBattle(weapon, null);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(agi));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(reckless);
        }
    }

    [Test]
    public void GatekeeperHpBonusRestoresOnRevivalAndDoesNotAccumulate()
    {
        var instance = new GameObject("Gatekeeper test");
        var gatekeeper = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper);
        try
        {
            var character = instance.AddComponent<Character>();
            character.Health.Initialize(120);
            character.ConfigureForBattle(null, gatekeeper);
            int boostedHP = character.MaxHP;
            Assert.That(character.HP, Is.EqualTo(boostedHP));
            character.Health.Initialize(120, 0);
            character.Health.RestoreFull();
            Assert.That(character.HP, Is.EqualTo(boostedHP));
            character.ConfigureForBattle(null, gatekeeper);
            Assert.That(character.MaxHP, Is.EqualTo(boostedHP));
            character.ConfigureForBattle(null, null);
            Assert.That(character.MaxHP, Is.EqualTo(120));
            Assert.That(character.HP, Is.EqualTo(120));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(gatekeeper);
        }
    }

    [Test]
    public void CunningAgiBonusIncludesManualAdjustmentAndClearsOnPersonalityChange()
    {
        var instance = new GameObject("Cunning test");
        var cunning = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Cunning);
        try
        {
            var character = instance.AddComponent<Character>();
            typeof(Character).GetProperty("AGI").SetValue(character, 30);
            var adjustments = new System.Collections.Generic.Dictionary<CombatStat, int> { [CombatStat.AGI] = 5 };
            character.ConfigureForBattle(null, cunning, statAdjustments: adjustments);
            float adjusted = character.GetEffectiveStat(CombatStat.AGI);
            character.ConfigureForBattle(null, cunning, statAdjustments: adjustments);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(adjusted));
            character.ConfigureForBattle(null, cunning);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.LessThan(adjusted));
            character.ConfigureForBattle(null, null);
            Assert.That(character.GetEffectiveStat(CombatStat.AGI), Is.EqualTo(30));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(cunning);
        }
    }

    [TestCase(30)]
    [TestCase(31)]
    [TestCase(1)]
    public void AttentionSeekerAllStatsIncludeAdjustmentsAndClearWithoutAccumulating(int value)
    {
        var instance = new GameObject("Attention seeker test");
        var profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.AttentionSeeker);
        var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
        try
        {
            var character = instance.AddComponent<Character>();
            character.Health.Initialize(120);
            var adjustments = new System.Collections.Generic.Dictionary<CombatStat, int>();
            foreach (var stat in new[] { CombatStat.STR, CombatStat.INT, CombatStat.FAI, CombatStat.AGI })
            {
                typeof(Character).GetProperty(stat.ToString()).SetValue(character, value);
                adjustments[stat] = 5;
            }
            adjustments[CombatStat.HP] = 10;
            character.ConfigureForBattle(weapon, null, statAdjustments: adjustments);
            int baselineHP = character.MaxHP;
            var baseline = new System.Collections.Generic.Dictionary<CombatStat, float>();
            foreach (var stat in new[] { CombatStat.STR, CombatStat.INT, CombatStat.FAI, CombatStat.AGI })
                baseline[stat] = character.GetEffectiveStat(stat);
            character.ConfigureForBattle(weapon, profile, statAdjustments: adjustments);
            int boostedHP = character.MaxHP;
            var boosted = new System.Collections.Generic.Dictionary<CombatStat, float>();
            foreach (var stat in baseline.Keys)
                boosted[stat] = character.GetEffectiveStat(stat);
            character.ConfigureForBattle(weapon, profile, statAdjustments: adjustments);
            Assert.That(character.MaxHP, Is.EqualTo(boostedHP));
            foreach (var stat in baseline.Keys)
                Assert.That(character.GetEffectiveStat(stat), Is.EqualTo(boosted[stat]));
            character.Health.Initialize(120, 0);
            character.Health.RestoreFull();
            Assert.That(character.HP, Is.EqualTo(boostedHP));
            character.ConfigureForBattle(weapon, null, statAdjustments: adjustments);
            Assert.That(character.MaxHP, Is.EqualTo(baselineHP));
            foreach (var stat in baseline.Keys)
                Assert.That(character.GetEffectiveStat(stat), Is.EqualTo(baseline[stat]));
            character.ConfigureForBattle(null, null);
            Assert.That(character.MaxHP, Is.EqualTo(120));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void InvalidBalanceCannotStartExperiment()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new CombatAutoBattleBalanceSettings { RecklessHPMultiplier = 0f }.Validate());
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new CombatAutoBattleBalanceSettings { ReviveDelayAfterArrivalSeconds = float.NaN }.Validate());
    }
}
