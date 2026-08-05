using System.Collections.Generic;

public class Shield : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _strBonus;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Shield;
    public override float Range => _range;
    public override int STRBonus => _strBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.STR;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Shield(
        float range = 1.8f,
        float cooldown = 1.3f,
        int strBonus = 6,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _strBonus = strBonus;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
