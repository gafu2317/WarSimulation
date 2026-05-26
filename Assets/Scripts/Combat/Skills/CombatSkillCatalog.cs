using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CombatSkillCatalog",
    menuName = "WarSimulation/Combat/Skill Catalog")]
public sealed class CombatSkillCatalog : ScriptableObject
{
    [SerializeField] private SkillDefinition[] _definitions = System.Array.Empty<SkillDefinition>();

    public IReadOnlyList<SkillDefinition> Definitions => _definitions;

    public bool TryGetDefinition(SkillId skillId, out SkillDefinition definition)
    {
        for (int i = 0; i < _definitions.Length; i++)
        {
            SkillDefinition candidate = _definitions[i];
            if (candidate == null || candidate.SkillId != skillId) continue;

            definition = candidate;
            return true;
        }

        definition = null;
        return false;
    }

    public IReadOnlyList<SkillDefinition> GetDefinitionsForKind(WeaponKind kind)
    {
        var matches = new List<SkillDefinition>();
        for (int i = 0; i < _definitions.Length; i++)
        {
            SkillDefinition definition = _definitions[i];
            if (definition == null || definition.RequiredWeaponKind != kind) continue;

            matches.Add(definition);
        }

        return matches;
    }

    public static CombatSkillCatalog CreateDefaultRuntimeCatalog()
    {
        var catalog = CreateInstance<CombatSkillCatalog>();
        catalog._definitions = new[]
        {
            CreateDefinition(SkillId.Sword_Slash, WeaponKind.Sword, "斬撃"),
            CreateDefinition(SkillId.Shield_Guard, WeaponKind.Shield, "守護"),
            CreateDefinition(SkillId.Wand_Bolt, WeaponKind.Wand, "魔弾"),
            CreateDefinition(SkillId.Grimoire_StrDebuff, WeaponKind.Grimoire, "STRデバフ"),
            CreateDefinition(SkillId.Bible_Heal, WeaponKind.Bible, "回復"),
            CreateDefinition(SkillId.Rosary_FaithBuff, WeaponKind.Rosary, "信仰バフ"),
        };
        return catalog;
    }

    private static SkillDefinition CreateDefinition(SkillId skillId, WeaponKind kind, string displayName)
    {
        var definition = CreateInstance<SkillDefinition>();
        definition.ConfigureForTests(skillId, kind, displayName);
        return definition;
    }
}
