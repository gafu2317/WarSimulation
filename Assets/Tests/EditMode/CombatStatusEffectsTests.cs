using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatStatusEffectsTests
{
    [Test]
    public void CombatStatusEffects_ApplyReducesMultiplier()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatStatusEffects statusEffects = character.StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f);

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(character.STRBuff, Is.EqualTo(0.7f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_UpdatesSameKeyEffectInsteadOfStacking()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, StatDebuffSkill.GetEffectKey(CombatStatusEffects.StatKind.STR));
            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, StatDebuffSkill.GetEffectKey(CombatStatusEffects.StatKind.STR));

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(0.7f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_AllowsDifferentKeysOnSameStat()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, "DebuffA");
            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.8f, 5f, "DebuffB");

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(0.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_HasActiveEffectAndRemainingSeconds()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(
                CombatStatusEffects.StatKind.FAI,
                1.2f,
                6f,
                StatBuffSkill.GetEffectKey(CombatStatusEffects.StatKind.FAI));

            Assert.That(statusEffects.HasActiveEffect(StatBuffSkill.GetEffectKey(CombatStatusEffects.StatKind.FAI)), Is.True);
            Assert.That(statusEffects.GetRemainingSeconds(StatBuffSkill.GetEffectKey(CombatStatusEffects.StatKind.FAI)), Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_ExpiredEffectIsRemoved()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f);
            ExpireAllEffects(statusEffects);

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    private static void ExpireAllEffects(CombatStatusEffects statusEffects)
    {
        FieldInfo effectsField = typeof(CombatStatusEffects).GetField(
            "_effects",
            BindingFlags.NonPublic | BindingFlags.Instance);
        object effectsList = effectsField?.GetValue(statusEffects);
        if (effectsList == null) return;

        System.Collections.IList list = (System.Collections.IList)effectsList;
        for (int i = 0; i < list.Count; i++)
        {
            object effect = list[i];
            FieldInfo expiresAtField = effect.GetType().GetField(
                "ExpiresAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            expiresAtField?.SetValue(effect, -1f);
            list[i] = effect;
        }
    }
}
