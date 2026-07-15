using UnityEngine;

public sealed class RosaryHealingAreaSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _radius;
    private readonly int _healPerTick;
    private readonly float _durationSeconds;
    private readonly float _tickIntervalSeconds;
    private readonly float _cooldownSeconds;

    public RosaryHealingAreaSkill(
        float maxRange = 35f,
        float radius = 3f,
        int healPerTick = 4,
        float durationSeconds = 5f,
        float tickIntervalSeconds = 1f,
        float cooldownSeconds = 7f)
    {
        _maxRange = maxRange;
        _radius = radius;
        _healPerTick = healPerTick;
        _durationSeconds = durationSeconds;
        _tickIntervalSeconds = tickIntervalSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "回復エリア";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 1.5f;
    public override SkillTargetKind TargetKind => SkillTargetKind.Point;
    public override float MaxRange => _maxRange;
    public override float AreaRadius => _radius;

    public override int EstimateHealing(Character self, SkillExecutionContext context, Character target)
    {
        int tickCount = Mathf.Max(1, Mathf.CeilToInt(_durationSeconds / Mathf.Max(0.1f, _tickIntervalSeconds)));
        return Mathf.Max(0, _healPerTick) * tickCount;
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasTargetPoint) return;

        var zoneGo = new GameObject("RosaryHealingAreaZone");
        zoneGo.transform.position = context.TargetPoint;
        var zone = zoneGo.AddComponent<RosaryHealingAreaZone>();
        zone.Initialize(
            self,
            _radius,
            _healPerTick,
            _durationSeconds,
            _tickIntervalSeconds);
        CombatSkillActionEvents.RecordCharacterEffect(
            CombatActionEffectKind.PersistentEffectStarted,
            CombatEffectSource.Capture(self),
            null,
            statusKey: "RosaryHealingArea");
    }
}
