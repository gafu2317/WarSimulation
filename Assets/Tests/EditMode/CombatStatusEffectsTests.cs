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

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, GrimoireStrDebuffSkill.EffectKey);
            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, GrimoireStrDebuffSkill.EffectKey);

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
                RosaryFaithBuffSkill.EffectKey);

            Assert.That(statusEffects.HasActiveEffect(RosaryFaithBuffSkill.EffectKey), Is.True);
            Assert.That(statusEffects.GetRemainingSeconds(RosaryFaithBuffSkill.EffectKey), Is.GreaterThan(0f));
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

    [Test]
    public void CombatAttack_DamageIsReducedWhenAttackerHasStrDebuff()
    {
        GameObject attackerGo = new GameObject("Attacker");
        try
        {
            Character attacker = attackerGo.AddComponent<Character>();
            attacker.EquipWeapon(new Sword());
            SetCharacterStr(attacker, 20);

            int baseDamage = attacker.Attack.CalculateDamage(attacker.EquippedWeapon);

            attacker.StatusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f);
            int debuffedDamage = attacker.Attack.CalculateDamage(attacker.EquippedWeapon);

            Assert.That(baseDamage, Is.EqualTo(22));
            Assert.That(debuffedDamage, Is.EqualTo(19));
            Assert.That(debuffedDamage, Is.LessThan(baseDamage));
        }
        finally
        {
            Object.DestroyImmediate(attackerGo);
        }
    }

    private static void SetCharacterStr(Character character, int str)
    {
        PropertyInfo property = typeof(Character).GetProperty(
            nameof(Character.STR),
            BindingFlags.Public | BindingFlags.Instance);
        property?.SetValue(character, str);
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
