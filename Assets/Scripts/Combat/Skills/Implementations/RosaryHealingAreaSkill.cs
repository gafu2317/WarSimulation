using UnityEngine;

public sealed class RosaryHealingAreaSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _radius;
    private readonly float _faiScalePerTick;
    private readonly float _durationSeconds;
    private readonly float _tickIntervalSeconds;
    private readonly float _cooldownSeconds;

    public RosaryHealingAreaSkill(
        float maxRange = 35f,
        float radius = 3f,
        float faiScalePerTick = 0.1f,
        float durationSeconds = 5f,
        float tickIntervalSeconds = 1f,
        float cooldownSeconds = 7f)
    {
        _maxRange = maxRange;
        _radius = radius;
        _faiScalePerTick = faiScalePerTick;
        _durationSeconds = durationSeconds;
        _tickIntervalSeconds = tickIntervalSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "回復エリア";
    public override string PowerDescription =>
        $"FAI × {_faiScalePerTick:0.###}/{_tickIntervalSeconds:0.##}秒 × {_durationSeconds:0.##}秒";
    public override string EffectDescription => "地点に回復エリアを設置";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 1.5f;
    public override SkillTargetKind TargetKind => SkillTargetKind.Point;
    public override float MaxRange => _maxRange;
    public override float AreaRadius => _radius;

    public override int EstimateHealing(Character self, SkillExecutionContext context, Character target)
    {
        context = context.Capture(self);
        int tickCount = Mathf.Max(1, Mathf.CeilToInt(_durationSeconds / Mathf.Max(0.1f, _tickIntervalSeconds)));
        int healPerTick = Mathf.Max(
            1,
            Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScalePerTick));
        return healPerTick * tickCount;
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasTargetPoint) return;
        context = context.Capture(self);

        var zoneGo = new GameObject("RosaryHealingAreaZone");
        zoneGo.transform.position = context.TargetPoint;
        var zone = zoneGo.AddComponent<RosaryHealingAreaZone>();
        zone.Initialize(
            self,
            _radius,
            Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScalePerTick)),
            _durationSeconds,
            _tickIntervalSeconds);
        CombatSkillActionEvents.RecordCharacterEffect(
            CombatActionEffectKind.PersistentEffectStarted,
            CombatEffectSource.Capture(self),
            null,
            statusKey: "RosaryHealingArea");
    }
}
