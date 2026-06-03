using UnityEngine;

public sealed class BibleGotsumeSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly int _reflectDamage;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;

    public BibleGotsumeSkill(
        float maxRange = 6f,
        int reflectDamage = 4,
        float durationSeconds = 5f,
        float cooldownSeconds = 7f)
    {
        _maxRange = maxRange;
        _reflectDamage = reflectDamage;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "ゴツメ";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null || !target.Health.IsAlive) return;

        BibleGotsumeEffect effect = target.GetComponent<BibleGotsumeEffect>();
        if (effect == null)
        {
            effect = target.gameObject.AddComponent<BibleGotsumeEffect>();
        }

        effect.Initialize(target, _reflectDamage, _durationSeconds);
    }
}
