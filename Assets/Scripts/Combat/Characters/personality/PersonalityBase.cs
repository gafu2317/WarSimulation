using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatCharacterBody))]
public abstract class PersonalityBase : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float _decisionInterval = 0.2f;
    [SerializeField, Min(0.02f)] private float _moveCommandInterval = 0.5f;
    [SerializeField, Min(0.02f)] private float _skillCommandInterval = 0.5f;

    private Character _owner;
    private CombatCharacterBody _body;
    private CombatAiContextCollector _contextCollector;
    private float _nextDecisionTime;
    private float _nextMoveCommandTime;
    private float _nextSkillCommandTime;

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;
    public bool HasPlannedOnce { get; private set; }

    protected Character Owner => _owner != null ? _owner : _owner = GetComponent<Character>();

    protected virtual void Awake()
    {
        ResolveComponents();
    }

    protected virtual void Update()
    {
        if (!CombatBattleFlow.IsRunning) return;
        if (Time.time < _nextDecisionTime) return;

        _nextDecisionTime = Time.time + _decisionInterval;
        Tick();
    }

    public void Tick()
    {
        ResolveComponents();

        LastPlan = DecidePlan();
        HasPlannedOnce = true;
        ExecuteMove(LastPlan.MoveTarget);
        ExecuteSkill(LastPlan);
    }

    public abstract CombatAiPlan DecidePlan();

    protected virtual bool TryMoveTo(Vector3 destination)
    {
        return _body != null && _body.TrySetDestination(destination);
    }

    protected IReadOnlyList<Character> GetVisibleEnemies()
    {
        Character owner = Owner;
        CombatVision vision = owner != null ? owner.Vision : null;
        if (vision == null) return System.Array.Empty<Character>();

        vision.UpdateVision();
        return vision.VisibleEnemies;
    }

    protected WeaponBase GetCurrentWeapon()
    {
        Character owner = Owner;
        return owner != null && owner.EquippedWeapon != null ? owner.EquippedWeapon : WeaponBase.Unarmed;
    }

    protected IReadOnlyList<SkillBase> GetAvailableCombatSkills()
    {
        Character owner = Owner;
        if (owner == null) return System.Array.Empty<SkillBase>();

        return owner.AvailableCombatSkills;
    }

    protected bool IsValidSkillTarget(SkillBase skill, Character target)
    {
        Character owner = Owner;
        if (owner == null || skill == null) return false;

        CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
            skill,
            CombatSkillEvaluationRequest.ForTarget(owner, target));
        return result.CanUse;
    }

    protected bool IsValidSkillContext(SkillBase skill, SkillExecutionContext context)
    {
        Character owner = Owner;
        if (skill == null || owner == null) return false;

        CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(owner, skill, context);
        return result.CanUse;
    }

    protected bool CanExecuteSkill(SkillBase skill, Character target)
    {
        return CanExecuteSkill(skill, SkillExecutionContext.ForTarget(target));
    }

    protected bool CanExecuteSkill(SkillBase skill, SkillExecutionContext context)
    {
        Character owner = Owner;
        if (owner == null || skill == null) return false;

        CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(owner, skill, context);
        return result.CanUse;
    }

    protected CombatAiContext CollectContext()
    {
        ResolveComponents();
        return _contextCollector != null
            ? _contextCollector.Collect(Owner)
            : new CombatAiContext(
                Owner,
                System.Array.Empty<Character>(),
                System.Array.Empty<Character>(),
                System.Array.Empty<Character>(),
                System.Array.Empty<CombatCharacterIntel>(),
                System.Array.Empty<CombatCharacterIntel>(),
                default,
                Vector3.zero,
                false,
                default,
                false,
                default,
                System.Array.Empty<Vector3>(),
                System.Array.Empty<Vector3>(),
                System.Array.Empty<Vector3>(),
                System.Array.Empty<Vector3>());
    }

    private void ExecuteMove(CombatMoveTarget target)
    {
        if (!target.HasDestination) return;
        if (Time.time < _nextMoveCommandTime) return;

        _nextMoveCommandTime = Time.time + _moveCommandInterval;
        TryMoveTo(target.Destination);
    }

    private void ExecuteSkill(CombatAiPlan plan)
    {
        if (plan.Skill == null) return;
        if (Time.time < _nextSkillCommandTime) return;
        Character owner = Owner;
        CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(owner, plan.Skill, plan.SkillContext);
        if (!result.CanUse) return;

        plan.Skill.Execute(owner, result.Context);
        owner.SkillCooldowns?.StartCooldown(plan.Skill);
        _nextSkillCommandTime = Time.time + _skillCommandInterval;
    }

    private void ResolveComponents()
    {
        _owner ??= GetComponent<Character>();
        _body ??= GetComponent<CombatCharacterBody>();
        _contextCollector ??= GetComponent<CombatAiContextCollector>();
        if (_contextCollector == null)
        {
            _contextCollector = gameObject.AddComponent<CombatAiContextCollector>();
        }
    }
}
