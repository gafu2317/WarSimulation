using System;
using System.Collections.Generic;

public class WeaponBase
{
    public static readonly WeaponBase Unarmed = new WeaponBase();

    // 品質
    public int Quality { private set; get; }

    // 使用可能回数
    public int UsableCount { private set; get; } = 5;

    public virtual WeaponKind Kind => WeaponKind.Unarmed;
    public virtual float Range => 1.5f;
    public virtual int STRBonus => 0;
    public virtual int INTBonus => 0;
    public virtual int FAIBonus => 0;
    public virtual int AGIBonus => 0;
    public virtual float CooldownSeconds => 1.2f;
    public virtual CombatStat ScalingStat => CombatStat.STR;

    public virtual IReadOnlyList<SkillId> GrantedSkillIds => Array.Empty<SkillId>();
    public virtual int PrimaryStatBonus => GetStatBonus(ScalingStat);

    [Obsolete("Use Character.AvailableCombatSkills instead.")]
    public virtual IReadOnlyList<SkillBase> Skills => Array.Empty<SkillBase>();

    public virtual int GetStatBonus(CombatStat stat)
    {
        return stat switch
        {
            CombatStat.STR => STRBonus,
            CombatStat.INT => INTBonus,
            CombatStat.FAI => FAIBonus,
            CombatStat.AGI => AGIBonus,
            _ => 0,
        };
    }
}
