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
        if (skill == null || target == null || target.Health == null || owner == null) return false;

        if (skill.TargetKind == SkillTargetKind.Ally ||
            skill.TargetKind == SkillTargetKind.AllyOrSelf)
        {
            if (target == owner) return target.Health.CanAct;
            if (target.Team != owner.Team || !target.Health.CanAct) return false;

            float distance = Vector3.Distance(owner.transform.position, target.transform.position);
            return distance <= skill.MaxRange;
        }

        if (target.Team == owner.Team || !target.Health.IsTargetable) return false;

        float distance = Vector3.Distance(owner.transform.position, target.transform.position);
        return distance <= skill.MaxRange;
    }

    protected bool CanExecuteSkill(SkillBase skill, Character target)
    {
        Character owner = Owner;
        if (owner == null || owner.Health == null || !owner.Health.CanAct) return false;
        if (skill == null || target == null) return false;
        if (owner.SkillCooldowns != null && !owner.SkillCooldowns.IsReady(skill)) return false;

        return IsValidSkillTarget(skill, target);
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
        if (plan.Skill == null || plan.SkillTarget == null) return;
        if (Time.time < _nextSkillCommandTime) return;
        if (!CanExecuteSkill(plan.Skill, plan.SkillTarget)) return;

        Character owner = Owner;
        plan.Skill.Execute(owner, plan.SkillTarget);
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
