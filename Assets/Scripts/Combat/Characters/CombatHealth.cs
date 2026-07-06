using System;
using UnityEngine;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatCharacterBody))]
public sealed class CombatHealth : MonoBehaviour, ICombatHealthSource
{
    public sealed class IncomingDamageContext
    {
        public IncomingDamageContext(int amount, Character attacker)
        {
            Amount = amount;
            Attacker = attacker;
        }

        public int Amount { get; set; }
        public Character Attacker { get; }
        public bool IsHandled { get; set; }
    }

    [SerializeField, Min(1)] private int _maxHP = 1;
    [SerializeField, Min(0)] private int _hp = 1;
    [SerializeField, Min(0.1f)] private float _retreatArrivalDistance = 1.25f;
    [SerializeField, Min(0f)] private float _minReviveDelay = 5f;

    private Character _owner;
    private CombatCharacterBody _body;
    private CombatCharacterSystem _characterSystem;
    private Vector3 _retreatDestination;
    private bool _hasRetreatDestination;
    private float _retreatStartTime;

    public int MaxHP => _maxHP;
    public int HP => _hp;
    public bool IsAlive => _hp > 0;
    public LifeState LifeState { get; private set; } = LifeState.Active;
    public bool IsTargetable => LifeState == LifeState.Active && _hp > 0;
    public bool CanAct =>
        LifeState == LifeState.Active &&
        _hp > 0 &&
        (ResolveOwner() == null ||
            ResolveOwner().StatusEffects == null ||
            !ResolveOwner().StatusEffects.HasActiveEffectImmediate(CombatStatusEffects.EffectType.Bind));
    public bool HasRetreatDestination => _hasRetreatDestination;

    public event Action HealthChanged;
    public event Action<IncomingDamageContext> IncomingDamage;
    public event Action<int, Character> Damaged;
    public event Action<Character, Character> Defeated;

    private void Awake()
    {
        _owner = GetComponent<Character>();
        _body = GetComponent<CombatCharacterBody>();
    }

    private void Update()
    {
        TryCompleteRetreatIfArrived();
    }

    public void Initialize(int maxHP, int currentHP = -1)
    {
        _maxHP = Mathf.Max(1, maxHP);
        _hp = Mathf.Clamp(currentHP < 0 ? _maxHP : currentHP, 0, _maxHP);
        LifeState = _hp > 0 ? LifeState.Active : LifeState.Retreating;
        _hasRetreatDestination = false;
        NotifyHealthChanged();
    }

    public int TakeDamage(int amount, Character attacker = null)
    {
        if (amount <= 0 || !IsTargetable) return 0;

        var incomingDamage = new IncomingDamageContext(amount, attacker);
        IncomingDamage?.Invoke(incomingDamage);
        if (incomingDamage.IsHandled) return 0;

        amount = incomingDamage.Amount;
        if (amount <= 0 || !IsTargetable) return 0;

        Character owner = ResolveOwner();
        if (owner != null &&
            owner.StatusEffects != null &&
            owner.StatusEffects.HasActiveEffectImmediate(CombatStatusEffects.EffectType.Invulnerable))
        {
            return 0;
        }

        int previousHP = _hp;
        _hp = Mathf.Max(0, _hp - amount);
        int appliedDamage = previousHP - _hp;

        if (_hp == 0)
        {
            EnterRetreat();
            Defeated?.Invoke(ResolveOwner(), attacker);
        }

        if (appliedDamage > 0)
        {
            Damaged?.Invoke(appliedDamage, attacker);
        }

        NotifyHealthChanged();
        return appliedDamage;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || LifeState != LifeState.Active) return 0;

        int previousHP = _hp;
        _hp = Mathf.Min(_maxHP, _hp + amount);
        int healed = _hp - previousHP;
        if (healed > 0) NotifyHealthChanged();
        return healed;
    }

    public void RestoreFull()
    {
        _hp = _maxHP;
        LifeState = LifeState.Active;
        _hasRetreatDestination = false;
        _retreatStartTime = 0f;
        NotifyHealthChanged();
    }

    public void EnterRetreat()
    {
        _hp = 0;
        LifeState = LifeState.Retreating;
        _retreatStartTime = Time.time;

        _hasRetreatDestination = false;
        if (TryResolveHomePosition(out Vector3 homePosition))
        {
            _retreatDestination = homePosition;

            _body ??= GetComponent<CombatCharacterBody>();
            if (IsAtRetreatDestination(homePosition))
            {
                _hasRetreatDestination = true;
                TryCompleteRetreatIfArrived();
                return;
            }

            if (_body != null && _body.TrySetDestination(homePosition))
            {
                _hasRetreatDestination = true;
            }
        }
    }

    public bool TryCompleteRetreatIfArrived()
    {
        if (LifeState != LifeState.Retreating || !_hasRetreatDestination) return false;
        if (Time.time - _retreatStartTime < _minReviveDelay) return false;
        if (!IsAtRetreatDestination(_retreatDestination)) return false;

        RestoreFull();
        return true;
    }

    private bool TryResolveHomePosition(out Vector3 homePosition)
    {
        _owner ??= GetComponent<Character>();
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem != null && characterSystem.TryGetHomePosition(_owner, out homePosition))
        {
            return true;
        }

        homePosition = default;
        return false;
    }

    private bool IsAtRetreatDestination(Vector3 destination)
    {
        Vector3 currentPosition = transform.position;
        currentPosition.y = 0f;
        destination.y = 0f;
        return Vector3.Distance(currentPosition, destination) <= _retreatArrivalDistance;
    }

    private CombatCharacterSystem ResolveCharacterSystem()
    {
        if (_characterSystem != null) return _characterSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            _characterSystem = context.CharacterSystem;
            return _characterSystem;
        }

        _characterSystem = FindAnyObjectByType<CombatCharacterSystem>();
        return _characterSystem;
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke();
    }

    private Character ResolveOwner()
    {
        _owner ??= GetComponent<Character>();
        return _owner;
    }
}
