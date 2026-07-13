using System.Collections.Generic;

public class Grimoire : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _intBonus;
    private readonly float _chaseEnemyBias;
    private readonly float _hideInForestBias;
    private readonly float _seekHighGroundBias;
    private readonly float _followMeleeAllyBias;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Grimoire;
    public override float Range => _range;
    public override int INTBonus => _intBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.INT;
    public override float ChaseEnemyBias => _chaseEnemyBias;
    public override float HideInForestBias => _hideInForestBias;
    public override float SeekHighGroundBias => _seekHighGroundBias;
    public override float FollowMeleeAllyBias => _followMeleeAllyBias;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Grimoire(
        float range = 30f,
        float cooldown = 2f,
        int intBonus = 14,
        float chaseEnemyBias = 0f,
        float hideInForestBias = 0f,
        float seekHighGroundBias = 50f,
        float followMeleeAllyBias = 0f,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _intBonus = intBonus;
        _chaseEnemyBias = chaseEnemyBias;
        _hideInForestBias = hideInForestBias;
        _seekHighGroundBias = seekHighGroundBias;
        _followMeleeAllyBias = followMeleeAllyBias;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
