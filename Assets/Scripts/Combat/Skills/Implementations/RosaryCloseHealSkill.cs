using UnityEngine;

public sealed class RosaryCloseHealSkill : SkillBase
{
    private readonly int _baseHeal;
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryCloseHealSkill(
        int baseHeal = 15,
        float faiScale = 0.8f,
        float maxRange = 2.5f,
        float cooldownSeconds = 7f)
    {
        _baseHeal = baseHeal;
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "大回復";

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
