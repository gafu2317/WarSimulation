using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatAiContextCollector))]
public sealed class CombatAiBrain : MonoBehaviour
{
    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(0.05f)] private float _decisionIntervalSeconds = 0.5f;
    [SerializeField] private bool _executeMovement = true;
    [SerializeField] private bool _executeSkills = true;
    [SerializeField] private bool _showObjectiveLabel = true;

    private Character _owner;
    private CombatAiContextCollector _contextCollector;
    private CombatAiWorldLabel _worldLabel;
    private float _nextDecisionTime;

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;
    public CombatAiContext LastContext { get; private set; }
    public CombatSkillEvaluationResult LastSkillEvaluation { get; private set; }
    public bool HasLastSkillEvaluation { get; private set; }

    private void Awake()
    {
        ResolveDependencies();
        RefreshWorldLabel();
    }

    private void Update()
    {
        RefreshWorldLabel();
        if (!_enabled || Time.time < _nextDecisionTime) return;

        _nextDecisionTime = Time.time + _decisionIntervalSeconds;
        TickNow();
    }

    public bool TickNow()
    {
        ResolveDependencies();
        if (!CanRun()) return false;

        LastContext = _contextCollector.Collect(_owner);
        LastPlan = CombatAiPlanner.BuildPlan(LastContext, _owner.PersonalityProfile);
        RefreshWorldLabel();
        return ExecutePlan(LastPlan);
    }

    public bool ExecutePlan(CombatAiPlan plan)
    {
        ResolveDependencies();
        if (!CanRun()) return false;

        LastPlan = plan;
        RefreshWorldLabel();
        bool usedSkill = TryExecuteSkill(plan);
        bool moved = TryExecuteMovement(plan);
        return usedSkill || moved;
    }

    private bool TryExecuteSkill(CombatAiPlan plan)
    {
        HasLastSkillEvaluation = false;
        if (!_executeSkills || plan.Skill == null) return false;

        CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(
            _owner,
            plan.Skill,
            plan.SkillContext);
        LastSkillEvaluation = evaluation;
        HasLastSkillEvaluation = true;

        if (!evaluation.CanUse) return false;

        plan.Skill.Execute(_owner, evaluation.Context);
        _owner.SkillCooldowns?.StartCooldown(plan.Skill);
        return true;
    }

    private bool TryExecuteMovement(CombatAiPlan plan)
    {
        if (!_executeMovement || !plan.MoveTarget.HasDestination) return false;

        Vector3 destination = plan.MoveTarget.Kind == CombatMoveTargetKind.Character &&
            plan.MoveTarget.TargetCharacter != null
                ? plan.MoveTarget.TargetCharacter.transform.position
                : plan.MoveTarget.Destination;
        _owner.MoveToTarget(destination);
        return true;
    }

    private bool CanRun()
    {
        if (_owner == null || _contextCollector == null) return false;
        if (_owner.Health == null || !_owner.Health.CanAct) return false;
        return CombatBattleFlow.IsRunning;
    }

    private void ResolveDependencies()
    {
        if (_owner == null)
        {
            _owner = GetComponent<Character>();
        }

        if (_contextCollector == null)
        {
            _contextCollector = GetComponent<CombatAiContextCollector>();
            if (_contextCollector == null)
            {
                _contextCollector = gameObject.AddComponent<CombatAiContextCollector>();
            }
        }

        if (_worldLabel == null)
        {
            _worldLabel = GetComponent<CombatAiWorldLabel>();
            if (_worldLabel == null && _showObjectiveLabel)
            {
                _worldLabel = gameObject.AddComponent<CombatAiWorldLabel>();
            }
        }
    }

    private void RefreshWorldLabel()
    {
        if (_worldLabel == null) return;
        _worldLabel.SetVisible(_showObjectiveLabel);
        if (_showObjectiveLabel)
        {
            _worldLabel.SetObjective(LastPlan.Objective, _enabled && _owner != null && _owner.Health != null && _owner.Health.IsAlive);
        }
    }
}
