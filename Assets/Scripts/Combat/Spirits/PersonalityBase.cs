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
    private float _nextDecisionTime;
    private float _nextMoveCommandTime;

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;

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
    }
}
