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
            CreateDefinition(SkillId.Shield_Slash, WeaponKind.Shield, "盾撃"),
            CreateDefinition(SkillId.Wand_Bolt, WeaponKind.Wand, "魔弾"),
            CreateDefinition(SkillId.Wand_ArcaneBlast, WeaponKind.Wand, "極大魔弾"),
            CreateDefinition(SkillId.Grimoire_Bolt, WeaponKind.Grimoire, "呪弾"),
            CreateDefinition(SkillId.Grimoire_StrDebuff, WeaponKind.Grimoire, "STRデバフ"),
            CreateDefinition(SkillId.Bible_Smite, WeaponKind.Bible, "制裁"),
            CreateDefinition(SkillId.Bible_StrBuff, WeaponKind.Bible, "守護"),
            CreateDefinition(SkillId.Bible_FaiBuff, WeaponKind.Bible, "信仰バフ"),
            CreateDefinition(SkillId.Bible_IntBuff, WeaponKind.Bible, "INTバフ"),
            CreateDefinition(SkillId.Bible_AgiBuff, WeaponKind.Bible, "AGIバフ"),
            CreateDefinition(SkillId.Rosary_Strike, WeaponKind.Rosary, "聖撃"),
            CreateDefinition(SkillId.Rosary_DistantHeal, WeaponKind.Rosary, "遠隔癒し"),
            CreateDefinition(SkillId.Rosary_CloseHeal, WeaponKind.Rosary, "大回復"),
            CreateDefinition(SkillId.StatDebuff_INT, WeaponKind.Grimoire, "INTデバフ"),
            CreateDefinition(SkillId.StatDebuff_FAI, WeaponKind.Grimoire, "FAIデバフ"),
            CreateDefinition(SkillId.StatDebuff_AGI, WeaponKind.Grimoire, "AGIデバフ"),
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
