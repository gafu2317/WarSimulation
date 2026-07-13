using UnityEngine;

public sealed class UnstableRetaliationSkill : SkillBase
{
    public override string Name => "逆ギレ";
    public override SkillTargetKind TargetKind => SkillTargetKind.Ally;
    public override float MaxRange => 2f;

    public override int EstimateDamage(Character self, SkillExecutionContext context, Character target)
    {
        if (self == null) return 0;
        WeaponBase weapon = self.EquippedWeapon ?? WeaponBase.Unarmed;
        return Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(weapon.ScalingStat) * 0.25f));
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        int damage = EstimateDamage(self, context, target);
        if (target == null || target.Team != self.Team || damage <= 0) return;
        target.Health?.TakeDamage(damage, self);
    }
}
