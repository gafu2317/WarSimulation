using System.Reflection;
using NUnit.Framework;
using UnityEngine;

internal static class CombatEditModeTestUtil
{
    public static void SetPrivateField(object target, string fieldName, object value)
    {
        Assert.That(target, Is.Not.Null, "SetPrivateField target must not be null.");
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field ??= target.GetType().BaseType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    public static void WireMapSystem(CombatCharacterSystem system, CombatMapSystem mapSystem)
    {
        SetPrivateField(system, "_mapSystem", mapSystem);
    }

    public static void WireVision(CombatVision vision, CombatCharacterSystem system)
    {
        SetPrivateField(vision, "_characterSystem", system);
    }

    public static void WireCollector(
        CombatAiContextCollector collector,
        CombatCharacterSystem system,
        CombatMapSystem mapSystem)
    {
        SetPrivateField(collector, "_characterSystem", system);
        SetPrivateField(collector, "_mapSystem", mapSystem);
    }

    public static void WireBattleFlow(CombatBattleFlow flow, CombatMagicStoneSystem stoneSystem)
    {
        flow.SetMagicStoneSystem(stoneSystem);
    }

    public static CombatSkillCatalog CreateTestSkillCatalog(params SkillDefinition[] definitions)
    {
        var catalog = ScriptableObject.CreateInstance<CombatSkillCatalog>();
        CombatEditModeTestUtil.SetPrivateField(catalog, "_definitions", definitions ?? System.Array.Empty<SkillDefinition>());
        return catalog;
    }

    public static SkillDefinition CreateTestSkillDefinition(
        SkillId skillId,
        WeaponKind requiredWeaponKind,
        string displayName = null)
    {
        var definition = ScriptableObject.CreateInstance<SkillDefinition>();
        definition.ConfigureForTests(skillId, requiredWeaponKind, displayName);
        return definition;
    }

    public static void WireSkillCatalog(Character character, CombatSkillCatalog catalog)
    {
        SetPrivateField(character, "_skillCatalogOverride", catalog);
        character.RebuildCombatSkills();
    }

    public static void SetAvailableCombatSkills(Character character, params SkillBase[] skills)
    {
        Assert.That(character, Is.Not.Null);
        var field = character.GetType().GetField("_availableCombatSkills", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        var list = (System.Collections.Generic.List<SkillBase>)field.GetValue(character);
        list.Clear();

        if (skills != null)
        {
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null)
                {
                    list.Add(skills[i]);
                }
            }
        }
    }
}
