using System.Collections.Generic;

public class Sword : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _strBonus;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Sword;
    public override float Range => _range;
    public override int STRBonus => _strBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.STR;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Sword(
        float range = 2f,
        float cooldown = 0.9f,
        int strBonus = 12,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _strBonus = strBonus;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
