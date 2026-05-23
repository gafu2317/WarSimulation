public abstract class SkillBase
{
    public abstract string Name { get; }

    public virtual float CooldownSeconds => 0f;

    public virtual string CooldownKey => GetType().FullName;

    public virtual SkillTargetKind TargetKind => SkillTargetKind.Enemy;

    public abstract float EvaluateScore(Character self, Character target);

    public abstract void Execute(Character self, Character target);
}
