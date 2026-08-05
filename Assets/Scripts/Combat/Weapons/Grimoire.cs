using System.Collections.Generic;

public class Grimoire : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _intBonus;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Grimoire;
    public override float Range => _range;
    public override int INTBonus => _intBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.INT;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Grimoire(
        float range = 30f,
        float cooldown = 2f,
        int intBonus = 14,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _intBonus = intBonus;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
