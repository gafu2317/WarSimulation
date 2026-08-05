using System.Collections.Generic;

public class Wand : WeaponBase
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _intBonus;
    private readonly IReadOnlyList<SkillId> _grantedSkillIds;

    public override WeaponKind Kind => WeaponKind.Wand;
    public override float Range => _range;
    public override int INTBonus => _intBonus;
    public override float CooldownSeconds => _cooldown;
    public override CombatStat ScalingStat => CombatStat.INT;
    public override IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public Wand(
        float range = 30f,
        float cooldown = 1.4f,
        int intBonus = 10,
        IReadOnlyList<SkillId> grantedSkillIds = null)
    {
        _range = range;
        _cooldown = cooldown;
        _intBonus = intBonus;
        _grantedSkillIds = grantedSkillIds ?? System.Array.Empty<SkillId>();
    }
}
