using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WeaponConfigTests
{
    [Test]
    public void CreateWeapon_SwordConfig_MatchesDefaultSwordValues()
    {
        WeaponConfig config = CreateConfig(WeaponKind.Sword);
        try
        {
            AssertWeapon(config.CreateWeapon(), 2f, 12, 0.9f, CombatStat.STR, WeaponKind.Sword);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void CreateWeapon_AllKinds_ProduceExpectedWeaponTypes()
    {
        AssertKind(WeaponKind.Sword, typeof(Sword));
        AssertKind(WeaponKind.Shield, typeof(Shield));
        AssertKind(WeaponKind.Wand, typeof(Wand));
        AssertKind(WeaponKind.Grimoire, typeof(Grimoire));
        AssertKind(WeaponKind.Bible, typeof(Bible));
        AssertKind(WeaponKind.Rosary, typeof(Rosary));
        AssertKind(WeaponKind.Unarmed, typeof(WeaponBase));
    }

    [Test]
    public void CreateWeapon_CustomValues_AreApplied()
    {
        WeaponConfig config = ScriptableObject.CreateInstance<WeaponConfig>();
        try
        {
            config.ApplyKindDefaults(WeaponKind.Wand);
            SetField(config, "_range", 9.5f);
            SetField(config, "_cooldownSeconds", 2.1f);
            SetField(config, "_primaryStatBonus", 15);

            WeaponBase weapon = config.CreateWeapon();

            Assert.That(weapon, Is.InstanceOf<Wand>());
            Assert.That(weapon.Range, Is.EqualTo(9.5f).Within(0.001f));
            Assert.That(weapon.PrimaryStatBonus, Is.EqualTo(15));
            Assert.That(weapon.CooldownSeconds, Is.EqualTo(2.1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ApplyKindDefaults_Wand_SetsCombatDefaults()
    {
        WeaponConfig config = CreateConfig(WeaponKind.Wand);
        try
        {
            Assert.That(config.Range, Is.EqualTo(20f).Within(0.001f));
            Assert.That(config.CooldownSeconds, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(config.PrimaryStatBonus, Is.EqualTo(10));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ApplyKindDefaults_Sword_SetsCombatDefaults()
    {
        WeaponConfig config = CreateConfig(WeaponKind.Sword);
        try
        {
            Assert.That(config.Range, Is.EqualTo(2f).Within(0.001f));
            Assert.That(config.CooldownSeconds, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(config.PrimaryStatBonus, Is.EqualTo(12));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Character_AppliesInitialWeaponConfig_OnApply()
    {
        GameObject characterGo = new GameObject("Character");
        WeaponConfig config = CreateConfig(WeaponKind.Grimoire);
        try
        {
            Character character = characterGo.AddComponent<Character>();
            SetField(character, "_initialWeaponConfig", config);

            Assert.That(character.EquippedWeapon, Is.Null);

            character.ApplyInitialWeaponFromConfig();

            Assert.That(character.EquippedWeapon, Is.InstanceOf<Grimoire>());
            Assert.That(character.EquippedWeapon.Range, Is.EqualTo(30f).Within(0.001f));
            Assert.That(character.EquippedWeapon.PrimaryStatBonus, Is.EqualTo(14));
            Assert.That(character.EquippedWeapon.Kind, Is.EqualTo(WeaponKind.Grimoire));
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Character_WithoutInitialWeaponConfig_StaysUnarmed()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.ApplyInitialWeaponFromConfig();

            Assert.That(character.EquippedWeapon, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Character_InitializeOnBattleStart_ReappliesWeaponFromConfig()
    {
        GameObject characterGo = new GameObject("Character");
        WeaponConfig config = CreateConfig(WeaponKind.Sword);
        try
        {
            Character character = characterGo.AddComponent<Character>();
            SetField(character, "_initialWeaponConfig", config);
            character.UnEquipWeapon();

            character.InitializeOnBattleStart();

            Assert.That(character.EquippedWeapon, Is.InstanceOf<Sword>());
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Character_InitializeOnBattleStart_ReusesConfiguredLoadout()
    {
        GameObject characterGo = new GameObject("Character");
        WeaponConfig config = CreateConfig(WeaponKind.Sword);
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.ConfigureForBattle(config, null);
            WeaponBase configuredWeapon = character.EquippedWeapon;

            character.InitializeOnBattleStart();

            Assert.That(character.EquippedWeapon, Is.SameAs(configuredWeapon));
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CreateWeapon_GrantedSkillIds_ArePassedToWeapon()
    {
        WeaponConfig config = CreateConfig(WeaponKind.Wand);
        try
        {
            SetField(config, "_grantedSkillIds", new[] { SkillId.Wand_Bolt });

            Wand weapon = config.CreateWeapon() as Wand;

            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.GrantedSkillIds.Count, Is.EqualTo(1));
            Assert.That(weapon.GrantedSkillIds[0], Is.EqualTo(SkillId.Wand_Bolt));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void AssertKind(WeaponKind kind, System.Type expectedType)
    {
        WeaponConfig config = CreateConfig(kind);
        try
        {
            WeaponBase weapon = config.CreateWeapon();
            Assert.That(weapon, Is.InstanceOf(expectedType));
            Assert.That(weapon.Kind, Is.EqualTo(kind));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static WeaponConfig CreateConfig(WeaponKind kind)
    {
        WeaponConfig config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.ApplyKindDefaults(kind);
        return config;
    }

    private static void AssertWeapon(
        WeaponBase weapon,
        float range,
        int power,
        float cooldown,
        CombatStat stat,
        WeaponKind kind)
    {
        Assert.That(weapon.Kind, Is.EqualTo(kind));
        Assert.That(weapon.Range, Is.EqualTo(range).Within(0.001f));
        Assert.That(weapon.PrimaryStatBonus, Is.EqualTo(power));
        Assert.That(weapon.CooldownSeconds, Is.EqualTo(cooldown).Within(0.001f));
        Assert.That(weapon.ScalingStat, Is.EqualTo(stat));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }
}
