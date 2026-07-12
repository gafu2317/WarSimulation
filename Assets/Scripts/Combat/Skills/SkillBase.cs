using UnityEngine;

public abstract class SkillBase
{
    public abstract string Name { get; }

    public virtual float CooldownSeconds => 0f;

    public virtual float CastTimeSeconds => 0f;

    public virtual string CooldownKey => GetType().FullName;

    public virtual SkillTargetKind TargetKind => SkillTargetKind.Enemy;

    public virtual float MaxRange => float.PositiveInfinity;

    public virtual float AreaRadius => 0f;

    public virtual bool CanTargetMagicStone => false;

    public virtual int SelfHpCost => 0;

    public virtual int EstimateDamage(Character self, SkillExecutionContext context, Character target)
    {
        return 0;
    }

    public virtual int EstimateHealing(Character self, SkillExecutionContext context, Character target)
    {
        return 0;
    }

    public abstract void Execute(Character self, SkillExecutionContext context);

    protected static float ComputeHorizontalDistance(Character self, Character target)
    {
        if (self == null || target == null) return 0f;
        return ComputeHorizontalDistance(self.transform.position, target.transform.position);
    }

    protected static float ComputeHorizontalDistance(Character self, MagicStone target)
    {
        if (self == null || target == null) return 0f;
        return ComputeHorizontalDistance(self.transform.position, target.transform.position);
    }

    protected static float ComputeHorizontalDistance(Character self, Vector3 targetPoint)
    {
        if (self == null) return 0f;
        return ComputeHorizontalDistance(self.transform.position, targetPoint);
    }

    protected static float ComputeHorizontalDistance(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return Vector3.Distance(from, to);
    }

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
        if (targetVision != null)
        {
            if (ShouldRefreshRecognition(self, target))
            {
                targetVision.UpdateVision();
            }

            if (!targetVision.HasRecognitionOf(self))
            {
                multiplier *= 1.5f;
            }
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
    }

    protected static int TakeDamage(Character self, SkillExecutionContext context, int amount)
    {
        if (context.PrimaryTarget != null)
        {
            return TakeDamage(self, context, context.PrimaryTarget, amount);
        }

        return TakeDamage(context.PrimaryStone, amount);
    }

    protected static int TakeDamage(
        Character self,
        SkillExecutionContext context,
        Character target,
        int amount)
    {
        if (target == null || target.Health == null || !target.Health.IsTargetable) return 0;

        int damage = context.IsCaptured
            ? Mathf.Max(1, Mathf.RoundToInt(amount * context.GetDamageMultiplier(target)))
            : ComputeStealthAwareDamage(self, target, amount);
        return target.Health.TakeDamage(damage, self);
    }

    protected static int TakeDamage(MagicStone target, int amount)
    {
        if (target == null || target.FeatureIndex < 0 || amount <= 0) return 0;

        CombatMagicStoneSystem system = CombatMagicStoneSystemResolver.Resolve();
        return system != null ? system.TakeDamage(target.FeatureIndex, amount) : 0;
    }

    protected static int ApplyDamageModifiers(Character self, SkillExecutionContext context, Character target, int amount)
    {
        if (target == null || amount <= 0) return 0;
        return context.IsCaptured
            ? Mathf.Max(1, Mathf.RoundToInt(amount * context.GetDamageMultiplier(target)))
            : ComputeStealthAwareDamage(self, target, amount);
    }

    protected static void BreakStealthOnUse(Character self)
    {
        if (self == null || self.StatusEffects == null || !self.StatusEffects.IsStealthed) return;

        self.StatusEffects.ClearEffect(CombatStatusEffects.EffectType.Stealth);
    }

    private static bool ShouldRefreshRecognition(Character self, Character target)
    {
        if (self == null || target == null) return false;

        CombatCharacterSystem[] systems = Object.FindObjectsByType<CombatCharacterSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            CombatCharacterSystem system = systems[i];
            bool hasSelf = system.AllyCharacters.Contains(self) || system.EnemyCharacters.Contains(self);
            bool hasTarget = system.AllyCharacters.Contains(target) || system.EnemyCharacters.Contains(target);
            if (hasSelf && hasTarget)
            {
                return true;
            }
        }

        return false;
    }
}
