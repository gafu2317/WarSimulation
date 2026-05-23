using System.Collections.Generic;

public class Sword : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _basePower;
    private readonly float _chaseEnemyBias;
    private readonly float _hideInForestBias;
    private readonly float _seekHighGroundBias;
    private readonly float _followMeleeAllyBias;
    private readonly IReadOnlyList<SkillBase> _skills;

    public override WeaponKind Kind => WeaponKind.Sword;
    public override float Range => _range;
    public override int BasePower => _basePower;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.STR;
    public override float ChaseEnemyBias => _chaseEnemyBias;
    public override float HideInForestBias => _hideInForestBias;
    public override float SeekHighGroundBias => _seekHighGroundBias;
    public override float FollowMeleeAllyBias => _followMeleeAllyBias;
    public override IReadOnlyList<SkillBase> Skills => _skills;

    public Sword(
        float range = 2f,
        float cooldown = 1f,
        float basePower = 12f,
        float chaseEnemyBias = 0f,
        float hideInForestBias = 0f,
        float seekHighGroundBias = 0f,
        float followMeleeAllyBias = 0f,
        IReadOnlyList<SkillBase> skills = null)
    {
        _range = range;
        _cooldown = cooldown;
        _basePower = (int)basePower;
        _chaseEnemyBias = chaseEnemyBias;
        _hideInForestBias = hideInForestBias;
        _seekHighGroundBias = seekHighGroundBias;
        _followMeleeAllyBias = followMeleeAllyBias;
        _skills = skills ?? System.Array.Empty<SkillBase>();
    }
}
