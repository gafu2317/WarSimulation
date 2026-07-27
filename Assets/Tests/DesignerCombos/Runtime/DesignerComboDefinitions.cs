using System;
using System.Collections.Generic;

public enum DesignerComboKind
{
    BindFollowUp,
    PoisonFortress,
    MagicStoneAssault,
    FrontlineBreakthrough,
    RemoteSupportLoneWolf,
    DecoyBombardment,
    DecoySustain,
    DiversionMagicStoneAssault,
    LoversFollowUnit,
    OppositeGenderEscort,
    DistributedHunt,
    MagicStoneAssaultRosary,
    FrontlineBreakthroughBible,
    OppositeGenderEscortBible,
}

public enum DesignerComboTestScope
{
    BehaviorCheck,
    Comparison,
    ExtendedComparison,
    AddedMembers,
    Counter,
}

public enum DesignerComboTerrainKind
{
    Open,
    Forest,
    ChokePoint,
    Production,
}

public enum DesignerComboVariantKind
{
    Linked,
    Ablated,
    Normal,
    Counter,
}

[Serializable]
public sealed class DesignerComboRoleDefinition
{
    public string Id;
    public WeaponKind Weapon;
    public CombatAiPersonalityKind Personality;

    public DesignerComboRoleDefinition(string id, WeaponKind weapon, CombatAiPersonalityKind personality)
    {
        Id = id;
        Weapon = weapon;
        Personality = personality;
    }
}

[Serializable]
public sealed class DesignerComboScenarioDefinition
{
    public DesignerComboKind Kind;
    public string DisplayName;
    public string PrimaryMetricName;
    public DesignerComboRoleDefinition[] Roles;
    public DesignerComboRoleDefinition[] CounterRoles;
    public int ScalableRoleIndex;
    public float LinkDistance;
    public bool RequiresLovers;
    public bool RequiresOppositeGenders;

    public DesignerComboScenarioDefinition(
        DesignerComboKind kind,
        string displayName,
        string primaryMetricName,
        DesignerComboRoleDefinition[] roles,
        DesignerComboRoleDefinition[] counterRoles,
        int scalableRoleIndex = -1,
        float linkDistance = 8f,
        bool requiresLovers = false,
        bool requiresOppositeGenders = false)
    {
        Kind = kind;
        DisplayName = displayName;
        PrimaryMetricName = primaryMetricName;
        Roles = roles;
        CounterRoles = counterRoles;
        ScalableRoleIndex = scalableRoleIndex;
        LinkDistance = linkDistance;
        RequiresLovers = requiresLovers;
        RequiresOppositeGenders = requiresOppositeGenders;
    }
}

public static class DesignerComboScenarioCatalog
{
    private static readonly IReadOnlyList<DesignerComboScenarioDefinition> Scenarios = CreateScenarios();

    public static IReadOnlyList<DesignerComboScenarioDefinition> All => Scenarios;

    public static DesignerComboScenarioDefinition Get(DesignerComboKind kind)
    {
        for (int i = 0; i < Scenarios.Count; i++)
        {
            if (Scenarios[i].Kind == kind) return Scenarios[i];
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "未定義のデザイナーズコンボです。");
    }

    private static IReadOnlyList<DesignerComboScenarioDefinition> CreateScenarios()
    {
        return new[]
        {
            Scenario(
                DesignerComboKind.BindFollowUp,
                "拘束追撃",
                "拘束中の双剣ダメージ",
                Roles(Role("妨害役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("追撃役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie)),
                Roles(Role("接近役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("遠隔役", WeaponKind.Wand, CombatAiPersonalityKind.Calm))),
            Scenario(
                DesignerComboKind.PoisonFortress,
                "毒籠城",
                "毒の有効ダメージ",
                Roles(Role("毒役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("足止め役", WeaponKind.Shield, CombatAiPersonalityKind.Cautious)),
                Roles(Role("突破役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("突破支援", WeaponKind.Bible, CombatAiPersonalityKind.HotBlooded)),
                scalableRoleIndex: 0),
            Scenario(
                DesignerComboKind.MagicStoneAssault,
                "魔石強襲",
                "強襲役の魔石ダメージ",
                Roles(Role("強襲役", WeaponKind.Wand, CombatAiPersonalityKind.Reckless), Role("護衛役", WeaponKind.Shield, CombatAiPersonalityKind.Devoted)),
                Roles(Role("追撃役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("拘束役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning))),
            Scenario(
                DesignerComboKind.MagicStoneAssaultRosary,
                "魔石強襲（ロザリオ型）",
                "強襲役の魔石ダメージ",
                Roles(Role("強襲役", WeaponKind.Wand, CombatAiPersonalityKind.Reckless), Role("回復役", WeaponKind.Rosary, CombatAiPersonalityKind.Lonely)),
                Roles(Role("追撃役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("拘束役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning))),
            Scenario(
                DesignerComboKind.FrontlineBreakthrough,
                "前線突破",
                "支援範囲内での追跡時間",
                Roles(Role("突破役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("同行役", WeaponKind.Shield, CombatAiPersonalityKind.HotBlooded)),
                Roles(Role("分断役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("砲撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm)),
                linkDistance: 6f),
            Scenario(
                DesignerComboKind.FrontlineBreakthroughBible,
                "前線突破（聖書型）",
                "支援範囲内での追跡時間",
                Roles(Role("突破役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("同行役", WeaponKind.Bible, CombatAiPersonalityKind.Lonely)),
                Roles(Role("分断役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("砲撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm)),
                linkDistance: 6f),
            Scenario(
                DesignerComboKind.RemoteSupportLoneWolf,
                "遠隔支援付き一匹狼",
                "単独交戦の維持時間",
                Roles(Role("単独役", WeaponKind.Sword, CombatAiPersonalityKind.LoneWolf), Role("遠隔回復役", WeaponKind.Rosary, CombatAiPersonalityKind.Coward)),
                Roles(Role("後衛接近役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("妨害役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning))),
            Scenario(
                DesignerComboKind.DecoyBombardment,
                "囮砲撃",
                "範囲攻撃の同時命中数",
                Roles(Role("囮役", WeaponKind.Shield, CombatAiPersonalityKind.AttentionSeeker), Role("砲撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm)),
                Roles(Role("範囲攻撃役1", WeaponKind.Wand, CombatAiPersonalityKind.Calm), Role("範囲攻撃役2", WeaponKind.Wand, CombatAiPersonalityKind.Calm))),
            Scenario(
                DesignerComboKind.DecoySustain,
                "囮維持",
                "複数に狙われた盾の生存時間",
                Roles(Role("囮役", WeaponKind.Shield, CombatAiPersonalityKind.AttentionSeeker), Role("回復役", WeaponKind.Rosary, CombatAiPersonalityKind.Coward)),
                Roles(Role("集中役1", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("集中役2", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie))),
            Scenario(
                DesignerComboKind.DiversionMagicStoneAssault,
                "陽動魔石強襲",
                "陽動中の魔石ダメージ",
                Roles(Role("陽動役", WeaponKind.Shield, CombatAiPersonalityKind.AttentionSeeker), Role("強襲役", WeaponKind.Wand, CombatAiPersonalityKind.Reckless)),
                Roles(Role("追撃役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("遠隔役", WeaponKind.Wand, CombatAiPersonalityKind.Calm)),
                scalableRoleIndex: 1,
                linkDistance: 18f),
            Scenario(
                DesignerComboKind.LoversFollowUnit,
                "恋人追従部隊",
                "下世話の能力上昇時間",
                Roles(Role("恋人1", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie), Role("恋人2", WeaponKind.Shield, CombatAiPersonalityKind.Devoted), Role("追従役", WeaponKind.Bible, CombatAiPersonalityKind.Gossiper)),
                Roles(Role("分断役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("範囲攻撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm), Role("突破役", WeaponKind.Sword, CombatAiPersonalityKind.BattleJunkie)),
                scalableRoleIndex: 2,
                requiresLovers: true),
            Scenario(
                DesignerComboKind.OppositeGenderEscort,
                "異性護衛",
                "スケベの能力上昇時間",
                Roles(Role("護衛役", WeaponKind.Shield, CombatAiPersonalityKind.Devoted), Role("支援役", WeaponKind.Rosary, CombatAiPersonalityKind.Lecherous)),
                Roles(Role("分断役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("範囲攻撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm)),
                scalableRoleIndex: 1,
                requiresOppositeGenders: true),
            Scenario(
                DesignerComboKind.OppositeGenderEscortBible,
                "異性護衛（聖書型）",
                "スケベの能力上昇時間",
                Roles(Role("護衛役", WeaponKind.Shield, CombatAiPersonalityKind.Devoted), Role("支援役", WeaponKind.Bible, CombatAiPersonalityKind.Lecherous)),
                Roles(Role("分断役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning), Role("範囲攻撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm)),
                scalableRoleIndex: 1,
                requiresOppositeGenders: true),
            Scenario(
                DesignerComboKind.DistributedHunt,
                "分散狩り",
                "攻撃対象の非重複時間",
                Roles(Role("狩人1", WeaponKind.Sword, CombatAiPersonalityKind.LoneWolf), Role("狩人2", WeaponKind.Sword, CombatAiPersonalityKind.LoneWolf), Role("妨害役", WeaponKind.Grimoire, CombatAiPersonalityKind.Cunning)),
                Roles(Role("範囲攻撃役", WeaponKind.Wand, CombatAiPersonalityKind.Calm), Role("護衛役", WeaponKind.Shield, CombatAiPersonalityKind.Devoted), Role("回復役", WeaponKind.Rosary, CombatAiPersonalityKind.Coward)),
                scalableRoleIndex: 0),
        };
    }

    private static DesignerComboScenarioDefinition Scenario(
        DesignerComboKind kind,
        string name,
        string metric,
        DesignerComboRoleDefinition[] roles,
        DesignerComboRoleDefinition[] counters,
        int scalableRoleIndex = -1,
        float linkDistance = 8f,
        bool requiresLovers = false,
        bool requiresOppositeGenders = false)
    {
        return new DesignerComboScenarioDefinition(kind, name, metric, roles, counters, scalableRoleIndex, linkDistance, requiresLovers, requiresOppositeGenders);
    }

    private static DesignerComboRoleDefinition Role(string id, WeaponKind weapon, CombatAiPersonalityKind personality)
    {
        return new DesignerComboRoleDefinition(id, weapon, personality);
    }

    private static DesignerComboRoleDefinition[] Roles(params DesignerComboRoleDefinition[] roles) => roles;
}
