using System.Collections.Generic;

public class Sword : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _strBonus;
    private readonly float _chaseEnemyBias;
    private readonly float _hideInForestBias;
    private readonly float _seekHighGroundBias;
    private readonly float _followMeleeAllyBias;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Sword;
    public override float Range => _range;
    public override int STRBonus => _strBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.STR;
    public override float ChaseEnemyBias => _chaseEnemyBias;
    public override float HideInForestBias => _hideInForestBias;
    public override float SeekHighGroundBias => _seekHighGroundBias;
    public override float FollowMeleeAllyBias => _followMeleeAllyBias;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Sword(
        float range = 2f,
        float cooldown = 0.9f,
        int strBonus = 12,
        float chaseEnemyBias = 0f,
        float hideInForestBias = 0f,
        float seekHighGroundBias = 0f,
        float followMeleeAllyBias = 0f,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _strBonus = strBonus;
        _chaseEnemyBias = chaseEnemyBias;
        _hideInForestBias = hideInForestBias;
        _seekHighGroundBias = seekHighGroundBias;
        _followMeleeAllyBias = followMeleeAllyBias;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
