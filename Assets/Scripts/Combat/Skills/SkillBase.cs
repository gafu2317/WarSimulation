public abstract class SkillBase
{
    public abstract string Name { get; }

    public virtual float CooldownSeconds => 0f;

    public virtual string CooldownKey => GetType().FullName;

    public virtual SkillTargetKind TargetKind => SkillTargetKind.Enemy;

    public virtual float MaxRange => float.PositiveInfinity;

    public abstract void Execute(Character self, Character target);
}
