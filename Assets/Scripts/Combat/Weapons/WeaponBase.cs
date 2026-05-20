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
    public virtual int BasePower => 4;
    public virtual float CooldownSeconds => 1.2f;
    public virtual CombatStat ScalingStat => CombatStat.STR;

    public virtual float ChaseEnemyBias => 0f;
    public virtual float HideInForestBias => 0f;
    public virtual float SeekHighGroundBias => 0f;
    public virtual float FollowMeleeAllyBias => 0f;
    public virtual bool SharesObservationFromHighGround => false;
    public virtual IReadOnlyList<SkillBase> Skills => Array.Empty<SkillBase>();
}
