using System.Collections.Generic;

public class Rosary : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _faiBonus;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Rosary;
    public override float Range => _range;
    public override int FAIBonus => _faiBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.FAI;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Rosary(
        float range = 15f,
        float cooldown = 1.2f,
        int faiBonus = 8,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _faiBonus = faiBonus;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
