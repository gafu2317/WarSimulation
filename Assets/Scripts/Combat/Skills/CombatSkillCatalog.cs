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
            CreateDefinition(SkillId.Shield_ShoulderGuard, WeaponKind.Shield, "肩代わり"),
            CreateDefinition(SkillId.Wand_Bolt, WeaponKind.Wand, "魔弾"),
            CreateDefinition(SkillId.Wand_ArcaneBlast, WeaponKind.Wand, "極大魔弾"),
            CreateDefinition(SkillId.Wand_AreaBlast, WeaponKind.Wand, "範囲魔法"),
            CreateDefinition(SkillId.Wand_GodsHand, WeaponKind.Wand, "神の手"),
            CreateDefinition(SkillId.Grimoire_Bolt, WeaponKind.Grimoire, "通常攻撃"),
            CreateDefinition(SkillId.Grimoire_StrDebuff, WeaponKind.Grimoire, "STRデバフ"),
            CreateDefinition(SkillId.Grimoire_Bind, WeaponKind.Grimoire, "金縛り"),
            CreateDefinition(SkillId.Grimoire_Poison, WeaponKind.Grimoire, "毒"),
            CreateDefinition(SkillId.Grimoire_Stealth, WeaponKind.Grimoire, "不可視"),
            CreateDefinition(SkillId.Bible_Smite, WeaponKind.Bible, "通常攻撃"),
            CreateDefinition(SkillId.Bible_StrBuff, WeaponKind.Bible, "STRバフ"),
            CreateDefinition(SkillId.Bible_FaiBuff, WeaponKind.Bible, "FAIバフ"),
            CreateDefinition(SkillId.Bible_IntBuff, WeaponKind.Bible, "INTバフ"),
            CreateDefinition(SkillId.Bible_AgiBuff, WeaponKind.Bible, "AGIバフ"),
            CreateDefinition(SkillId.Bible_Invulnerable, WeaponKind.Bible, "無敵"),
            CreateDefinition(SkillId.Bible_Gotsume, WeaponKind.Bible, "ゴツメ"),
            CreateDefinition(SkillId.Bible_CarryRush, WeaponKind.Bible, "高速移動"),
            CreateDefinition(SkillId.Rosary_Strike, WeaponKind.Rosary, "通常攻撃"),
            CreateDefinition(SkillId.Rosary_DistantHeal, WeaponKind.Rosary, "遠隔癒し"),
            CreateDefinition(SkillId.Rosary_CloseHeal, WeaponKind.Rosary, "大回復"),
            CreateDefinition(SkillId.Rosary_Regeneration, WeaponKind.Rosary, "継続回復"),
            CreateDefinition(SkillId.Rosary_HealingArea, WeaponKind.Rosary, "回復エリア"),
            CreateDefinition(SkillId.Rosary_SacrificeThunder, WeaponKind.Rosary, "神の雷"),
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
