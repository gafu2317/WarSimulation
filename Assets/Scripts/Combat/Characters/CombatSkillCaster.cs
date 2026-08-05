using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
public sealed class CombatSkillCaster : MonoBehaviour
{
    private Character _owner;
    private SkillExecutionContext _context;
    private float _startedAt;
    private float _completeAt;
    private CombatSkillActionInfo _castingAction;

    public bool IsCasting => CastingSkill != null;
    public SkillBase CastingSkill { get; private set; }
    public SkillExecutionContext CastingContext => IsCasting ? _context : SkillExecutionContext.None;
    public float RemainingSeconds => IsCasting ? Mathf.Max(0f, _completeAt - Time.time) : 0f;
    public float NormalizedProgress => !IsCasting
        ? 0f
        : Mathf.Clamp01((Time.time - _startedAt) / Mathf.Max(CastingSkill.CastTimeSeconds, Mathf.Epsilon));

    private void Awake()
    {
        _owner = GetComponent<Character>();
    }

    private void Update()
    {
        Tick(Time.time);
    }

    public bool TryStartCast(SkillBase skill, SkillExecutionContext context)
    {
        _owner ??= GetComponent<Character>();
        if (skill == null || IsCasting) return false;
        if (_owner == null || _owner.Health == null) return false;
        if (!CombatBattleFlow.AllowsCombatActions || !_owner.Health.CanAct) return false;

        FaceStoneTargets(context);
        context = context.Capture(_owner);
        CombatSkillActionInfo action = CombatSkillActionEvents.Start(_owner, skill, context);
        if (skill.CastTimeSeconds <= 0f)
        {
            Execute(action, skill, context);
            return true;
        }

        CastingSkill = skill;
        _context = context;
        _castingAction = action;
        _startedAt = Time.time;
        _completeAt = _startedAt + skill.CastTimeSeconds;
        _owner.StopMoving();
        return true;
    }

    private void FaceStoneTargets(SkillExecutionContext context)
    {
        if (_owner == null) return;

        if (context.PrimaryStone != null)
        {
            _owner.FaceHorizontalToward(context.PrimaryStone.transform.position);
            return;
        }

        if (context.ResolvedStones != null && context.ResolvedStones.Count > 0 && context.ResolvedStones[0] != null)
        {
            _owner.FaceHorizontalToward(context.ResolvedStones[0].transform.position);
            return;
        }

        if (context.HasTargetPoint && context.ResolvedStones != null && context.ResolvedStones.Count > 0)
            _owner.FaceHorizontalToward(context.TargetPoint);
    }

    public void Tick(float now)
    {
        _owner ??= GetComponent<Character>();
        if (!IsCasting) return;
        if (_owner == null || _owner.Health == null)
        {
            ClearCast();
            return;
        }

        if (!CombatBattleFlow.AllowsCombatActions || !_owner.Health.IsTargetable)
        {
            ClearCast();
            return;
        }

        if (now < _completeAt) return;

        SkillBase skill = CastingSkill;
        SkillExecutionContext context = _context;
        CombatSkillActionInfo action = _castingAction;
        ResetCast();
        Execute(action, skill, context);
    }

    public void ClearCast()
    {
        if (IsCasting)
        {
            CombatSkillActionInfo action = _castingAction;
            ResetCast();
            CombatSkillActionEvents.Cancel(action);
            return;
        }

        ResetCast();
    }

    private void ResetCast()
    {
        CastingSkill = null;
        _context = SkillExecutionContext.None;
        _castingAction = default;
    }

    private void Execute(
        CombatSkillActionInfo action,
        SkillBase skill,
        SkillExecutionContext context)
    {
        if (_owner == null || skill == null) return;
        CombatSkillActionEvents.Execute(action, () => skill.Execute(_owner, context));
        _owner.SkillCooldowns.StartCooldown(skill);
        CombatSkillUseEvents.RaiseSkillUsed(_owner, skill.Name);
    }
}
