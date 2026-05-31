using UnityEngine;

public abstract class SkillBase
{
    public abstract string Name { get; }

    public virtual float CooldownSeconds => 0f;

    public virtual string CooldownKey => GetType().FullName;

    public virtual SkillTargetKind TargetKind => SkillTargetKind.Enemy;

    public virtual float MaxRange => float.PositiveInfinity;

    public virtual float AreaRadius => 0f;

    public abstract void Execute(Character self, SkillExecutionContext context);

    protected static int ComputeDistanceScaledAmount(
        int baseAmount,
        float distance,
        float maxRange,
        float nearMultiplier,
        float farMultiplier)
    {
        if (baseAmount <= 0) return 1;
        if (maxRange <= 0f) return Mathf.Max(1, baseAmount);

        float t = Mathf.Clamp01(distance / maxRange);
        float multiplier = Mathf.Lerp(nearMultiplier, farMultiplier, t);
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));
    }

    protected static int ComputeStealthAwareDamage(Character self, Character target, int baseDamage)
    {
        if (self == null || target == null) return Mathf.Max(1, baseDamage);

        float multiplier = 1f;
        CombatVision targetVision = target.Vision;
        if (targetVision != null && !targetVision.HasRecognitionOf(self))
        {
            multiplier *= 1.5f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
    }

    protected static void BreakStealthOnUse(Character self)
    {
        if (self == null || self.StatusEffects == null || !self.StatusEffects.IsStealthed) return;

        self.StatusEffects.ClearEffect(CombatStatusEffects.EffectType.Stealth);
    }
}
