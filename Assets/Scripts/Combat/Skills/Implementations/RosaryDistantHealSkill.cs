using UnityEngine;

public sealed class RosaryDistantHealSkill : SkillBase
{
    private readonly int _baseHeal;
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryDistantHealSkill(
        int baseHeal = 3,
        float faiScale = 0.3f,
        float maxRange = 9f,
        float cooldownSeconds = 3.5f)
    {
        _baseHeal = baseHeal;
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "遠隔癒し";

    public override float CooldownSeconds => _cooldownSeconds;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.CanAct) return;
        if (!IsWithinRange(self, target)) return;

        int healAmount = Mathf.Max(1, _baseHeal + Mathf.RoundToInt(self.FAI * _faiScale));
        target.Health.Heal(healAmount);
    }

    private bool IsWithinRange(Character self, Character target)
    {
        if (target == self) return true;

        float distance = Vector3.Distance(self.transform.position, target.transform.position);
        return distance <= _maxRange;
    }
}
