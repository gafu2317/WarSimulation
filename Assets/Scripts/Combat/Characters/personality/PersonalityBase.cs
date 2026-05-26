using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatCharacterBody))]
public abstract class PersonalityBase : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float _decisionInterval = 0.2f;
    [SerializeField, Min(0.02f)] private float _moveCommandInterval = 0.5f;

    private Character _owner;
    private CombatCharacterBody _body;
    private CombatAiContextCollector _contextCollector;
    private float _nextDecisionTime;
    private float _nextMoveCommandTime;

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;
    public bool HasPlannedOnce { get; private set; }

    protected Character Owner => _owner != null ? _owner : _owner = GetComponent<Character>();

    protected virtual void Awake()
    {
        ResolveComponents();
    }

    protected virtual void Update()
    {
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
