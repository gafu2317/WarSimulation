using System.Collections.Generic;

public class Grimoire : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _basePower;
    private readonly float _chaseEnemyBias;
    private readonly float _hideInForestBias;
    private readonly float _seekHighGroundBias;
    private readonly float _followMeleeAllyBias;
    private readonly bool _sharesObservationFromHighGround;
    private readonly IReadOnlyList<SkillBase> _skills;

    public override WeaponKind Kind => WeaponKind.Grimoire;
    public override float Range => _range;
    public override int BasePower => _basePower;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.INT;
    public override float ChaseEnemyBias => _chaseEnemyBias;
    public override float HideInForestBias => _hideInForestBias;
    public override float SeekHighGroundBias => _seekHighGroundBias;
    public override float FollowMeleeAllyBias => _followMeleeAllyBias;
    public override bool SharesObservationFromHighGround => _sharesObservationFromHighGround;
    public override IReadOnlyList<SkillBase> Skills => _skills;

    public Grimoire(
        float range = 7f,
        float cooldown = 2f,
        float basePower = 14f,
        float chaseEnemyBias = 0f,
        float hideInForestBias = 0f,
        float seekHighGroundBias = 50f,
        float followMeleeAllyBias = 0f,
        bool sharesObservationFromHighGround = true,
        IReadOnlyList<SkillBase> skills = null)
    {
        _range = range;
        _cooldown = cooldown;
        _basePower = (int)basePower;
        _chaseEnemyBias = chaseEnemyBias;
        _hideInForestBias = hideInForestBias;
        _seekHighGroundBias = seekHighGroundBias;
        _followMeleeAllyBias = followMeleeAllyBias;
        _sharesObservationFromHighGround = sharesObservationFromHighGround;
        _skills = skills ?? new SkillBase[] { new GrimoireStrDebuffSkill() };
    }
}
