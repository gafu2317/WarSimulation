using System.Collections.Generic;

public class Bible : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _faiBonus;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Bible;
    public override float Range => _range;
    public override int FAIBonus => _faiBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.FAI;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Bible(
        float range = 30f,
        float cooldown = 1.6f,
        int faiBonus = 10,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _faiBonus = faiBonus;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
